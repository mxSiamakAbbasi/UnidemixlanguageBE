using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;
using Unidemix.Api.Models;
using Unidemix.Api.Services;

namespace Unidemix.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email))
            return Conflict(new ProblemDetails { Title = "Email already exists", Status = StatusCodes.Status409Conflict });
        var user = new User { Email = email, DisplayName = request.DisplayName.Trim(), PasswordHash = "" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Register), CreateResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null || new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password", Status = StatusCodes.Status401Unauthorized });
        return Ok(CreateResponse(user));
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, user.ToResponse());
    }
}

internal static class UserMapping
{
    public static UserResponse ToResponse(this User x) => new(x.Id, x.Email, x.DisplayName, x.NativeLanguage,
        x.LearningLanguage, x.Level, x.Goal, x.DailyGoalMinutes, x.Role);
}
