namespace Unidemix.Api.Models;

public sealed class SocialProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Username { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? ProfilePhotoStorageKey { get; set; }
    public string Bio { get; set; } = "";
    public string PracticeGoal { get; set; } = "";
    public string Availability { get; set; } = "";
    public string? City { get; set; }
    public Guid? CityId { get; set; }
    public City? CityReference { get; set; }
    public bool LookingForPartner { get; set; }
    public string Privacy { get; set; } = "Public";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FollowRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}

public sealed class City
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CountryCode { get; set; }
    public required string CanonicalName { get; set; }
    public required string PersianName { get; set; }
    public required string EnglishName { get; set; }
    public string? GermanName { get; set; }
    public string? LocalName { get; set; }
    public string SearchAliases { get; set; } = "";
}

public sealed class UserFollow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    public Guid FollowedUserId { get; set; }
    public User FollowedUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerId { get; set; }
    public User Blocker { get; set; } = null!;
    public Guid BlockedUserId { get; set; }
    public User BlockedUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public Guid? ReportedUserId { get; set; }
    public User? ReportedUser { get; set; }
    public string TargetType { get; set; } = "User";
    public Guid TargetId { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string ImageUrl { get; set; }
    public string? ImageStorageKey { get; set; }
    public string Caption { get; set; } = "";
    public string? Context { get; set; }
    public string Status { get; set; } = "Published";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<PostLike> Likes { get; set; } = [];
    public ICollection<PostComment> Comments { get; set; } = [];
}

public sealed class PostLike
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PostComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Text { get; set; }
    public string Status { get; set; } = "Published";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ContentMention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MentionedUserId { get; set; }
    public User MentionedUser { get; set; } = null!;
    public Guid? PostId { get; set; }
    public SocialPost? Post { get; set; }
    public Guid? CommentId { get; set; }
    public PostComment? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MessageRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public required string Introduction { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? ConversationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserOneId { get; set; }
    public User UserOne { get; set; } = null!;
    public Guid UserTwoId { get; set; }
    public User UserTwo { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public required string Type { get; set; }
    public required string Text { get; set; }
    public required string Destination { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class ConversationReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
