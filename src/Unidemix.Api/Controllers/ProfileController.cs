using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/profile")]
public sealed class ProfileController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Goals = ["migration", "work", "study", "travel", "personal-interest", "exam", "daily-life", "other"];
    private static readonly HashSet<string> MigrationSubtypes = ["work-migration", "university-study", "ausbildung", "family-reunification", "undecided"];
    [HttpGet]
    public async Task<ActionResult<UserResponse>> Get()
    {
        var user = await CurrentUserAsync();
        return user is null ? NotFound() : Ok(user.ToResponse());
    }

    [HttpPut]
    public async Task<ActionResult<UserResponse>> Update(UpdateProfileRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        user.DisplayName = request.DisplayName.Trim();
        user.NativeLanguage = request.NativeLanguage.ToLowerInvariant();
        user.LearningLanguage = request.LearningLanguage.ToLowerInvariant();
        user.Level = request.Level;
        var goal = request.Goal.Trim().ToLowerInvariant() switch
        {
            "immigration" => "migration",
            "family" => "daily-life",
            "university" => "study",
            var value => value
        };
        if (!Goals.Contains(goal)) return ValidationProblem("Learning goal is not supported.");
        var subtype = string.IsNullOrWhiteSpace(request.GoalSubtype) ? null : request.GoalSubtype.Trim().ToLowerInvariant();
        if (goal == "migration" && subtype is not null && !MigrationSubtypes.Contains(subtype))
            return ValidationProblem("Learning goal subtype is not supported.");
        user.Goal = goal;
        user.GoalSubtype = goal == "migration" ? subtype : null;
        user.TargetLevel = request.TargetLevel;
        user.ExamGoal = goal == "exam" && !string.IsNullOrWhiteSpace(request.ExamGoal) ? request.ExamGoal.Trim() : null;
        if (request.LearningOnboardingCompleted.HasValue) user.LearningOnboardingCompleted = request.LearningOnboardingCompleted.Value;
        if (request.LearningPathGuideDismissed.HasValue) user.LearningPathGuideDismissed = request.LearningPathGuideDismissed.Value;
        user.DailyGoalMinutes = request.DailyGoalMinutes;
        await db.SaveChangesAsync();
        return Ok(user.ToResponse());
    }

    private Task<Models.User?> CurrentUserAsync() =>
        db.Users.SingleOrDefaultAsync(x => x.Id == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
}
