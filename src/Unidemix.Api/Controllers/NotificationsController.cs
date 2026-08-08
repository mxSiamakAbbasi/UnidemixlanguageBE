using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public sealed class NotificationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var userId = CurrentUserId(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NotificationResponse(x.Id, x.Type, x.ActorId,
                x.Actor == null ? null : x.Actor.DisplayName, x.Text, x.Destination, x.CreatedAt, x.ReadAt)).ToListAsync();
        return Ok(new PagedResponse<NotificationResponse>(items, page, pageSize, total));
    }

    [HttpGet("unread-count")]
    public Task<int> UnreadCount() => db.Notifications.CountAsync(x => x.UserId == CurrentUserId() && x.ReadAt == null);

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == CurrentUserId());
        if (item is null) return NotFound();
        item.ReadAt ??= DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var items = await db.Notifications.Where(x => x.UserId == CurrentUserId() && x.ReadAt == null).ToListAsync();
        var now = DateTimeOffset.UtcNow; foreach (var item in items) item.ReadAt = now;
        await db.SaveChangesAsync(); return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
