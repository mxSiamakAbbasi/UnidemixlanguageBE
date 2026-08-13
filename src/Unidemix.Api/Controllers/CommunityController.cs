using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;
using Unidemix.Api.Models;
using Unidemix.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/social")]
public sealed partial class CommunityController(AppDbContext db, ISocialImageStorage imageStorage) : ControllerBase
{
    private static readonly HashSet<string> Contexts = ["StudyToday", "LearningMoment", "WithLanguagePartner", "Achievement", "Other"];
    private static readonly HashSet<string> PostReportReasons = ["UnrelatedToLanguageLearning", "PoliticalOrInflammatory", "Harassment", "InappropriateContent", "SpamOrAdvertising", "PrivacyViolation", "Other"];
    private static readonly HashSet<string> CommentReportReasons = ["Harassment", "PoliticalOrInflammatory", "InappropriateContent", "Spam", "PrivacyViolation", "Other"];

    [HttpGet("feed")]
    public async Task<ActionResult<PagedResponse<PostResponse>>> Feed([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var viewerId = CurrentUserId(); (page, pageSize) = Paging(page, pageSize);
        var query = VisiblePosts(viewerId);
        var total = await query.CountAsync();
        var posts = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResponse<PostResponse>(posts.Select(x => MapPost(x, viewerId)).ToList(), page, pageSize, total));
    }

    [HttpPost("posts")]
    public async Task<ActionResult<PostResponse>> CreatePost(CreatePostRequest request)
    {
        var userId = CurrentUserId();
        if (!await db.SocialProfiles.AnyAsync(x => x.UserId == userId)) return BadRequest(new ProblemDetails { Title = "A Social profile is required" });
        var storageKey = request.ImageStorageKey.Trim();
        if (!await imageStorage.ExistsAsync(storageKey, HttpContext.RequestAborted)) return BadRequest(new ProblemDetails { Title = "Uploaded image was not found" });
        var post = new SocialPost { UserId = userId, ImageStorageKey = storageKey, ImageUrl = AbsoluteImageUrl(imageStorage.GetPublicUrl(storageKey)), Caption = request.Caption.Trim() };
        db.SocialPosts.Add(post);
        await AddMentions(post.Caption, userId, post: post);
        await db.SaveChangesAsync();
        return Created($"/api/social/posts/{post.Id}", MapPost(await PostQuery().SingleAsync(x => x.Id == post.Id), userId));
    }

    [HttpPost("uploads/images"), Consumes("multipart/form-data"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ImageUploadResponse>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        const long maxBytes = 8 * 1024 * 1024;
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (file.Length is <= 0 or > maxBytes) return BadRequest(new ProblemDetails { Title = "Image must be between 1 byte and 8 MB" });
        if (!allowedTypes.Contains(file.ContentType)) return BadRequest(new ProblemDetails { Title = "Only JPEG, PNG, and WebP images are supported" });
        try
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, cancellationToken);
            if (image.Width <= 0 || image.Height <= 0 || image.Width > 12000 || image.Height > 12000 || (long)image.Width * image.Height > 40_000_000)
                return BadRequest(new ProblemDetails { Title = "Image dimensions are invalid or too large" });
            image.Mutate(x => { x.AutoOrient(); if (Math.Max(image.Width, image.Height) > 1600) x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1600, 1600) }); });
            await using var optimized = new MemoryStream();
            await image.SaveAsWebpAsync(optimized, new WebpEncoder { Quality = 82 }, cancellationToken);
            optimized.Position = 0;
            var stored = await imageStorage.SaveAsync(optimized, "webp", cancellationToken);
            return Created(stored.ImageUrl, new ImageUploadResponse(AbsoluteImageUrl(stored.ImageUrl), stored.StorageKey, stored.ThumbnailUrl));
        }
        catch (UnknownImageFormatException) { return BadRequest(new ProblemDetails { Title = "The uploaded file is not a valid supported image" }); }
        catch (InvalidImageContentException) { return BadRequest(new ProblemDetails { Title = "The uploaded image is invalid or corrupted" }); }
    }

    [HttpGet("posts/{postId:guid}")]
    public async Task<ActionResult<PostDetailResponse>> GetPost(Guid postId)
    {
        var viewerId = CurrentUserId();
        var post = await VisiblePosts(viewerId).SingleOrDefaultAsync(x => x.Id == postId);
        if (post is null) return NotFound();
        var comments = await CommentQuery().Where(x => x.PostId == postId && x.Status == "Published")
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == viewerId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == viewerId)))
            .OrderBy(x => x.CreatedAt).ToListAsync();
        return Ok(new PostDetailResponse(MapPost(post, viewerId), comments.Select(x => MapComment(x, viewerId)).ToList()));
    }

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId)
    {
        var post = await db.SocialPosts.SingleOrDefaultAsync(x => x.Id == postId && x.Status == "Published");
        if (post is null) return NotFound();
        if (post.UserId != CurrentUserId()) return Forbid();
        post.Status = "Removed"; post.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("posts/{postId:guid}/likes")]
    public async Task<IActionResult> Like(Guid postId)
    {
        var userId = CurrentUserId(); var post = await db.SocialPosts.SingleOrDefaultAsync(x => x.Id == postId && x.Status == "Published");
        if (post is null || await IsBlocked(userId, post.UserId)) return NotFound();
        if (!await db.PostLikes.AnyAsync(x => x.UserId == userId && x.PostId == postId))
        {
            db.PostLikes.Add(new PostLike { UserId = userId, PostId = postId });
            if (post.UserId != userId) AddNotification(post.UserId, userId, "PostLiked", "پست شما را پسندید.", $"/social/posts/{postId}");
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("posts/{postId:guid}/likes")]
    public async Task<IActionResult> Unlike(Guid postId)
    {
        var userId = CurrentUserId(); var like = await db.PostLikes.SingleOrDefaultAsync(x => x.UserId == userId && x.PostId == postId);
        if (like is not null) { db.PostLikes.Remove(like); await db.SaveChangesAsync(); } return NoContent();
    }

    [HttpGet("posts/{postId:guid}/comments")]
    public async Task<ActionResult<PagedResponse<CommentResponse>>> Comments(Guid postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var viewerId = CurrentUserId(); if (!await VisiblePosts(viewerId).AnyAsync(x => x.Id == postId)) return NotFound();
        (page, pageSize) = Paging(page, pageSize);
        var query = CommentQuery().Where(x => x.PostId == postId && x.Status == "Published")
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == viewerId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == viewerId)));
        var total = await query.CountAsync(); var comments = await query.OrderBy(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResponse<CommentResponse>(comments.Select(x => MapComment(x, viewerId)).ToList(), page, pageSize, total));
    }

    [HttpPost("posts/{postId:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> CreateComment(Guid postId, CreateCommentRequest request)
    {
        var userId = CurrentUserId(); var post = await db.SocialPosts.SingleOrDefaultAsync(x => x.Id == postId && x.Status == "Published");
        if (post is null || await IsBlocked(userId, post.UserId)) return NotFound();
        var comment = new PostComment { PostId = postId, UserId = userId, Text = request.Text.Trim() }; db.PostComments.Add(comment);
        if (post.UserId != userId) AddNotification(post.UserId, userId, "PostCommented", "برای پست شما نظر نوشت.", $"/social/posts/{postId}");
        await AddMentions(comment.Text, userId, postId: postId, comment: comment);
        await db.SaveChangesAsync();
        return Created($"/api/social/posts/{postId}", MapComment(await CommentQuery().SingleAsync(x => x.Id == comment.Id), userId));
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        var comment = await db.PostComments.SingleOrDefaultAsync(x => x.Id == commentId && x.Status == "Published");
        if (comment is null) return NotFound(); if (comment.UserId != CurrentUserId()) return Forbid();
        comment.Status = "Removed"; comment.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("profiles/{userId:guid}/posts")]
    public async Task<ActionResult<PagedResponse<PostResponse>>> ProfilePosts(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        var viewerId = CurrentUserId(); if (userId != viewerId && await IsBlocked(viewerId, userId)) return NotFound();
        (page, pageSize) = Paging(page, pageSize); var query = VisiblePosts(viewerId).Where(x => x.UserId == userId); var total = await query.CountAsync();
        var posts = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResponse<PostResponse>(posts.Select(x => MapPost(x, viewerId)).ToList(), page, pageSize, total));
    }

    [HttpGet("mentions")]
    public async Task<ActionResult<IReadOnlyList<MentionSuggestionResponse>>> Mentions([FromQuery] string query = "")
    {
        var userId = CurrentUserId(); var term = query.Trim().TrimStart('@').ToLowerInvariant();
        var users = await db.SocialProfiles.AsNoTracking().Include(x => x.User)
            .Where(x => x.UserId != userId)
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == userId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == userId)))
            .OrderBy(x => x.User.DisplayName).Take(50).ToListAsync();
        return Ok(users.Where(x => term.Length == 0 || x.User.DisplayName.ToLowerInvariant().Contains(term) || MentionKey(x.User).Contains(term))
            .Take(8).Select(x => new MentionSuggestionResponse(x.UserId, x.User.DisplayName, x.ProfilePhotoUrl, MentionKey(x.User))).ToList());
    }

    [HttpGet("profiles/by-mention/{mentionKey}")]
    public async Task<ActionResult<MentionSuggestionResponse>> ProfileByMention(string mentionKey)
    {
        var viewerId = CurrentUserId(); var key = mentionKey.Trim().TrimStart('@').ToLowerInvariant();
        var profiles = await db.SocialProfiles.AsNoTracking().Include(x => x.User)
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == viewerId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == viewerId))).ToListAsync();
        var profile = profiles.SingleOrDefault(x => MentionKey(x.User) == key); return profile is null ? NotFound() : Ok(new MentionSuggestionResponse(profile.UserId, profile.User.DisplayName, profile.ProfilePhotoUrl, key));
    }

    [HttpPost("content-reports")]
    public async Task<ActionResult<ContentReportResponse>> ReportContent(CreateContentReportRequest request)
    {
        var reporterId = CurrentUserId(); if (request.TargetType is not ("Post" or "Comment")) return BadRequest(new ProblemDetails { Title = "Invalid report target" });
        var reasons = request.TargetType == "Post" ? PostReportReasons : CommentReportReasons;
        if (!reasons.Contains(request.Reason)) return BadRequest(new ProblemDetails { Title = "Invalid report reason" });
        Guid ownerId;
        if (request.TargetType == "Post") { var target = await db.SocialPosts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.Status == "Published"); if (target is null) return NotFound(); ownerId = target.UserId; }
        else { var target = await db.PostComments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.Status == "Published"); if (target is null) return NotFound(); ownerId = target.UserId; }
        if (ownerId == reporterId) return BadRequest(new ProblemDetails { Title = "Users cannot report their own content" });
        if (await db.Reports.AnyAsync(x => x.ReporterId == reporterId && x.TargetType == request.TargetType && x.TargetId == request.TargetId && x.Reason == request.Reason && x.Status == "Active")) return Conflict(new ProblemDetails { Title = "An active report already exists" });
        var report = new Report { ReporterId = reporterId, ReportedUserId = ownerId, TargetType = request.TargetType, TargetId = request.TargetId, Reason = request.Reason, Details = NullIfWhiteSpace(request.Details) };
        db.Reports.Add(report); await db.SaveChangesAsync();
        return Created("/api/social/content-reports", new ContentReportResponse(report.Id, report.TargetType, report.TargetId, report.Reason, report.Status, report.CreatedAt));
    }

    private IQueryable<SocialPost> PostQuery() => db.SocialPosts.AsNoTracking().Include(x => x.User).ThenInclude(x => x.SocialProfile).Include(x => x.Likes).Include(x => x.Comments);
    private IQueryable<SocialPost> VisiblePosts(Guid viewerId) => PostQuery().Where(x => x.Status == "Published")
        .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == viewerId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == viewerId)));
    private IQueryable<PostComment> CommentQuery() => db.PostComments.AsNoTracking().Include(x => x.User).ThenInclude(x => x.SocialProfile);
    private PostResponse MapPost(SocialPost post, Guid viewerId) => new(post.Id, Author(post.User), post.ImageUrl, post.Caption, post.Context, post.Status, post.CreatedAt, post.UpdatedAt, post.Likes.Count, post.Comments.Count(x => x.Status == "Published"), post.Likes.Any(x => x.UserId == viewerId), post.UserId == viewerId);
    private CommentResponse MapComment(PostComment comment, Guid viewerId) => new(comment.Id, comment.PostId, Author(comment.User), comment.Text, comment.Status, comment.CreatedAt, comment.UpdatedAt, comment.UserId == viewerId);
    private static PostAuthorResponse Author(User user) => new(user.Id, user.DisplayName, user.SocialProfile?.ProfilePhotoUrl, user.NativeLanguage, user.LearningLanguage, user.Level, MentionKey(user));

    private async Task AddMentions(string text, Guid actorId, SocialPost? post = null, Guid? postId = null, PostComment? comment = null)
    {
        var keys = MentionRegex().Matches(text).Select(x => x.Groups[1].Value.ToLowerInvariant()).Distinct().ToHashSet(); if (keys.Count == 0) return;
        var profiles = await db.SocialProfiles.Include(x => x.User).Where(x => x.UserId != actorId)
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == actorId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == actorId))).ToListAsync();
        foreach (var profile in profiles.Where(x => keys.Contains(MentionKey(x.User))).DistinctBy(x => x.UserId))
        {
            var mention = new ContentMention { MentionedUserId = profile.UserId, Post = post, PostId = postId, Comment = comment }; db.ContentMentions.Add(mention);
            var destinationPostId = post?.Id ?? postId!.Value; AddNotification(profile.UserId, actorId, "Mentioned", "شما را منشن کرد.", $"/social/posts/{destinationPostId}");
        }
    }

    private void AddNotification(Guid userId, Guid actorId, string type, string text, string destination) => db.Notifications.Add(new Notification { UserId = userId, ActorId = actorId, Type = type, Text = text, Destination = destination });
    private async Task<bool> IsBlocked(Guid first, Guid second) => await db.UserBlocks.AnyAsync(x => (x.BlockerId == first && x.BlockedUserId == second) || (x.BlockerId == second && x.BlockedUserId == first));
    private static string MentionKey(User user) => user.SocialProfile?.Username ?? "member";
    private static (int, int) Paging(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 50));
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string AbsoluteImageUrl(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : $"{Request.Scheme}://{Request.Host}{path}";
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [GeneratedRegex(@"(?<![\w@])@([A-Za-z0-9_.-]{1,64})", RegexOptions.CultureInvariant)] private static partial Regex MentionRegex();
}
