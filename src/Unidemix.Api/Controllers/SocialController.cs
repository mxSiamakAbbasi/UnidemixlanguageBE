using System.Security.Claims;
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
using System.Globalization;
using System.Text;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/social")]
public sealed class SocialController(AppDbContext db, ISocialImageStorage imageStorage, ISocialFollowerAccessService followerAccess, ISocialUsernameService usernames) : ControllerBase
{
    private static readonly HashSet<string> ReportReasons =
    ["Spam", "Harassment", "FakeProfile", "InappropriateBehavior", "SexualContent", "Other"];
    private static readonly HashSet<string> SupportedLearningLanguages = ["de", "fa", "en", "fr", "tr"];
    private static readonly HashSet<string> SupportedCefrLevels = ["A1", "A2", "B1", "B2", "C1"];

    [HttpGet("profile")]
    public async Task<ActionResult<SocialProfileResponse>> GetOwnProfile()
    {
        var userId = CurrentUserId();
        if (!await db.SocialProfiles.AnyAsync(x => x.UserId == userId))
        {
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
            var profile = new SocialProfile { UserId = userId, Username = "pending" };
            await usernames.SaveNewProfileAsync(profile, user.Email, user.DisplayName);
        }
        return Ok(await ProfileResponse(userId, userId));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<SocialProfileResponse>> UpdateOwnProfile(UpdateSocialProfileRequest request)
    {
        var userId = CurrentUserId();
        var profile = await db.SocialProfiles.SingleOrDefaultAsync(x => x.UserId == userId);
        var isNewProfile = profile is null;
        if (profile is null)
        {
            profile = new SocialProfile { UserId = userId, Username = "pending" };
            db.SocialProfiles.Add(profile);
        }
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        if (!string.IsNullOrWhiteSpace(request.DisplayName)) user.DisplayName = request.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LearningLanguageCode))
        {
            var learningLanguage = request.LearningLanguageCode.Trim().ToLowerInvariant();
            if (!SupportedLearningLanguages.Contains(learningLanguage)) return BadRequest(new ProblemDetails { Title = "Unsupported learning language" });
            user.LearningLanguage = learningLanguage;
        }
        if (!string.IsNullOrWhiteSpace(request.CefrLevel))
        {
            var level = request.CefrLevel.Trim().ToUpperInvariant();
            if (!SupportedCefrLevels.Contains(level)) return BadRequest(new ProblemDetails { Title = "Unsupported CEFR level" });
            user.Level = level;
        }
        if (profile.ProfilePhotoStorageKey is null) profile.ProfilePhotoUrl = NullIfWhiteSpace(request.ProfilePhotoUrl);
        profile.Bio = request.Bio.Trim();
        profile.PracticeGoal = request.PracticeGoal.Trim();
        profile.Availability = request.Availability.Trim();
        if (request.CityId is not null && !await db.Cities.AnyAsync(x => x.Id == request.CityId))
            return BadRequest(new ProblemDetails { Title = "Selected city was not found" });
        profile.CityId = request.CityId;
        profile.City = request.CityId is null ? null : await db.Cities.Where(x => x.Id == request.CityId).Select(x => x.PersianName).SingleAsync();
        profile.LookingForPartner = request.LookingForPartner;
        profile.Privacy = request.Privacy;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNewProfile) await usernames.SaveNewProfileAsync(profile, user.Email, user.DisplayName);
        else await db.SaveChangesAsync();
        return Ok(await ProfileResponse(userId, userId));
    }

    [HttpGet("discover")]
    public async Task<ActionResult<PagedResponse<PartnerSummaryResponse>>> Discover(
        [FromQuery] string? nativeLanguage, [FromQuery] string? learningLanguage,
        [FromQuery] string? cefrLevel, [FromQuery] string? goal,
        [FromQuery] string? availability, [FromQuery] Guid? cityId,
        [FromQuery] string? search = null, [FromQuery] bool excludeFollowing = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        var currentId = CurrentUserId();
        var current = await db.Users.AsNoTracking().SingleAsync(x => x.Id == currentId);
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = db.SocialProfiles.AsNoTracking().Include(x => x.User).Include(x => x.CityReference)
            .Where(x => x.LookingForPartner && x.UserId != currentId)
            .Where(x => !db.UserBlocks.Any(b =>
                (b.BlockerId == currentId && b.BlockedUserId == x.UserId) ||
                (b.BlockerId == x.UserId && b.BlockedUserId == currentId)));

        if (!string.IsNullOrWhiteSpace(nativeLanguage)) query = query.Where(x => x.User.NativeLanguage == nativeLanguage.ToLower());
        if (!string.IsNullOrWhiteSpace(learningLanguage)) query = query.Where(x => x.User.LearningLanguage == learningLanguage.ToLower());
        if (!string.IsNullOrWhiteSpace(cefrLevel)) query = query.Where(x => x.User.Level == cefrLevel.ToUpper());
        if (!string.IsNullOrWhiteSpace(goal)) query = query.Where(x => x.User.Goal == goal || x.PracticeGoal.Contains(goal));
        if (!string.IsNullOrWhiteSpace(availability)) query = query.Where(x => x.Availability.Contains(availability));
        if (cityId is not null) query = query.Where(x => x.CityId == cityId);
        var normalizedSearch = usernames.Normalize(search ?? "");
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Username == normalizedSearch || x.User.DisplayName.ToLower().Contains(search.Trim().TrimStart('@').ToLower()));
        if (excludeFollowing) query = query.Where(x => !db.UserFollows.Any(f => f.FollowerId == currentId && f.FollowedUserId == x.UserId));

        var total = await query.CountAsync();
        var profiles = await query
            .OrderByDescending(x => x.Username == normalizedSearch)
            .ThenByDescending(x => x.User.NativeLanguage == current.LearningLanguage)
            .ThenByDescending(x => x.User.LearningLanguage == current.NativeLanguage)
            .ThenByDescending(x => x.User.Level == current.Level)
            .ThenByDescending(x => x.User.Goal == current.Goal)
            .ThenBy(x => x.User.DisplayName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var followedIds = await db.UserFollows.AsNoTracking().Where(x => x.FollowerId == currentId)
            .Select(x => x.FollowedUserId).ToListAsync();
        var followed = followedIds.ToHashSet();
        var requested = (await db.FollowRequests.AsNoTracking().Where(x => x.RequesterId == currentId && x.Status == "Pending")
            .Select(x => x.TargetUserId).ToListAsync()).ToHashSet();
        var items = profiles.Select(x => ToPartnerSummary(x, current, followed.Contains(x.UserId), requested.Contains(x.UserId))).ToList();
        return Ok(new PagedResponse<PartnerSummaryResponse>(items, page, pageSize, total));
    }

    [HttpGet("suggestions")]
    public Task<ActionResult<PagedResponse<PartnerSummaryResponse>>> Suggestions([FromQuery] int pageSize = 8) =>
        Discover(null, null, null, null, null, null, null, true, 1, pageSize);

    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyList<CityResponse>>> SearchCities([FromQuery] string query = "")
    {
        var normalized = NormalizeSearch(query);
        if (normalized.Length < 3) return Ok(Array.Empty<CityResponse>());
        var cities = await db.Cities.AsNoTracking().ToListAsync();
        var result = cities.Select(city => new
            {
                City = city,
                Rank = SearchText(city).StartsWith(normalized, StringComparison.Ordinal) ? 0 :
                    SearchText(city).Split('|').Any(x => x.StartsWith(normalized, StringComparison.Ordinal)) ? 1 : 2
            })
            .Where(x => SearchText(x.City).Contains(normalized, StringComparison.Ordinal))
            .OrderBy(x => x.Rank).ThenBy(x => x.City.PersianName).Take(8)
            .Select(x => new CityResponse(x.City.Id, x.City.CountryCode, x.City.PersianName, x.City.EnglishName)).ToList();
        return Ok(result);
    }

    [HttpPost("profile/photo"), Consumes("multipart/form-data"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<SocialProfileResponse>> UploadProfilePhoto(IFormFile file, CancellationToken cancellationToken)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (file.Length is <= 0 or > 8 * 1024 * 1024 || !allowed.Contains(file.ContentType))
            return BadRequest(new ProblemDetails { Title = "Only valid JPEG, PNG, or WebP images up to 8 MB are supported" });
        try
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, cancellationToken);
            if (image.Width <= 0 || image.Height <= 0 || image.Width > 12000 || image.Height > 12000 || (long)image.Width * image.Height > 40_000_000)
                return BadRequest(new ProblemDetails { Title = "Image dimensions are invalid or too large" });
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center, Size = new Size(512, 512) }));
            await using var optimized = new MemoryStream();
            await image.SaveAsWebpAsync(optimized, new WebpEncoder { Quality = 82 }, cancellationToken); optimized.Position = 0;
            var stored = await imageStorage.SaveAsync(optimized, "webp", cancellationToken);
            var userId = CurrentUserId(); var profile = await db.SocialProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            var isNewProfile = profile is null;
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, cancellationToken);
            if (profile is null) { profile = new SocialProfile { UserId = userId, Username = "pending" }; db.SocialProfiles.Add(profile); }
            profile.ProfilePhotoStorageKey = stored.StorageKey;
            profile.ProfilePhotoUrl = $"{Request.Scheme}://{Request.Host}{stored.ImageUrl}";
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNewProfile) await usernames.SaveNewProfileAsync(profile, user.Email, user.DisplayName, cancellationToken);
            else await db.SaveChangesAsync(cancellationToken);
            return Ok(await ProfileResponse(userId, userId));
        }
        catch (UnknownImageFormatException) { return BadRequest(new ProblemDetails { Title = "The uploaded file is not a valid image" }); }
        catch (InvalidImageContentException) { return BadRequest(new ProblemDetails { Title = "The uploaded image is invalid or corrupted" }); }
    }

    [HttpGet("profiles/{userId:guid}")]
    public async Task<ActionResult<SocialProfileResponse>> GetProfile(Guid userId)
    {
        var currentId = CurrentUserId();
        if (userId != currentId && await IsBlocked(currentId, userId)) return NotFound();
        return await ProfileResponse(userId, currentId) is { } profile ? Ok(profile) : NotFound();
    }

    [HttpGet("profiles/by-username/{username}")]
    public async Task<ActionResult<SocialProfileResponse>> GetByUsername(string username)
    {
        var currentId = CurrentUserId(); var normalized = usernames.Normalize(username);
        if (normalized.Length is < 3 or > 24) return NotFound();
        var userId = await db.SocialProfiles.AsNoTracking().Where(x => x.Username == normalized).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync();
        if (userId is null || userId != currentId && await IsBlocked(currentId, userId.Value)) return NotFound();
        return await ProfileResponse(userId.Value, currentId) is { } profile ? Ok(profile) : NotFound();
    }

    [HttpPost("follows/{userId:guid}")]
    public async Task<ActionResult<FollowActionResponse>> Follow(Guid userId)
    {
        var currentId = CurrentUserId();
        if (userId == currentId) return BadRequest(new ProblemDetails { Title = "Users cannot follow themselves" });
        var target = await db.SocialProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId);
        if (target is null) return NotFound();
        if (await followerAccess.IsBlockedAsync(currentId, userId)) return Conflict(new ProblemDetails { Title = "Social interaction is unavailable" });
        if (await db.UserFollows.AnyAsync(x => x.FollowerId == currentId && x.FollowedUserId == userId))
            return Ok(new FollowActionResponse("Following"));
        if (target.Privacy == "Private")
        {
            if (!await db.FollowRequests.AnyAsync(x => x.RequesterId == currentId && x.TargetUserId == userId && x.Status == "Pending"))
            {
                db.FollowRequests.Add(new FollowRequest { RequesterId = currentId, TargetUserId = userId });
                AddNotification(userId, currentId, "FollowRequest", "درخواست دنبال‌کردن جدیدی دارید.", "/social/profile?followRequests=1");
                await db.SaveChangesAsync();
            }
            return Ok(new FollowActionResponse("Requested"));
        }
        if (!await db.UserFollows.AnyAsync(x => x.FollowerId == currentId && x.FollowedUserId == userId))
        {
            db.UserFollows.Add(new UserFollow { FollowerId = currentId, FollowedUserId = userId });
            var pending = await db.FollowRequests.SingleOrDefaultAsync(x => x.RequesterId == currentId && x.TargetUserId == userId && x.Status == "Pending");
            if (pending is not null) { pending.Status = "Accepted"; pending.RespondedAt = DateTimeOffset.UtcNow; }
            await db.SaveChangesAsync();
        }
        return Ok(new FollowActionResponse("Following"));
    }

    [HttpDelete("follow-requests/{userId:guid}")]
    public async Task<IActionResult> CancelFollowRequest(Guid userId)
    {
        var currentId = CurrentUserId();
        var request = await db.FollowRequests.SingleOrDefaultAsync(x => x.RequesterId == currentId && x.TargetUserId == userId && x.Status == "Pending");
        if (request is not null) { request.Status = "Cancelled"; request.RespondedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); }
        return NoContent();
    }

    [HttpGet("follow-requests")]
    public async Task<ActionResult<PagedResponse<FollowRequestResponse>>> FollowRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var currentId = CurrentUserId(); (page, pageSize) = NormalizePaging(page, pageSize);
        var query = db.FollowRequests.AsNoTracking().Where(x => x.TargetUserId == currentId && x.Status == "Pending");
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.Users, x => x.RequesterId, x => x.Id, (r, u) => new { r, u })
            .GroupJoin(db.SocialProfiles, x => x.r.RequesterId, x => x.UserId, (x, p) => new { x.r, x.u, p })
            .SelectMany(x => x.p.DefaultIfEmpty(), (x, p) => new FollowRequestResponse(x.r.Id, x.r.RequesterId, x.u.DisplayName,
                p == null ? null : p.ProfilePhotoUrl, x.u.NativeLanguage, x.u.LearningLanguage, x.u.Level, x.r.CreatedAt)).ToListAsync();
        return Ok(new PagedResponse<FollowRequestResponse>(items, page, pageSize, total));
    }

    [HttpPost("follow-requests/{requestId:guid}/accept")]
    public async Task<ActionResult<FollowActionResponse>> AcceptFollowRequest(Guid requestId)
    {
        var currentId = CurrentUserId();
        var request = await db.FollowRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.TargetUserId == currentId);
        if (request is null) return NotFound();
        if (request.Status == "Accepted") return Ok(new FollowActionResponse("Following"));
        if (request.Status != "Pending") return Conflict(new ProblemDetails { Title = "Follow request is no longer pending" });
        if (await followerAccess.IsBlockedAsync(currentId, request.RequesterId)) return Conflict(new ProblemDetails { Title = "Social interaction is unavailable" });
        if (!await db.UserFollows.AnyAsync(x => x.FollowerId == request.RequesterId && x.FollowedUserId == currentId))
            db.UserFollows.Add(new UserFollow { FollowerId = request.RequesterId, FollowedUserId = currentId });
        request.Status = "Accepted"; request.RespondedAt = DateTimeOffset.UtcNow;
        AddNotification(request.RequesterId, currentId, "FollowRequestAccepted", "درخواست دنبال‌کردن شما پذیرفته شد.", $"/social/profile/{currentId}");
        await db.SaveChangesAsync();
        return Ok(new FollowActionResponse("Following"));
    }

    [HttpPost("follow-requests/{requestId:guid}/decline")]
    public async Task<IActionResult> DeclineFollowRequest(Guid requestId)
    {
        var currentId = CurrentUserId();
        var request = await db.FollowRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.TargetUserId == currentId);
        if (request is null) return NotFound();
        if (request.Status != "Pending") return Conflict(new ProblemDetails { Title = "Follow request is no longer pending" });
        request.Status = "Declined"; request.RespondedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("follows/{userId:guid}")]
    public async Task<IActionResult> Unfollow(Guid userId)
    {
        var currentId = CurrentUserId();
        var follow = await db.UserFollows.SingleOrDefaultAsync(x => x.FollowerId == currentId && x.FollowedUserId == userId);
        if (follow is not null) { db.UserFollows.Remove(follow); await db.SaveChangesAsync(); }
        return NoContent();
    }

    [HttpGet("profiles/{userId:guid}/followers")]
    public Task<ActionResult<PagedResponse<PartnerSummaryResponse>>> Followers(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        FollowList(userId, true, page, pageSize);

    [HttpGet("profiles/{userId:guid}/following")]
    public Task<ActionResult<PagedResponse<PartnerSummaryResponse>>> Following(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        FollowList(userId, false, page, pageSize);

    [HttpPost("blocks/{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId)
    {
        var currentId = CurrentUserId();
        if (userId == currentId) return BadRequest(new ProblemDetails { Title = "Users cannot block themselves" });
        if (!await db.Users.AnyAsync(x => x.Id == userId)) return NotFound();
        if (!await db.UserBlocks.AnyAsync(x => x.BlockerId == currentId && x.BlockedUserId == userId))
        {
            db.UserBlocks.Add(new UserBlock { BlockerId = currentId, BlockedUserId = userId });
        }
        db.UserFollows.RemoveRange(await db.UserFollows.Where(x =>
            (x.FollowerId == currentId && x.FollowedUserId == userId) || (x.FollowerId == userId && x.FollowedUserId == currentId)).ToListAsync());
        var pending = await db.FollowRequests.Where(x => x.Status == "Pending" &&
            ((x.RequesterId == currentId && x.TargetUserId == userId) || (x.RequesterId == userId && x.TargetUserId == currentId))).ToListAsync();
        foreach (var request in pending) { request.Status = "Cancelled"; request.RespondedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("blocks/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId)
    {
        var currentId = CurrentUserId();
        var block = await db.UserBlocks.SingleOrDefaultAsync(x => x.BlockerId == currentId && x.BlockedUserId == userId);
        if (block is not null) { db.UserBlocks.Remove(block); await db.SaveChangesAsync(); }
        return NoContent();
    }

    [HttpPost("reports")]
    public async Task<ActionResult<UserReportResponse>> ReportUser(CreateUserReportRequest request)
    {
        var currentId = CurrentUserId();
        if (request.ReportedUserId == currentId) return BadRequest(new ProblemDetails { Title = "Users cannot report themselves" });
        if (!ReportReasons.Contains(request.Reason)) return BadRequest(new ProblemDetails { Title = "Invalid report reason" });
        if (!await db.Users.AnyAsync(x => x.Id == request.ReportedUserId)) return NotFound();
        var existing = await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.ReporterId == currentId &&
            x.ReportedUserId == request.ReportedUserId && x.Reason == request.Reason && x.Status == "Active");
        if (existing is not null) return Conflict(new ProblemDetails { Title = "An active report already exists" });
        var report = new Report { ReporterId = currentId, ReportedUserId = request.ReportedUserId, TargetType = "User", TargetId = request.ReportedUserId,
            Reason = request.Reason, Details = NullIfWhiteSpace(request.Details) };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return Created("/api/social/reports", new UserReportResponse(report.Id, report.ReportedUserId!.Value, report.Reason, report.Status, report.CreatedAt));
    }

    private async Task<ActionResult<PagedResponse<PartnerSummaryResponse>>> FollowList(Guid userId, bool followers, int page, int pageSize)
    {
        var currentId = CurrentUserId();
        if (userId != currentId && await IsBlocked(currentId, userId)) return NotFound();
        if (userId != currentId)
        {
            var target = await db.SocialProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId);
            if (target is null) return NotFound();
            if (target.Privacy == "Private" && !await db.UserFollows.AnyAsync(x => x.FollowerId == currentId && x.FollowedUserId == userId)) return Forbid();
        }
        (page, pageSize) = NormalizePaging(page, pageSize);
        var ids = followers
            ? db.UserFollows.Where(x => x.FollowedUserId == userId).Select(x => x.FollowerId)
            : db.UserFollows.Where(x => x.FollowerId == userId).Select(x => x.FollowedUserId);
        var query = db.SocialProfiles.AsNoTracking().Include(x => x.User).Include(x => x.CityReference).Where(x => ids.Contains(x.UserId))
            .Where(x => !db.UserBlocks.Any(b => (b.BlockerId == currentId && b.BlockedUserId == x.UserId) || (b.BlockerId == x.UserId && b.BlockedUserId == currentId)));
        var total = await query.CountAsync();
        var profiles = await query.OrderBy(x => x.User.DisplayName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var myFollowing = (await db.UserFollows.AsNoTracking().Where(x => x.FollowerId == currentId).Select(x => x.FollowedUserId).ToListAsync()).ToHashSet();
        var current = await db.Users.AsNoTracking().SingleAsync(x => x.Id == currentId);
        var requested = (await db.FollowRequests.AsNoTracking().Where(x => x.RequesterId == currentId && x.Status == "Pending").Select(x => x.TargetUserId).ToListAsync()).ToHashSet();
        return Ok(new PagedResponse<PartnerSummaryResponse>(profiles.Select(x => ToPartnerSummary(x, current, myFollowing.Contains(x.UserId), requested.Contains(x.UserId))).ToList(), page, pageSize, total));
    }

    private async Task<SocialProfileResponse?> ProfileResponse(Guid userId, Guid viewerId)
    {
        var profile = await db.SocialProfiles.AsNoTracking().Include(x => x.User).Include(x => x.CityReference).SingleOrDefaultAsync(x => x.UserId == userId);
        if (profile is null) return null;
        var blockedIds = db.UserBlocks.Where(x => x.BlockerId == userId).Select(x => x.BlockedUserId)
            .Union(db.UserBlocks.Where(x => x.BlockedUserId == userId).Select(x => x.BlockerId));
        var followers = await db.UserFollows.CountAsync(x => x.FollowedUserId == userId && !blockedIds.Contains(x.FollowerId));
        var following = await db.UserFollows.CountAsync(x => x.FollowerId == userId && !blockedIds.Contains(x.FollowedUserId));
        var posts = await db.SocialPosts.CountAsync(x => x.UserId == userId && x.Status == "Published");
        var isFollowing = viewerId != userId && await db.UserFollows.AnyAsync(x => x.FollowerId == viewerId && x.FollowedUserId == userId);
        var isBlocked = viewerId != userId && await db.UserBlocks.AnyAsync(x => x.BlockerId == viewerId && x.BlockedUserId == userId);
        var isRequested = viewerId != userId && await db.FollowRequests.AnyAsync(x => x.RequesterId == viewerId && x.TargetUserId == userId && x.Status == "Pending");
        var followState = isFollowing ? "Following" : isRequested ? "Requested" : "None";
        return new SocialProfileResponse(profile.UserId, profile.Username, profile.User.DisplayName, profile.ProfilePhotoUrl, profile.Bio,
            profile.User.NativeLanguage, profile.User.LearningLanguage, profile.User.Level, profile.User.Goal,
            profile.PracticeGoal, profile.Availability, profile.CityReference?.PersianName ?? profile.City, profile.CityId, profile.LookingForPartner,
            posts, followers, following, isFollowing && !isBlocked, isBlocked, profile.Privacy, isBlocked ? "Blocked" : followState);
    }

    private async Task<bool> IsBlocked(Guid first, Guid second) => await db.UserBlocks.AnyAsync(x =>
        (x.BlockerId == first && x.BlockedUserId == second) || (x.BlockerId == second && x.BlockedUserId == first));

    private static PartnerSummaryResponse ToPartnerSummary(SocialProfile profile, User current, bool isFollowing, bool isRequested)
    {
        var reason = profile.User.NativeLanguage == current.LearningLanguage && profile.User.LearningLanguage == current.NativeLanguage
            ? $"زبان مادری {profile.User.DisplayName} {LanguageFa(profile.User.NativeLanguage)} است و در حال یادگیری {LanguageFa(profile.User.LearningLanguage)} است."
            : profile.User.NativeLanguage == current.LearningLanguage
                ? $"زبان مادری او {LanguageFa(profile.User.NativeLanguage)} است."
                : profile.User.Level == current.Level ? $"سطح زبانی مشابه شما ({profile.User.Level}) دارد." : "برای تمرین زبان در دسترس است.";
        return new PartnerSummaryResponse(profile.UserId, profile.Username, profile.User.DisplayName, profile.ProfilePhotoUrl,
            profile.User.NativeLanguage, profile.User.LearningLanguage, profile.User.Level, profile.User.Goal,
            profile.PracticeGoal, profile.Availability, profile.CityReference?.PersianName ?? profile.City, profile.CityId, reason, isFollowing,
            profile.Privacy, isFollowing ? "Following" : isRequested ? "Requested" : "None");
    }

    private void AddNotification(Guid userId, Guid actorId, string type, string text, string destination) =>
        db.Notifications.Add(new Notification { UserId = userId, ActorId = actorId, Type = type, Text = text, Destination = destination });

    private static string LanguageFa(string code) => code switch { "de" => "آلمانی", "fa" => "فارسی", "en" => "انگلیسی", _ => code.ToUpperInvariant() };
    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string SearchText(City city) => NormalizeSearch(string.Join('|', city.PersianName, city.EnglishName, city.GermanName, city.LocalName, city.CanonicalName, city.SearchAliases));
    private static string NormalizeSearch(string value)
    {
        var text = value.Trim().ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک');
        var decomposed = text.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
