using System.ComponentModel.DataAnnotations;

namespace Unidemix.Api.Contracts;

public sealed record UpdateSocialProfileRequest(
    [Url, MaxLength(500)] string? ProfilePhotoUrl,
    [MaxLength(500)] string Bio,
    [MaxLength(120)] string PracticeGoal,
    [MaxLength(120)] string Availability,
    [MaxLength(100)] string? City,
    Guid? CityId,
    bool LookingForPartner,
    [MaxLength(100)] string? DisplayName = null,
    [MaxLength(10)] string? LearningLanguageCode = null,
    [MaxLength(3)] string? CefrLevel = null,
    [RegularExpression("Public|Private")] string Privacy = "Public");

public sealed record SocialProfileResponse(
    Guid UserId, string Username, string DisplayName, string? ProfilePhotoUrl, string Bio,
    string NativeLanguageCode, string LearningLanguageCode, string CefrLevel,
    string LearningGoal, string PracticeGoal, string Availability, string? City, Guid? CityId,
    bool LookingForPartner, int PostsCount, int FollowersCount, int FollowingCount,
    bool IsFollowing, bool IsBlocked, string Privacy, string FollowState);

public sealed record PartnerSummaryResponse(
    Guid UserId, string Username, string DisplayName, string? ProfilePhotoUrl,
    string NativeLanguageCode, string LearningLanguageCode, string CefrLevel,
    string LearningGoal, string PracticeGoal, string Availability, string? City, Guid? CityId,
    string MatchReason, bool IsFollowing, string Privacy, string FollowState);

public sealed record FollowActionResponse(string State);
public sealed record FollowRequestResponse(Guid Id, Guid RequesterId, string DisplayName,
    string? ProfilePhotoUrl, string NativeLanguageCode, string LearningLanguageCode,
    string CefrLevel, DateTimeOffset CreatedAt);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CreateUserReportRequest(
    Guid ReportedUserId,
    [Required, MaxLength(40)] string Reason,
    [MaxLength(1000)] string? Details);

public sealed record UserReportResponse(Guid Id, Guid ReportedUserId, string Reason, string Status, DateTimeOffset CreatedAt);

public sealed record CityResponse(Guid Id, string CountryCode, string PersianName, string EnglishName);
