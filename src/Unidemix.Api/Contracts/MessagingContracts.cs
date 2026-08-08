using System.ComponentModel.DataAnnotations;

namespace Unidemix.Api.Contracts;

public sealed record CreateMessageRequestRequest(Guid RecipientId, [Required, MinLength(5), MaxLength(300)] string Introduction);
public sealed record MessageRequestResponse(Guid Id, Guid SenderId, string SenderName, string? SenderPhotoUrl,
    Guid RecipientId, string RecipientName, string Introduction, string Status, Guid? ConversationId,
    DateTimeOffset CreatedAt, DateTimeOffset? RespondedAt);
public sealed record ConversationSummaryResponse(Guid Id, Guid PartnerId, string PartnerName, string? PartnerPhotoUrl,
    string? LastMessage, DateTimeOffset? LastMessageAt, int UnreadCount);
public sealed record ChatMessageResponse(Guid Id, Guid SenderId, string SenderName, string Text, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record ConversationResponse(Guid Id, Guid PartnerId, string PartnerName, string? PartnerPhotoUrl,
    IReadOnlyList<ChatMessageResponse> Messages);
public sealed record SendMessageRequest([Required, MinLength(1), MaxLength(2000)] string Text);
public sealed record NotificationResponse(Guid Id, string Type, Guid? ActorId, string? ActorName, string Text,
    string Destination, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record CreateConversationReportRequest([Required, MaxLength(40)] string Reason, [MaxLength(1000)] string? Details);
