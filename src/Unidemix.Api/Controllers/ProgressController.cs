using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/progress")]
public sealed class ProgressController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> SectionCodes = ["vocabulary", "listening", "speaking", "reading", "writing", "grammar", "final-practice"];
    [HttpPut("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Save(Guid lessonId, ProgressRequest request)
    {
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId)) return NotFound();
        var userId = CurrentUserId();
        var progress = await db.LessonProgress.SingleOrDefaultAsync(x => x.UserId == userId && x.LessonId == lessonId);
        if (progress is null)
        {
            progress = new LessonProgress { UserId = userId, LessonId = lessonId };
            db.LessonProgress.Add(progress);
        }
        progress.Percent = Math.Max(progress.Percent, request.Percent);
        progress.BestScore = Math.Max(progress.BestScore, request.Score);
        progress.IsCompleted = progress.Percent == 100;
        progress.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { progress.LessonId, progress.Percent, progress.BestScore, progress.IsCompleted, progress.UpdatedAt });
    }

    [HttpPut("lessons/{lessonId:guid}/sections/{sectionCode}")]
    public async Task<IActionResult> SaveSection(Guid lessonId, string sectionCode, SectionProgressRequest request)
    {
        sectionCode = sectionCode.ToLowerInvariant();
        if (!SectionCodes.Contains(sectionCode)) return BadRequest(new { title = "Unknown lesson section" });
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId)) return NotFound();
        var userId = CurrentUserId();
        var section = await db.LessonSectionProgress.SingleOrDefaultAsync(x => x.UserId == userId && x.LessonId == lessonId && x.SectionCode == sectionCode);
        if (section is null)
        {
            section = new LessonSectionProgress { UserId = userId, LessonId = lessonId, SectionCode = sectionCode };
            db.LessonSectionProgress.Add(section);
        }
        section.Percent = Math.Clamp(request.Percent, 0, 100);
        section.IsCompleted = section.Percent == 100;
        section.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var completedSections = await db.LessonSectionProgress.CountAsync(x => x.UserId == userId && x.LessonId == lessonId && x.IsCompleted);
        var lessonProgress = await db.LessonProgress.SingleOrDefaultAsync(x => x.UserId == userId && x.LessonId == lessonId);
        if (lessonProgress is null)
        {
            lessonProgress = new LessonProgress { UserId = userId, LessonId = lessonId };
            db.LessonProgress.Add(lessonProgress);
        }
        lessonProgress.Percent = Math.Max(lessonProgress.Percent, (int)Math.Round(completedSections / (double)SectionCodes.Count * 100));
        lessonProgress.IsCompleted = lessonProgress.IsCompleted || completedSections == SectionCodes.Count;
        lessonProgress.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { section.SectionCode, section.Percent, section.IsCompleted, section.UpdatedAt, CompletedSections = completedSections, TotalSections = SectionCodes.Count });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var userId = CurrentUserId();
        var items = await db.LessonProgress.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        return Ok(new
        {
            StartedLessons = items.Count,
            CompletedLessons = items.Count(x => x.IsCompleted),
            AverageScore = items.Count == 0 ? 0 : Math.Round(items.Average(x => x.BestScore), 1),
            TotalXp = await db.LessonProgress.Where(x => x.UserId == userId && x.IsCompleted).SumAsync(x => x.Lesson.XpReward)
        });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record SectionProgressRequest(int Percent);
