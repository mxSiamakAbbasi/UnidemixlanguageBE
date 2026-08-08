using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/social")]
public sealed class MessagingController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ConversationReportReasons =
    ["Spam", "Harassment", "InappropriateBehavior", "SexualContent", "Other"];

    [HttpPost("message-requests")]
    public async Task<ActionResult<MessageRequestResponse>> CreateRequest(CreateMessageRequestRequest request)
    {
        var senderId = CurrentUserId();
        if (request.RecipientId == senderId) return BadRequest(new ProblemDetails { Title = "Users cannot message themselves" });
        if (!await db.SocialProfiles.AnyAsync(x => x.UserId == request.RecipientId)) return NotFound();
        if (await IsBlocked(senderId, request.RecipientId)) return Conflict(new ProblemDetails { Title = "Social interaction is unavailable" });
        if (await FindConversation(senderId, request.RecipientId) is not null)
            return Conflict(new ProblemDetails { Title = "A conversation already exists" });
        if (await db.MessageRequests.AnyAsync(x => x.SenderId == senderId && x.RecipientId == request.RecipientId && x.Status == "Pending"))
            return Conflict(new ProblemDetails { Title = "A pending request already exists" });

        var item = new MessageRequest { SenderId = senderId, RecipientId = request.RecipientId, Introduction = request.Introduction.Trim() };
        db.MessageRequests.Add(item);
        var senderName = await db.Users.Where(x => x.Id == senderId).Select(x => x.DisplayName).SingleAsync();
        AddNotification(request.RecipientId, senderId, "MessageRequest", $"{senderName} درخواست گفتگو فرستاد.", "/social/messages?tab=requests");
        await db.SaveChangesAsync();
        return Created($"/api/social/message-requests/{item.Id}", await MapRequest(item.Id));
    }

    [HttpGet("message-requests")]
    public async Task<ActionResult<PagedResponse<MessageRequestResponse>>> Requests([FromQuery] string box = "incoming", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = CurrentUserId();
        (page, pageSize) = NormalizePaging(page, pageSize);
        var query = db.MessageRequests.AsNoTracking().Where(x => box == "outgoing" ? x.SenderId == userId : x.RecipientId == userId)
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == x.SenderId && b.BlockedUserId == x.RecipientId) || (b.BlockerId == x.RecipientId && b.BlockedUserId == x.SenderId)));
        var total = await query.CountAsync();
        var ids = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToListAsync();
        var items = new List<MessageRequestResponse>();
        foreach (var id in ids) items.Add(await MapRequest(id));
        return Ok(new PagedResponse<MessageRequestResponse>(items, page, pageSize, total));
    }

    [HttpPost("message-requests/{requestId:guid}/accept")]
    public async Task<ActionResult<ConversationSummaryResponse>> Accept(Guid requestId)
    {
        var userId = CurrentUserId();
        var request = await db.MessageRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.RecipientId == userId);
        if (request is null) return NotFound();
        if (request.Status != "Pending") return Conflict(new ProblemDetails { Title = "Request has already been handled" });
        if (await IsBlocked(request.SenderId, request.RecipientId)) return Conflict(new ProblemDetails { Title = "Social interaction is unavailable" });

        var conversation = await FindConversation(request.SenderId, request.RecipientId);
        if (conversation is null)
        {
            var (one, two) = NormalizePair(request.SenderId, request.RecipientId);
            conversation = new Conversation { UserOneId = one, UserTwoId = two };
            db.Conversations.Add(conversation);
        }
        request.Status = "Accepted";
        request.RespondedAt = DateTimeOffset.UtcNow;
        request.ConversationId = conversation.Id;
        var recipientName = await db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).SingleAsync();
        AddNotification(request.SenderId, userId, "RequestAccepted", $"{recipientName} درخواست گفتگوی شما را پذیرفت.", $"/social/messages/{conversation.Id}");
        await db.SaveChangesAsync();
        return Ok(await MapConversationSummary(conversation.Id, userId));
    }

    [HttpPost("message-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId)
    {
        var userId = CurrentUserId();
        var request = await db.MessageRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.RecipientId == userId);
        if (request is null) return NotFound();
        if (request.Status != "Pending") return Conflict(new ProblemDetails { Title = "Request has already been handled" });
        request.Status = "Rejected";
        request.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResponse<ConversationSummaryResponse>>> Conversations([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = CurrentUserId();
        (page, pageSize) = NormalizePaging(page, pageSize);
        var query = db.Conversations.AsNoTracking().Where(x => x.UserOneId == userId || x.UserTwoId == userId)
            .Where(x => !db.UserBlocks.Any(b =>
                (b.BlockerId == x.UserOneId && b.BlockedUserId == x.UserTwoId) ||
                (b.BlockerId == x.UserTwoId && b.BlockedUserId == x.UserOneId)));
        var total = await query.CountAsync();
        var ids = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToListAsync();
        var items = new List<ConversationSummaryResponse>();
        foreach (var id in ids) items.Add(await MapConversationSummary(id, userId));
        return Ok(new PagedResponse<ConversationSummaryResponse>(items, page, pageSize, total));
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationResponse>> Conversation(Guid conversationId, [FromQuery] int pageSize = 100)
    {
        var userId = CurrentUserId();
        var conversation = await AuthorizedConversation(conversationId, userId);
        if (conversation is null) return NotFound();
        var unread = await db.ChatMessages.Where(x => x.ConversationId == conversationId && x.SenderId != userId && x.ReadAt == null).ToListAsync();
        var readAt = DateTimeOffset.UtcNow;
        foreach (var message in unread) message.ReadAt = readAt;
        if (unread.Count > 0) await db.SaveChangesAsync();
        pageSize = Math.Clamp(pageSize, 1, 200);
        var messages = await db.ChatMessages.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.CreatedAt).Take(pageSize).OrderBy(x => x.CreatedAt)
            .Join(db.Users, message => message.SenderId, user => user.Id,
                (message, user) => new ChatMessageResponse(message.Id, message.SenderId, user.DisplayName, message.Text, message.CreatedAt, message.ReadAt)).ToListAsync();
        var partnerId = conversation.UserOneId == userId ? conversation.UserTwoId : conversation.UserOneId;
        var partner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == partnerId);
        var photo = await db.SocialProfiles.Where(x => x.UserId == partnerId).Select(x => x.ProfilePhotoUrl).SingleOrDefaultAsync();
        return Ok(new ConversationResponse(conversation.Id, partnerId, partner.DisplayName, photo, messages));
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<ChatMessageResponse>> Send(Guid conversationId, SendMessageRequest request)
    {
        var userId = CurrentUserId();
        var conversation = await AuthorizedConversation(conversationId, userId);
        if (conversation is null) return NotFound();
        var text = request.Text.Trim();
        if (text.Length == 0) return BadRequest(new ProblemDetails { Title = "Message cannot be empty" });
        var message = new ChatMessage { ConversationId = conversationId, SenderId = userId, Text = text };
        db.ChatMessages.Add(message);
        conversation.UpdatedAt = message.CreatedAt;
        var recipientId = conversation.UserOneId == userId ? conversation.UserTwoId : conversation.UserOneId;
        var senderName = await db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).SingleAsync();
        AddNotification(recipientId, userId, "NewMessage", $"پیام جدید از {senderName}", $"/social/messages/{conversationId}");
        await db.SaveChangesAsync();
        return Created($"/api/social/conversations/{conversationId}/messages/{message.Id}",
            new ChatMessageResponse(message.Id, userId, senderName, message.Text, message.CreatedAt, null));
    }

    [HttpPost("conversations/{conversationId:guid}/report")]
    public async Task<IActionResult> ReportConversation(Guid conversationId, CreateConversationReportRequest request)
    {
        var userId = CurrentUserId();
        if (!ConversationReportReasons.Contains(request.Reason)) return BadRequest(new ProblemDetails { Title = "Invalid report reason" });
        if (await AuthorizedConversation(conversationId, userId, allowBlocked: true) is null) return NotFound();
        if (await db.ConversationReports.AnyAsync(x => x.ReporterId == userId && x.ConversationId == conversationId && x.Reason == request.Reason && x.Status == "Active"))
            return Conflict(new ProblemDetails { Title = "An active report already exists" });
        db.ConversationReports.Add(new ConversationReport { ReporterId = userId, ConversationId = conversationId,
            Reason = request.Reason, Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim() });
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Conversation?> AuthorizedConversation(Guid id, Guid userId, bool allowBlocked = false)
    {
        var conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == id && (x.UserOneId == userId || x.UserTwoId == userId));
        if (conversation is null) return null;
        if (!allowBlocked && await IsBlocked(conversation.UserOneId, conversation.UserTwoId)) return null;
        return conversation;
    }

    private Task<Conversation?> FindConversation(Guid first, Guid second)
    {
        var (one, two) = NormalizePair(first, second);
        return db.Conversations.SingleOrDefaultAsync(x => x.UserOneId == one && x.UserTwoId == two);
    }

    private async Task<MessageRequestResponse> MapRequest(Guid id)
    {
        var item = await db.MessageRequests.AsNoTracking().SingleAsync(x => x.Id == id);
        var sender = await db.Users.AsNoTracking().SingleAsync(x => x.Id == item.SenderId);
        var recipient = await db.Users.AsNoTracking().SingleAsync(x => x.Id == item.RecipientId);
        var photo = await db.SocialProfiles.Where(x => x.UserId == item.SenderId).Select(x => x.ProfilePhotoUrl).SingleOrDefaultAsync();
        return new MessageRequestResponse(item.Id, item.SenderId, sender.DisplayName, photo, item.RecipientId,
            recipient.DisplayName, item.Introduction, item.Status, item.ConversationId, item.CreatedAt, item.RespondedAt);
    }

    private async Task<ConversationSummaryResponse> MapConversationSummary(Guid id, Guid userId)
    {
        var conversation = await db.Conversations.AsNoTracking().SingleAsync(x => x.Id == id);
        var partnerId = conversation.UserOneId == userId ? conversation.UserTwoId : conversation.UserOneId;
        var partner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == partnerId);
        var photo = await db.SocialProfiles.Where(x => x.UserId == partnerId).Select(x => x.ProfilePhotoUrl).SingleOrDefaultAsync();
        var last = await db.ChatMessages.AsNoTracking().Where(x => x.ConversationId == id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        var unread = await db.ChatMessages.CountAsync(x => x.ConversationId == id && x.SenderId != userId && x.ReadAt == null);
        return new ConversationSummaryResponse(id, partnerId, partner.DisplayName, photo, last?.Text, last?.CreatedAt, unread);
    }

    private void AddNotification(Guid userId, Guid actorId, string type, string text, string destination) =>
        db.Notifications.Add(new Notification { UserId = userId, ActorId = actorId, Type = type, Text = text, Destination = destination });
    private async Task<bool> IsBlocked(Guid first, Guid second) => await db.UserBlocks.AnyAsync(x =>
        (x.BlockerId == first && x.BlockedUserId == second) || (x.BlockerId == second && x.BlockedUserId == first));
    private static (Guid One, Guid Two) NormalizePair(Guid first, Guid second) => first.CompareTo(second) < 0 ? (first, second) : (second, first);
    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
