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
            x.Id, x.Slug, x.Title, x.Description, x.LanguageCode, x.Level,
            LessonCount = x.Lessons.Count,
            CompletedLessons = x.Lessons.Count(l => l.Progress.Any(p => p.UserId == userId && p.IsCompleted))
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
                x.Id, x.Title, x.Description, x.Category, x.Order, x.DurationMinutes, x.XpReward, x.Icon,
                Progress = x.Progress.Where(p => p.UserId == userId).Select(p => p.Percent).FirstOrDefault(),
                IsCompleted = x.Progress.Any(p => p.UserId == userId && p.IsCompleted)
            }).ToListAsync();
        return Ok(lessons);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> GetLesson(Guid lessonId)
    {
        var lesson = await db.Lessons.AsNoTracking().Include(x => x.Exercises).SingleOrDefaultAsync(x => x.Id == lessonId);
        if (lesson is null) return NotFound();
        return Ok(new
        {
            lesson.Id, lesson.CourseId, lesson.Title, lesson.Description, lesson.Category, lesson.DurationMinutes, lesson.XpReward, lesson.Icon,
            Exercises = lesson.Exercises.OrderBy(e => e.Order).Select(e => new
            {
                e.Id, e.Type, e.Prompt,
                Options = e.OptionsJson == null ? null : JsonSerializer.Deserialize<string[]>(e.OptionsJson),
                e.Explanation, e.Order
            })
        });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
