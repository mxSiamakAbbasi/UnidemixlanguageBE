using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/vocabulary")]
public sealed class VocabularyController(AppDbContext db) : ControllerBase
{
    [HttpGet("review/summary")]
    public async Task<IActionResult> Summary()
    {
        var (userId, language, level) = await CurrentLearningContext();
        var now = DateTimeOffset.UtcNow;
        var dueCount = await LearningItems(language, level)
            .CountAsync(x => !x.Reviews.Any(r => r.UserId == userId) || x.Reviews.Any(r => r.UserId == userId && r.NextReviewAt <= now));
        return Ok(new { DueCount = dueCount });
    }

    [HttpGet("review/today")]
    public async Task<IActionResult> Today([FromQuery] int limit = 20)
    {
        var (userId, language, level) = await CurrentLearningContext();
        var now = DateTimeOffset.UtcNow;
        var items = await LearningItems(language, level)
            .Where(x => !x.Reviews.Any(r => r.UserId == userId) || x.Reviews.Any(r => r.UserId == userId && r.NextReviewAt <= now))
            .OrderBy(x => x.Reviews.Where(r => r.UserId == userId).Select(r => r.NextReviewAt).FirstOrDefault())
            .ThenBy(x => x.Lesson.Order).ThenBy(x => x.Order).Take(Math.Clamp(limit, 1, 50))
            .Select(x => new { x.Id, x.Term, x.ContentType, x.Pronunciation, x.Meaning, x.Example, x.ExampleTranslation, x.Note, LessonTitle = x.Lesson.Title })
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> LessonVocabulary(Guid lessonId)
    {
        var items = await db.VocabularyItems.AsNoTracking().Where(x => x.LessonId == lessonId).OrderBy(x => x.Order)
            .Select(x => new { x.Id, x.Term, x.ContentType, x.Pronunciation, x.Meaning, x.Example, x.ExampleTranslation, x.Note }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("{itemId:guid}/review")]
    public async Task<IActionResult> Review(Guid itemId, ReviewVocabularyRequest request)
    {
        var userId = CurrentUserId();
        if (!await db.VocabularyItems.AnyAsync(x => x.Id == itemId)) return NotFound();
        var review = await db.VocabularyReviews.SingleOrDefaultAsync(x => x.UserId == userId && x.VocabularyItemId == itemId);
        if (review is null)
        {
            review = new VocabularyReview { UserId = userId, VocabularyItemId = itemId };
            db.VocabularyReviews.Add(review);
        }

        var now = DateTimeOffset.UtcNow;
        review.LastReviewedAt = now;
        if (request.Known)
        {
            review.SuccessfulReviews += 1;
            review.IntervalDays = review.SuccessfulReviews switch { 1 => 2, 2 => 4, _ => Math.Min(60, Math.Max(4, review.IntervalDays * 2)) };
            review.NextReviewAt = now.AddDays(review.IntervalDays);
        }
        else
        {
            review.SuccessfulReviews = 0;
            review.FailedReviews += 1;
            review.IntervalDays = 0;
            review.NextReviewAt = now.AddHours(6);
        }

        await db.SaveChangesAsync();
        return Ok(new { review.NextReviewAt });
    }

    private IQueryable<VocabularyItem> LearningItems(string language, string level) => db.VocabularyItems.AsNoTracking()
        .Where(x => x.Lesson.Course.LanguageCode == language && x.Lesson.Course.Level == level);

    private async Task<(Guid UserId, string Language, string Level)> CurrentLearningContext()
    {
        var userId = CurrentUserId();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
        return (userId, user.LearningLanguage, user.Level);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record ReviewVocabularyRequest(bool Known);
