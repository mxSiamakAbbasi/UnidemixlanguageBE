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
        var lesson = await db.Lessons.AsNoTracking().Include(x => x.Exercises).SingleOrDefaultAsync(x => x.Id == lessonId);
        if (lesson is null) return NotFound();
        var order = lesson.SectionOrderJson is null
            ? new[] { "vocabulary", "listening", "speaking", "reading", "writing", "grammar", "final-practice" }
            : JsonSerializer.Deserialize<string[]>(lesson.SectionOrderJson) ?? [];
        var sectionProgress = await db.LessonSectionProgress.AsNoTracking().Where(x => x.UserId == userId && x.LessonId == lessonId)
            .ToDictionaryAsync(x => x.SectionCode);
        return Ok(new
        {
            lesson.Id, lesson.CourseId, lesson.Title, lesson.Description, lesson.Category, lesson.SectionTitle, lesson.DurationMinutes, lesson.XpReward, lesson.Icon,
            Sections = order.Select((code, index) => new
            {
                Code = code, Order = index + 1,
                Percent = sectionProgress.TryGetValue(code, out var progress) ? progress.Percent : 0,
                IsCompleted = sectionProgress.TryGetValue(code, out var completed) && completed.IsCompleted
            }),
            Exercises = lesson.Exercises.OrderBy(e => e.Order).Select(e => new
            {
                e.Id, e.Type, e.Kind, e.SectionCode, e.Prompt, e.CorrectAnswer,
                Options = e.OptionsJson == null ? null : JsonSerializer.Deserialize<string[]>(e.OptionsJson),
                e.Explanation, e.Order
            })
        });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
