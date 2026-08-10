using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/courses")]
public sealed class CoursesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        var userId = CurrentUserId();
        var courses = await db.Courses.AsNoTracking().OrderBy(x => x.Level).Select(x => new
        {
            x.Id, x.Slug, x.Title, x.Description, x.LanguageCode, x.Level, x.Kind, x.PathCode,
            LessonCount = x.Lessons.Count,
            CompletedLessons = x.Lessons.Count(l => l.Progress.Any(p => p.UserId == userId && p.IsCompleted)),
            Lessons = x.Lessons.OrderBy(l => l.Order).Select(l => new
            {
                l.Id, l.Title, l.Description, l.Category, l.SectionTitle, l.Order, l.DurationMinutes, l.XpReward, l.Icon,
                Progress = l.Progress.Where(p => p.UserId == userId).Select(p => p.Percent).FirstOrDefault(),
                IsCompleted = l.Progress.Any(p => p.UserId == userId && p.IsCompleted)
            })
        }).ToListAsync();
        return Ok(courses);
    }

    [HttpGet("{courseId:guid}/lessons")]
    public async Task<IActionResult> GetLessons(Guid courseId)
    {
        var userId = CurrentUserId();
        var lessons = await db.Lessons.AsNoTracking().Where(x => x.CourseId == courseId).OrderBy(x => x.Order)
            .Select(x => new
            {
                x.Id, x.Title, x.Description, x.Category, x.SectionTitle, x.Order, x.DurationMinutes, x.XpReward, x.Icon,
                Progress = x.Progress.Where(p => p.UserId == userId).Select(p => p.Percent).FirstOrDefault(),
                IsCompleted = x.Progress.Any(p => p.UserId == userId && p.IsCompleted)
            }).ToListAsync();
        return Ok(lessons);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> GetLesson(Guid lessonId)
    {
        var userId = CurrentUserId();
        var lesson = await db.Lessons.AsNoTracking().Include(x => x.Course).Include(x => x.Exercises).SingleOrDefaultAsync(x => x.Id == lessonId);
        if (lesson is null) return NotFound();
        var vocabulary = await db.VocabularyItems.AsNoTracking().Where(x => x.LessonId == lessonId).OrderBy(x => x.Order).ToListAsync();
        var order = lesson.SectionOrderJson is null
            ? new[] { "vocabulary", "listening", "speaking", "reading", "writing", "grammar", "final-practice" }
            : JsonSerializer.Deserialize<string[]>(lesson.SectionOrderJson) ?? [];
        var sectionProgress = await db.LessonSectionProgress.AsNoTracking().Where(x => x.UserId == userId && x.LessonId == lessonId)
            .ToDictionaryAsync(x => x.SectionCode);
        return Ok(new
        {
            lesson.Id, lesson.CourseId, lesson.Title, lesson.Description, lesson.Category, lesson.SectionTitle, lesson.DurationMinutes, lesson.XpReward, lesson.Icon,
            Audio = new
            {
                Status = lesson.AudioStatus,
                AssetUrl = lesson.AudioAssetUrl,
                ScriptExerciseId = lesson.AudioScriptExerciseOrder is null ? (Guid?)null : lesson.Exercises.FirstOrDefault(x => x.Order == lesson.AudioScriptExerciseOrder)?.Id,
                IsProductionReady = lesson.AudioStatus == "ready" && !string.IsNullOrWhiteSpace(lesson.AudioAssetUrl)
            },
            QuestionBankCount = lesson.Exercises.Count(x => x.IsQuestionBankItem),
            LearningContext = new
            {
                Language = lesson.Course.LanguageCode,
                Cefr = lesson.Course.Level,
                Course = lesson.Course.Title,
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                CanDoObjectives = lesson.CanDoObjectivesJson is null ? [] : JsonSerializer.Deserialize<string[]>(lesson.CanDoObjectivesJson) ?? [],
                KnownVocabulary = vocabulary.Where(x => !string.Equals(x.ContentType, "Chunk", StringComparison.OrdinalIgnoreCase)).Select(x => x.Term),
                KnownChunks = vocabulary.Where(x => string.Equals(x.ContentType, "Chunk", StringComparison.OrdinalIgnoreCase)).Select(x => x.Term),
                KnownGrammar = lesson.Exercises.Where(x => !x.IsQuestionBankItem && x.SectionCode == "grammar" && x.Kind == "Instruction").OrderBy(x => x.Order).Select(x => x.Prompt),
                DeferredGrammar = lesson.DeferredGrammarJson is null ? [] : JsonSerializer.Deserialize<string[]>(lesson.DeferredGrammarJson) ?? []
            },
            Sections = order.Select((code, index) => new
            {
                Code = code, Order = index + 1,
                Percent = sectionProgress.TryGetValue(code, out var progress) ? progress.Percent : 0,
                IsCompleted = sectionProgress.TryGetValue(code, out var completed) && completed.IsCompleted
            }),
            Exercises = lesson.Exercises.Where(e => !e.IsQuestionBankItem).OrderBy(e => e.Order).Select(e => new
            {
                e.Id, e.Type, e.Kind, e.SectionCode, e.Prompt, e.CorrectAnswer,
                Options = e.OptionsJson == null ? null : JsonSerializer.Deserialize<string[]>(e.OptionsJson),
                e.Explanation, e.Order
            })
        });
    }

    [HttpGet("lessons/{lessonId:guid}/practice-session")]
    public async Task<IActionResult> GetPracticeSession(Guid lessonId, [FromQuery] int size = 5, [FromQuery] int attempt = 0)
    {
        if (size is < 3 or > 20 || attempt < 0) return BadRequest();
        if (!await db.Lessons.AsNoTracking().AnyAsync(x => x.Id == lessonId)) return NotFound();
        var bank = await db.Exercises.AsNoTracking()
            .Where(x => x.LessonId == lessonId && x.IsQuestionBankItem)
            .OrderBy(x => x.Order)
            .ToListAsync();
        if (bank.Count == 0) return Ok(new { BankCount = 0, Attempt = attempt, Items = Array.Empty<object>() });

        var take = Math.Min(size, bank.Count);
        var start = bank.Count > take ? (attempt * take) % bank.Count : 0;
        var selected = Enumerable.Range(0, take).Select(index => bank[(start + index) % bank.Count]).ToList();
        return Ok(new
        {
            BankCount = bank.Count,
            Attempt = attempt,
            Items = selected.Select(e => new
            {
                e.Id, e.Type, e.Kind, e.SectionCode, e.QuestionSkill, e.Prompt, e.CorrectAnswer,
                Options = e.OptionsJson == null ? null : JsonSerializer.Deserialize<string[]>(e.OptionsJson),
                e.Explanation, e.Order
            })
        });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
