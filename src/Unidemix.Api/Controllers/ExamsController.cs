using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Unidemix.Api.Models;
using Unidemix.Api.Services;
using Unidemix.Api.Data;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/exams")]
public sealed class ExamsController(AppDbContext db, ExamAiGenerationService generationService, ExamAiCapabilityPolicy capabilityPolicy) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Catalog([FromQuery] string languageCode, [FromQuery] string level)
    {
        var providers = await db.ExamProviders.AsNoTracking().Where(x => x.IsActive && x.LanguageCode == languageCode)
            .Select(x => new { x.Id, x.Code, x.Name, x.LanguageCode,
                Programs = x.Programs.Where(p => p.IsActive && p.IsPublished && p.LevelMappings.Any(m => m.CefrLevel == level)).Select(p => new {
                    p.Id, p.Code, p.Level, p.Name, p.IsPublished, p.WrittenDurationMinutes, p.SpeakingDurationMinutes, p.PreparationDurationMinutes,
                    CefrMappings = p.LevelMappings.OrderBy(m => m.CefrLevel).Select(m => new { m.CefrLevel, m.IsApproximate }),
                    Sections = p.Sections.OrderBy(s => s.Order).Select(s => new { s.Id, s.Code, s.Name, s.Order }) }) })
            .Where(x => x.Programs.Any()).OrderBy(x => x.Name).ToListAsync();
        return Ok(providers);
    }

    [HttpGet("{programId:guid}")]
    public async Task<IActionResult> Definition(Guid programId)
    {
        var p = await db.ExamPrograms.AsNoTracking().Include(x => x.Provider).SingleOrDefaultAsync(x => x.Id == programId && x.IsPublished);
        if (p is null) return NotFound();
        var blueprint = await db.ExamBlueprints.AsNoTracking().Where(x => x.ExamProgramId == p.Id).OrderByDescending(x => x.Version).FirstOrDefaultAsync();
        return Ok(new { p.Id, p.Code, p.Level, p.Name, Provider = p.Provider.Name, ProviderCode = p.Provider.Code, p.ContentVersion,
            p.WrittenDurationMinutes, p.SpeakingDurationMinutes, p.PreparationDurationMinutes,
            Timing = StoredTiming(p),
            Disclaimer = $"این تمرین Unidemix بر اساس ساختار آزمون {p.Name} است؛ سؤال رسمی، گواهی یا همکاری با {p.Provider.Name} نیست.",
            Blueprint = blueprint is null ? null : new { blueprint.Id, blueprint.Version, blueprint.Variant, blueprint.Status },
            Sections = CamelElement<ExamSectionSource[]>(p.BlueprintJson ?? "[]"), PracticeBank = CamelElement<ExamTaskSource[]>(p.PracticeBankJson ?? "[]"),
            MockVariants = RedactAnswers(p.MockVariantsJson ?? "[]"), IsFullMockComplete = ValidateStored(p),
            Scoring = p.ScoringJson is null ? (JsonElement?)null : CamelElement<ExamScoringSource>(p.ScoringJson) });
    }

    [HttpGet("{programId:guid}/ai-capabilities")]
    public async Task<IActionResult> AiCapabilities(Guid programId)
    {
        if (!await db.ExamPrograms.AnyAsync(x => x.Id == programId && x.IsPublished)) return NotFound();
        var userId = CurrentUserId();
        var plan = await db.UserSubscriptions.AsNoTracking().Include(x => x.Plan)
            .Where(x => x.UserId == userId && x.Status == "Active" && x.EndsAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.EndsAt).Select(x => x.Plan.Code).FirstOrDefaultAsync() ?? "free";
        return Ok(new { Plan = plan, Capabilities = capabilityPolicy.Resolve(plan) });
    }

    [HttpPost("{programId:guid}/ai-generation")]
    public async Task<IActionResult> Generate(Guid programId, ExamAiGenerationRequest request, CancellationToken cancellationToken)
    {
        var (content, error, statusCode) = await generationService.GenerateAsync(programId, CurrentUserId(), request, cancellationToken);
        if (content is null) return StatusCode(statusCode, new { Error = error });
        return StatusCode(statusCode, GeneratedResponse(content));
    }

    [HttpGet("ai-generation/{contentId:guid}")]
    public async Task<IActionResult> Generated(Guid contentId)
    {
        var content = await db.GeneratedExamContents.AsNoTracking().Include(x => x.Blueprint)
            .SingleOrDefaultAsync(x => x.Id == contentId && x.UserId == CurrentUserId());
        if (content is null) return NotFound();
        return Ok(new { content.Id, content.Status, content.Mode, content.SectionKey, content.PartKey, content.RejectionReason,
            Blueprint = new { content.Blueprint.ExamKey, content.Blueprint.ProviderCode, content.Blueprint.Variant, content.Blueprint.Version },
            content.CreatedAt, content.ReadyAt, Content = ReadyContent(content) });
    }

    [HttpPost("{programId:guid}/attempts")]
    public async Task<IActionResult> Start(Guid programId, StartExamRequest request)
    {
        var p = await db.ExamPrograms.SingleOrDefaultAsync(x => x.Id == programId && x.IsPublished);
        if (p is null) return NotFound();
        GeneratedExamContent? generated = null;
        if (request.GeneratedContentId is not null)
            generated = await db.GeneratedExamContents.SingleOrDefaultAsync(x => x.Id == request.GeneratedContentId && x.UserId == CurrentUserId() && x.Status == "Ready" && x.Mode == "FullMockExam" && x.Blueprint.ExamProgramId == programId);
        var variants = JsonSerializer.Deserialize<MockVariantSource[]>(p.MockVariantsJson ?? "[]")!;
        if (request.GeneratedContentId is not null && generated is null) return BadRequest("Generated mock is not Ready.");
        if (generated is null && (request.VariantKey is null || !variants.Any(x => x.Key == request.VariantKey))) return BadRequest();
        if (request.ExecutionMode is not ("Familiarization" or "TimedSimulation")) return BadRequest("Invalid execution mode.");
        var attempt = new MockExamAttempt { ExamProgramId = p.Id, UserId = CurrentUserId(), VariantKey = generated is null ? request.VariantKey! : $"ai:{generated.Id}", GeneratedExamContentId = generated?.Id,
            ExecutionMode = request.ExecutionMode, ExpiresAt = request.ExecutionMode == "TimedSimulation" ? DateTimeOffset.UtcNow.AddMinutes(p.WrittenDurationMinutes) : null };
        db.MockExamAttempts.Add(attempt); await db.SaveChangesAsync();
        return Ok(new { attempt.Id, attempt.Status, attempt.ExecutionMode, attempt.StartedAt, attempt.ExpiresAt });
    }

    [HttpPut("attempts/{attemptId:guid}")]
    public async Task<IActionResult> Save(Guid attemptId, SaveExamRequest request)
    {
        var attempt = await db.MockExamAttempts.SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId() && x.Status == "InProgress");
        if (attempt is null) return NotFound();
        if (attempt.ExpiresAt is not null && attempt.ExpiresAt <= DateTimeOffset.UtcNow) return Conflict(new { Error = "Attempt expired." });
        var stored = JsonSerializer.Deserialize<Dictionary<string,string>>(attempt.AnswersJson) ?? [];
        foreach (var answer in request.Answers) stored[answer.Key] = answer.Value;
        attempt.AnswersJson = JsonSerializer.Serialize(stored);
        attempt.CurrentItemIndex = Math.Max(0, request.CurrentItemIndex);
        await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("attempts/{attemptId:guid}/playback/{taskKey}")]
    public async Task<IActionResult> ConsumePlayback(Guid attemptId, string taskKey)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
            : null;
        var attempt = await db.MockExamAttempts.Include(x => x.Program)
            .SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId() && x.Status == "InProgress");
        if (attempt is null) return NotFound();
        if (attempt.ExpiresAt is not null && attempt.ExpiresAt <= DateTimeOffset.UtcNow) return Conflict(new { Error = "Attempt expired." });
        var variant = await ResolveVariant(attempt);
        var task = variant.Tasks.SingleOrDefault(x => x.Key == taskKey && x.Section == "listening");
        if (task is null || string.IsNullOrWhiteSpace(task.Script) || task.PlaybackCount is null or <= 0)
            return Conflict(new { Error = "Listening audio is not ready." });

        var state = JsonSerializer.Deserialize<Dictionary<string, string>>(attempt.AnswersJson) ?? [];
        var playbackKey = $"__playback:{task.Key}";
        var used = state.TryGetValue(playbackKey, out var stored) && int.TryParse(stored, out var parsed) ? parsed : 0;
        if (used >= task.PlaybackCount.Value)
            return Conflict(new { Error = "Playback allowance exhausted.", Used = used, Maximum = task.PlaybackCount.Value });
        used++;
        state[playbackKey] = used.ToString(System.Globalization.CultureInfo.InvariantCulture);
        attempt.AnswersJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return Ok(new { Script = task.Script, Language = "de-DE", Used = used, Maximum = task.PlaybackCount.Value });
    }

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid attemptId, SaveExamRequest request)
    {
        var attempt = await db.MockExamAttempts.Include(x => x.Program).SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId() && x.Status == "InProgress");
        if (attempt is null) return NotFound();
        var expired = attempt.ExpiresAt is not null && attempt.ExpiresAt <= DateTimeOffset.UtcNow;
        var submittedAnswers = expired ? JsonSerializer.Deserialize<Dictionary<string, string>>(attempt.AnswersJson)! : request.Answers;
        var variant = await ResolveVariant(attempt);
        var scored = variant.Tasks.Where(x => !x.RequiresEvaluation && x.CorrectAnswer is not null).ToArray();
        var earned = scored.Sum(x => submittedAnswers.TryGetValue(x.Key, out var answer) && answer == x.CorrectAnswer ? x.Points : 0);
        var pending = variant.Tasks.Where(x => x.RequiresEvaluation).Select(x => x.Section).Distinct().ToArray();
        var result = new { AutomaticallyScoredPoints = earned, AutomaticallyScoredMaximum = scored.Sum(x => x.Points), NeedsEvaluation = pending,
            Status = pending.Length > 0 ? "Provisional" : "Complete", Label = "نتیجه تمرینی Unidemix؛ نتیجه یا گواهی رسمی نیست" };
        attempt.AnswersJson = JsonSerializer.Serialize(submittedAnswers); attempt.ResultJson = JsonSerializer.Serialize(result); attempt.Status = expired ? "Expired" : "Submitted"; attempt.SubmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); return Ok(new { attempt.Id, Result = result, Review = variant.Tasks });
    }

    [HttpGet("{programId:guid}/attempts")]
    public async Task<IActionResult> History(Guid programId) => Ok(await db.MockExamAttempts.AsNoTracking()
        .Where(x => x.ExamProgramId == programId && x.UserId == CurrentUserId()).OrderByDescending(x => x.StartedAt)
        .Select(x => new { x.Id, x.VariantKey, x.ExecutionMode, x.Status, x.CurrentItemIndex, x.ExpiresAt, x.StartedAt, x.SubmittedAt, Result = x.ResultJson }).Take(20).ToListAsync());

    [HttpPost("{programId:guid}/practice-sessions")]
    public async Task<IActionResult> StartPractice(Guid programId, StartPracticeRequest request)
    {
        var program = await db.ExamPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId && x.IsPublished);
        if (program is null) return NotFound();
        var tasks = JsonSerializer.Deserialize<ExamTaskSource[]>(program.PracticeBankJson ?? "[]")!;
        if (!tasks.Any(x => x.Section == request.SectionKey && x.Part == request.PartKey)) return BadRequest();
        var session = await db.ExamPracticeSessions.SingleOrDefaultAsync(x => x.UserId == CurrentUserId() && x.ExamProgramId == programId && x.SectionKey == request.SectionKey && x.PartKey == request.PartKey && x.Status == "InProgress");
        if (session is null) { session = new() { UserId = CurrentUserId(), ExamProgramId = programId, SectionKey = request.SectionKey, PartKey = request.PartKey }; db.ExamPracticeSessions.Add(session); await db.SaveChangesAsync(); }
        return Ok(PracticeResponse(session));
    }

    [HttpPut("practice-sessions/{sessionId:guid}")]
    public async Task<IActionResult> SavePractice(Guid sessionId, SavePracticeRequest request)
    {
        var session = await db.ExamPracticeSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == CurrentUserId());
        if (session is null) return NotFound();
        session.CurrentItemIndex = request.CurrentItemIndex; session.AnswersJson = JsonSerializer.Serialize(request.Answers);
        session.SubmittedItemsJson = JsonSerializer.Serialize(request.SubmittedItems); session.Status = request.Completed ? "Completed" : "InProgress";
        session.CompletedAt = request.Completed ? DateTimeOffset.UtcNow : null; session.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        return Ok(PracticeResponse(session));
    }

    private static object PracticeResponse(ExamPracticeSession x) => new { x.Id, x.ExamProgramId, x.SectionKey, x.PartKey, x.CurrentItemIndex,
        Answers = JsonSerializer.Deserialize<JsonElement>(x.AnswersJson), SubmittedItems = JsonSerializer.Deserialize<JsonElement>(x.SubmittedItemsJson), x.Status, x.UpdatedAt };

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<IActionResult> Attempt(Guid attemptId)
    {
        var a = await db.MockExamAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId());
        return a is null ? NotFound() : Ok(new { a.Id, a.ExamProgramId, a.VariantKey, a.GeneratedExamContentId, a.Status,
            Answers = PublicAnswers(a.AnswersJson), PlaybackCounts = PlaybackCounts(a.AnswersJson), a.CurrentItemIndex, Result = a.ResultJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(a.ResultJson), a.ExecutionMode, a.ExpiresAt, a.StartedAt, a.SubmittedAt });
    }

    private async Task<MockVariantSource> ResolveVariant(MockExamAttempt attempt)
    {
        if (attempt.GeneratedExamContentId is null)
            return JsonSerializer.Deserialize<MockVariantSource[]>(attempt.Program.MockVariantsJson ?? "[]")!.Single(x => x.Key == attempt.VariantKey);
        var content = await db.GeneratedExamContents.AsNoTracking().SingleAsync(x => x.Id == attempt.GeneratedExamContentId && x.Status == "Ready");
        var envelope = JsonSerializer.Deserialize<GeneratedExamEnvelope>(content.ContentJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return new(attempt.VariantKey, "AI Practice Mock", envelope.Tasks.Select(t => new ExamTaskSource(t.Key,t.Section,t.Part,t.Type,t.Prompt,t.SourceText,t.Script,t.SpeakerMetadata,t.DurationTargetSeconds,t.PlaybackCount,t.AudioStatus,t.CorrectAnswer,t.Options,t.Points,t.RequiresEvaluation)).ToArray());
    }

    private static object GeneratedResponse(GeneratedExamContent content) => new { content.Id, content.Status, content.Mode, content.SectionKey, content.PartKey,
        content.RejectionReason, content.CreatedAt, content.ReadyAt, Content = ReadyContent(content) };
    private static JsonElement? ReadyContent(GeneratedExamContent content) => content.Status == "Ready" ? JsonSerializer.Deserialize<JsonElement>(content.ContentJson!) : null;
    private static JsonElement RedactAnswers(string json) { var variants = JsonSerializer.Deserialize<MockVariantSource[]>(json)!; return JsonSerializer.SerializeToElement(variants.Select(v => new { v.Key,v.Name,Tasks=v.Tasks.Select(t=>new {t.Key,t.Section,t.Part,t.Type,t.Prompt,t.SourceText,Script=t.Section=="listening"?null:t.Script,t.SpeakerMetadata,t.DurationTargetSeconds,t.PlaybackCount,t.AudioStatus,t.Options,t.Points,t.RequiresEvaluation}) }),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}); }
    private static Dictionary<string,string> PublicAnswers(string json) => (JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? [])
        .Where(x => !x.Key.StartsWith("__playback:", StringComparison.Ordinal)).ToDictionary();
    private static Dictionary<string,int> PlaybackCounts(string json) => (JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? [])
        .Where(x => x.Key.StartsWith("__playback:", StringComparison.Ordinal) && int.TryParse(x.Value, out _))
        .ToDictionary(x => x.Key["__playback:".Length..], x => int.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture));
    private static bool ValidateStored(ExamProgram program)
    {
        var package = new TelcExamPackage("telc", "de", program.Level, program.Name, program.ContentVersion, program.SourceReference ?? "stored",
            StoredTiming(program),
            JsonSerializer.Deserialize<ExamSectionSource[]>(program.BlueprintJson ?? "[]")!, [],
            JsonSerializer.Deserialize<MockVariantSource[]>(program.MockVariantsJson ?? "[]")!,
            JsonSerializer.Deserialize<ExamScoringSource>(program.ScoringJson ?? "{}")!);
        return TelcExamPackageImporter.ValidateFullMocks(package).IsFullMockComplete;
    }
    private static ExamTimingSource StoredTiming(ExamProgram program)
        => string.IsNullOrWhiteSpace(program.TimingJson)
            ? new(program.WrittenDurationMinutes, program.SpeakingDurationMinutes, program.PreparationDurationMinutes,
                SessionDurationMinutes: program.WrittenDurationMinutes)
            : JsonSerializer.Deserialize<ExamTimingSource>(program.TimingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    private static JsonElement CamelElement<T>(string json) => JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<T>(json)!, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record StartExamRequest(string? VariantKey, Guid? GeneratedContentId = null, string ExecutionMode = "TimedSimulation");
public sealed record SaveExamRequest(Dictionary<string, string> Answers, int CurrentItemIndex = 0);
public sealed record StartPracticeRequest(string SectionKey, string PartKey);
public sealed record SavePracticeRequest(int CurrentItemIndex, Dictionary<string, string> Answers, string[] SubmittedItems, bool Completed = false);
