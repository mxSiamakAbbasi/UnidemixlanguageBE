using System.ComponentModel.DataAnnotations;

namespace Unidemix.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(80)] string DisplayName,
    [MaxLength(10)] string LearningLanguage = "de");
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
public sealed record UserResponse(Guid Id, string Email, string DisplayName, string NativeLanguage,
    string LearningLanguage, string Level, string Goal, int DailyGoalMinutes, string Role);
public sealed record UpdateProfileRequest(
    [Required, MaxLength(80)] string DisplayName,
    [Required, MaxLength(10)] string NativeLanguage,
    [Required, MaxLength(10)] string LearningLanguage,
    [RegularExpression("^(A1|A2|B1|B2|C1|C2)$")] string Level,
    [Required, MaxLength(50)] string Goal,
    [Range(5, 180)] int DailyGoalMinutes);
public sealed record ProgressRequest([Range(0, 100)] int Percent, [Range(0, 100)] int Score);
