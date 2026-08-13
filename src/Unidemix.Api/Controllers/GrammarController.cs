using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;
using Unidemix.Api.Services;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/learning/grammar")]
public sealed class GrammarController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Topics([FromQuery] string languageCode = "de", [FromQuery] string level = "A1")
    {
        var user = CurrentUserId();
        var topics = await db.GrammarTopics.AsNoTracking().Where(x => x.IsPublished && x.LanguageCode == languageCode && x.CefrLevel == level).OrderBy(x => x.Order).ToListAsync();
        var progress = await db.GrammarTopicProgress.AsNoTracking().Where(x => x.UserId == user && topics.Select(t => t.Id).Contains(x.GrammarTopicId)).ToDictionaryAsync(x => x.GrammarTopicId);
        return Ok(topics.Select(x => new { x.Id, x.ContentKey, x.Order, x.TitleDe, x.TitleFa, x.Category, x.Progression, RelatedCoreLessons = Read<string[]>(x.RelatedCoreLessonsJson), ExerciseCount = Read<GrammarExercise[]>(x.ExercisesJson).Length, Progress = progress.TryGetValue(x.Id, out var p) ? new { p.ReferenceViewedAt, p.SessionsCompleted, p.CorrectAnswers, p.TotalAnswers, p.LastPracticedAt } : null }));
    }

    [HttpGet("topics/{topicId:guid}")]
    public async Task<IActionResult> Topic(Guid topicId)
    {
        var x = await db.GrammarTopics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == topicId && x.IsPublished);
        return x is null ? NotFound() : Ok(new { x.Id, x.ContentKey, x.CefrLevel, x.TitleDe, x.TitleFa, x.Category, x.Progression, Reference = Read<GrammarReference>(x.ReferenceJson), RelatedCoreLessons = Read<string[]>(x.RelatedCoreLessonsJson), ExerciseCount = Read<GrammarExercise[]>(x.ExercisesJson).Length });
    }

    [HttpPost("topics/{topicId:guid}/reference-viewed")]
    public async Task<IActionResult> ReferenceViewed(Guid topicId)
    {
        if (!await db.GrammarTopics.AnyAsync(x => x.Id == topicId && x.IsPublished)) return NotFound();
        var user = CurrentUserId(); var progress = await db.GrammarTopicProgress.SingleOrDefaultAsync(x => x.UserId == user && x.GrammarTopicId == topicId);
        if (progress is null) { progress = new() { UserId = user, GrammarTopicId = topicId }; db.GrammarTopicProgress.Add(progress); }
        progress.ReferenceViewedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> Start(StartGrammarSession request)
    {
        var user = CurrentUserId(); var mode = request.Mode is "Mixed" or "Mistakes" ? request.Mode : "Topic";
        if (!request.ForceNew)
        {
            var existing = await db.GrammarPracticeSessions.Include(x => x.Answers).OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(x => x.UserId == user && x.Status == "InProgress" && x.Mode == mode && x.GrammarTopicId == request.TopicId && x.CefrLevel == request.CefrLevel);
            if (existing is not null) return Ok(await SessionResponse(existing));
        }
        var topics = await db.GrammarTopics.Where(x => x.IsPublished && x.LanguageCode == request.LanguageCode && x.CefrLevel == request.CefrLevel && (mode != "Topic" || x.Id == request.TopicId)).ToListAsync();
        if (topics.Count == 0) return NotFound();
        if (mode == "Mixed") topics = topics.Where(x => x.QualityStatus == "PASS").ToList();
        var pool = topics.SelectMany(t => Read<GrammarExercise[]>(t.ExercisesJson).Where(q => q.QaStatus == "PASS").Select(q => (Topic:t, Question:q))).ToList();
        if (mode == "Mistakes")
        {
            var wrong = await db.GrammarPracticeAnswers.AsNoTracking().Where(x => x.Session!.UserId == user && !x.IsCorrect).OrderByDescending(x => x.AnsweredAt).Select(x => x.QuestionId).Distinct().Take(100).ToListAsync();
            pool = pool.Where(x => wrong.Contains(x.Question.Id)).ToList();
        }
        if (pool.Count == 0) return BadRequest(new { Error = "No questions are available for this practice mode." });
        var id = Guid.NewGuid(); var size = mode == "Mixed" ? configuration.GetValue("GrammarPractice:MixedSessionSize", 12) : configuration.GetValue("GrammarPractice:SessionSize", 10);
        var selected = pool.OrderBy(x => StableOrder(id, x.Question.Id)).Take(size).Select(x => x.Question.Id).ToArray();
        var selectedQuestions = pool.Where(x => selected.Contains(x.Question.Id)).ToDictionary(x => x.Question.Id, x => x.Question);
        var optionOrder = BuildOptionOrder(id, selected, selectedQuestions);
        var session = new GrammarPracticeSession { Id = id, UserId = user, GrammarTopicId = mode == "Topic" ? request.TopicId : null, LanguageCode = request.LanguageCode, CefrLevel = request.CefrLevel, Mode = mode, SelectedQuestionIdsJson = JsonSerializer.Serialize(selected), QuestionOptionOrderJson = JsonSerializer.Serialize(optionOrder) };
        db.GrammarPracticeSessions.Add(session); await db.SaveChangesAsync(); return Ok(await SessionResponse(session));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> Resume(Guid sessionId)
    {
        var session = await db.GrammarPracticeSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == CurrentUserId());
        return session is null ? NotFound() : Ok(await SessionResponse(session));
    }

    [HttpPost("sessions/{sessionId:guid}/answers")]
    public async Task<IActionResult> Answer(Guid sessionId, GrammarAnswerRequest request)
    {
        var session = await db.GrammarPracticeSessions.Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == CurrentUserId() && x.Status == "InProgress");
        if (session is null) return NotFound();
        var selected = Read<string[]>(session.SelectedQuestionIdsJson); if (!selected.Contains(request.QuestionId) || session.Answers.Any(x => x.QuestionId == request.QuestionId)) return BadRequest();
        var topics = await db.GrammarTopics.AsNoTracking().Where(x => x.IsPublished && x.LanguageCode == session.LanguageCode && x.CefrLevel == session.CefrLevel).ToListAsync();
        var found = topics.SelectMany(t => Read<GrammarExercise[]>(t.ExercisesJson).Select(q => (Topic:t, Question:q))).SingleOrDefault(x => x.Question.Id == request.QuestionId);
        if (found.Topic is null) return BadRequest();
        var correct = found.Question.AcceptedAnswers.Any(x => Normalize(x) == Normalize(request.Answer));
        var practiceAnswer = new GrammarPracticeAnswer { GrammarPracticeSessionId = session.Id, Session = session, GrammarTopicId = found.Topic.Id, QuestionId = request.QuestionId, LearnerAnswer = request.Answer.Trim(), CorrectAnswer = found.Question.AcceptedAnswers[0], IsCorrect = correct };
        db.GrammarPracticeAnswers.Add(practiceAnswer);
        session.CurrentQuestionIndex = Math.Min(selected.Length, session.Answers.Count); session.UpdatedAt = DateTime.UtcNow;
        if (session.Answers.Count == selected.Length) { session.Status = "Completed"; session.CompletedAt = DateTime.UtcNow; }
        var progress = await db.GrammarTopicProgress.SingleOrDefaultAsync(x => x.UserId == session.UserId && x.GrammarTopicId == found.Topic.Id);
        if (progress is null) { progress = new() { UserId = session.UserId, GrammarTopicId = found.Topic.Id }; db.GrammarTopicProgress.Add(progress); }
        progress.TotalAnswers++; if (correct) progress.CorrectAnswers++; progress.LastPracticedAt = DateTime.UtcNow;
        if (session.Status == "Completed") foreach (var topicId in session.Answers.Select(x => x.GrammarTopicId).Distinct()) { var p = topicId == found.Topic.Id ? progress : await db.GrammarTopicProgress.SingleAsync(x => x.UserId == session.UserId && x.GrammarTopicId == topicId); p.SessionsCompleted++; }
        await db.SaveChangesAsync(); return Ok(new { IsCorrect = correct, CorrectAnswer = found.Question.AcceptedAnswers[0], found.Question.ExplanationFa, Session = await SessionResponse(session) });
    }

    private async Task<object> SessionResponse(GrammarPracticeSession session)
    {
        var ids = Read<string[]>(session.SelectedQuestionIdsJson); var topics = await db.GrammarTopics.AsNoTracking().Where(x => x.IsPublished && x.LanguageCode == session.LanguageCode && x.CefrLevel == session.CefrLevel).ToListAsync();
        var storedOptionOrder = string.IsNullOrWhiteSpace(session.QuestionOptionOrderJson) ? new Dictionary<string,string[]>() : Read<Dictionary<string,string[]>>(session.QuestionOptionOrderJson);
        var questions = topics.SelectMany(t => Read<GrammarExercise[]>(t.ExercisesJson).Where(q => ids.Contains(q.Id)).Select(q => new { q.Id, TopicId = t.Id, t.TitleDe, t.TitleFa, q.Type, q.PromptFa, q.PromptDe, Options = storedOptionOrder.TryGetValue(q.Id, out var options) ? options : q.Options })).OrderBy(x => Array.IndexOf(ids, x.Id));
        return new { session.Id, session.Mode, session.Status, session.CurrentQuestionIndex, session.StartedAt, session.UpdatedAt, session.CompletedAt, Questions = questions, Answers = session.Answers.OrderBy(x => x.AnsweredAt).Select(x => new { x.QuestionId, x.LearnerAnswer, x.IsCorrect, x.AnsweredAt }) };
    }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    private static string Normalize(string x) => string.Join(' ', x.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string StableOrder(Guid seed, string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed + value)));
    private static Dictionary<string,string[]> BuildOptionOrder(Guid sessionId, string[] selected, Dictionary<string,GrammarExercise> questions)
    {
        var result = new Dictionary<string,string[]>(); var positionUse = new Dictionary<int,int>(); var positions = new List<int>();
        foreach (var questionId in selected)
        {
            var question = questions[questionId]; var options = question.Options.OrderBy(x => StableOrder(sessionId, $"{questionId}|{x}")).ToList();
            if (options.Count < 2) { result[questionId] = options.ToArray(); continue; }
            var correct = options.SingleOrDefault(question.AcceptedAnswers.Contains);
            if (correct is null) { result[questionId] = options.ToArray(); continue; }
            var candidates = Enumerable.Range(0, options.Count).OrderBy(x => positionUse.GetValueOrDefault(x)).ThenBy(x => StableOrder(sessionId, $"position|{questionId}|{x}")).ToArray();
            var target = candidates.FirstOrDefault(x => !WouldCreateCycle(positions, x), candidates[0]);
            options.Remove(correct); options.Insert(target, correct); positionUse[target] = positionUse.GetValueOrDefault(target) + 1; positions.Add(target); result[questionId] = options.ToArray();
        }
        return result;
    }
    private static bool WouldCreateCycle(List<int> positions, int candidate)
    {
        foreach (var size in new[] { 2, 3 })
        {
            var count = positions.Count + 1;
            if (count < size * 2) continue;
            var start = count - size * 2;
            var repeats = true;
            for (var offset = 0; offset < size; offset++)
            {
                var first = positions[start + offset];
                var secondIndex = start + size + offset;
                var second = secondIndex == positions.Count ? candidate : positions[secondIndex];
                if (first != second) { repeats = false; break; }
            }
            if (repeats) return true;
        }
        return false;
    }
}

public sealed record StartGrammarSession(string LanguageCode = "de", string CefrLevel = "A1", Guid? TopicId = null, string Mode = "Topic", bool ForceNew = false);
public sealed record GrammarAnswerRequest(string QuestionId, string Answer);
