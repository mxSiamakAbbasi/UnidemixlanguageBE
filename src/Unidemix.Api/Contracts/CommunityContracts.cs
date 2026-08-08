using System.ComponentModel.DataAnnotations;

namespace Unidemix.Api.Contracts;

public sealed record CreatePostRequest(
    [Required, MaxLength(500)] string ImageStorageKey,
    [MaxLength(2200)] string Caption);

public sealed record ImageUploadResponse(string ImageUrl, string StorageKey, string? ThumbnailUrl);

public sealed record PostAuthorResponse(Guid UserId, string DisplayName, string? ProfilePhotoUrl,
    string NativeLanguageCode, string LearningLanguageCode, string CefrLevel, string MentionKey);
public sealed record PostResponse(Guid Id, PostAuthorResponse Author, string ImageUrl, string Caption,
    string? Context, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int LikeCount, int CommentCount, bool IsLikedByMe, bool IsMine);
public sealed record PostDetailResponse(PostResponse Post, IReadOnlyList<CommentResponse> Comments);
public sealed record CreateCommentRequest([Required, MinLength(1), MaxLength(1000)] string Text);
public sealed record CommentResponse(Guid Id, Guid PostId, PostAuthorResponse Author, string Text,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsMine);
public sealed record MentionSuggestionResponse(Guid UserId, string DisplayName, string? ProfilePhotoUrl, string MentionKey);
public sealed record CreateContentReportRequest(
    [Required] string TargetType,
    Guid TargetId,
    [Required, MaxLength(40)] string Reason,
    [MaxLength(1000)] string? Details);
public sealed record ContentReportResponse(Guid Id, string TargetType, Guid TargetId, string Reason, string Status, DateTimeOffset CreatedAt);
