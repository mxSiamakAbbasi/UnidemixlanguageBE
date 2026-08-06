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
        user.Goal = request.Goal.Trim();
        user.DailyGoalMinutes = request.DailyGoalMinutes;
        await db.SaveChangesAsync();
        return Ok(user.ToResponse());
    }

    private Task<Models.User?> CurrentUserAsync() =>
        db.Users.SingleOrDefaultAsync(x => x.Id == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
}
