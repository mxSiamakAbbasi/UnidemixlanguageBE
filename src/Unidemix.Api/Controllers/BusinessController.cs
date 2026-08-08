using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;
using Unidemix.Api.Services;

namespace Unidemix.Api.Controllers;

[ApiController, Route("api/catalog")]
public sealed class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("languages")]
    public Task<List<Language>> Languages() => db.Languages.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync();
    [HttpGet("plans")]
    public Task<List<SubscriptionPlan>> Plans() => db.SubscriptionPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MonthlyPrice).ToListAsync();
}

public sealed record QuoteRequest(Guid PlacementId, DateOnly StartsOn, DateOnly EndsOn);
public sealed record BookingRequest(Guid PlacementId, string ApplicantType, string ContactName, string Phone, string Email,
    string? CompanyName, string? CompanyNumber, string TargetUrl, string AssetUrl, DateOnly StartsOn, DateOnly EndsOn);

[ApiController, Route("api/ads")]
public sealed class AdsController(AppDbContext db, ProductFeatureService features) : ControllerBase
{
    [HttpGet("placements")]
    public async Task<IActionResult> Placements()
    {
        if (!await features.IsFixedAdReservationEnabledAsync()) return NotFound();
        return Ok(await db.AdPlacements.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Page).ToListAsync());
    }

    [HttpPost("quote")]
    public async Task<IActionResult> Quote(QuoteRequest request)
    {
        if (!await features.IsFixedAdReservationEnabledAsync()) return NotFound();
        var placement = await db.AdPlacements.FindAsync(request.PlacementId);
        if (placement is null || !placement.IsActive) return NotFound();
        var days = request.EndsOn.DayNumber - request.StartsOn.DayNumber + 1;
        if (days < placement.MinDays || days > placement.MaxDays) return BadRequest(new { title = "Invalid booking duration" });
        var occupied = await db.AdBookings.AnyAsync(x => x.PlacementId == request.PlacementId && x.Status != "Rejected" &&
            x.StartsOn <= request.EndsOn && x.EndsOn >= request.StartsOn);
        return Ok(new { available = !occupied, days, dailyPrice = placement.DailyPrice, totalPrice = days * placement.DailyPrice, placement.Currency });
    }

    [Authorize, HttpPost("bookings")]
    public async Task<IActionResult> Book(BookingRequest request)
    {
        if (!await features.IsFixedAdReservationEnabledAsync()) return NotFound();
        var placement = await db.AdPlacements.FindAsync(request.PlacementId);
        if (placement is null) return NotFound();
        var days = request.EndsOn.DayNumber - request.StartsOn.DayNumber + 1;
        if (days < placement.MinDays || days > placement.MaxDays) return BadRequest();
        if (await db.AdBookings.AnyAsync(x => x.PlacementId == request.PlacementId && x.Status != "Rejected" && x.StartsOn <= request.EndsOn && x.EndsOn >= request.StartsOn))
            return Conflict(new { title = "Selected dates are no longer available" });
        var booking = new AdBooking { PlacementId = request.PlacementId, UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            ApplicantType = request.ApplicantType, ContactName = request.ContactName, Phone = request.Phone, Email = request.Email,
            CompanyName = request.CompanyName, CompanyNumber = request.CompanyNumber, TargetUrl = request.TargetUrl,
            AssetUrl = request.AssetUrl, StartsOn = request.StartsOn, EndsOn = request.EndsOn,
            AgreedPrice = days * placement.DailyPrice, Currency = placement.Currency };
        db.AdBookings.Add(booking); await db.SaveChangesAsync();
        return Ok(booking);
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active([FromQuery] string? page)
    {
        if (!await features.IsFixedAdReservationEnabledAsync()) return NotFound();
        return Ok(await db.AdBookings.AsNoTracking().Include(x => x.Placement)
            .Where(x => x.Status == "Approved" && x.StartsOn <= DateOnly.FromDateTime(DateTime.UtcNow) && x.EndsOn >= DateOnly.FromDateTime(DateTime.UtcNow) && (page == null || x.Placement.Page == page)).ToListAsync());
    }
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin")]
public sealed class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("ad-placements")]
    public Task<List<AdPlacement>> Placements() => db.AdPlacements.AsNoTracking().OrderBy(x => x.Page).ToListAsync();
    [HttpGet("ad-bookings")]
    public Task<List<AdBooking>> Bookings() => db.AdBookings.Include(x => x.Placement).OrderByDescending(x => x.CreatedAt).ToListAsync();
    [HttpPut("ad-bookings/{id:guid}/status")]
    public async Task<IActionResult> BookingStatus(Guid id, [FromBody] StatusRequest request) { var x = await db.AdBookings.FindAsync(id); if (x is null) return NotFound(); x.Status = request.Status; x.AdminNote = request.Note; await db.SaveChangesAsync(); return Ok(x); }
    [HttpPost("ad-placements")]
    public async Task<IActionResult> AddPlacement(AdPlacement item) { db.AdPlacements.Add(item); await db.SaveChangesAsync(); return Ok(item); }
    [HttpPut("ad-placements/{id:guid}")]
    public async Task<IActionResult> UpdatePlacement(Guid id, AdPlacement input) { var x = await db.AdPlacements.FindAsync(id); if (x is null) return NotFound(); x.Name=input.Name;x.Page=input.Page;x.Position=input.Position;x.DailyPrice=input.DailyPrice;x.Currency=input.Currency;x.MinDays=input.MinDays;x.MaxDays=input.MaxDays;x.Width=input.Width;x.Height=input.Height;x.AllowsGif=input.AllowsGif;x.IsActive=input.IsActive;await db.SaveChangesAsync();return Ok(x); }
    [HttpGet("plans")]
    public Task<List<SubscriptionPlan>> Plans() => db.SubscriptionPlans.OrderBy(x => x.MonthlyPrice).ToListAsync();
}
public sealed record StatusRequest(string Status, string? Note);

[ApiController, Route("api/product-features")]
public sealed class ProductFeaturesController(AppDbContext db, ProductFeatureService features) : ControllerBase
{
    [HttpGet("advertising")]
    public async Task<IActionResult> Advertising() => Ok(new
    {
        AdvertisingEnabled = await features.IsEnabledAsync(ProductFeatureKeys.AdvertisingEnabled),
        FixedAdReservationEnabled = await features.IsFixedAdReservationEnabledAsync(),
        SocialPromotionEnabled = await features.IsSocialPromotionEnabledAsync()
    });

    [Authorize(Roles = "Admin"), HttpGet("/api/admin/product-features")]
    public async Task<IActionResult> List() => Ok(await db.ProductFeatureFlags.AsNoTracking()
        .Where(x => ProductFeatureKeys.All.Contains(x.Key)).OrderBy(x => x.Key).ToListAsync());

    [Authorize(Roles = "Admin"), HttpPut("/api/admin/product-features/{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateFeatureFlagRequest request)
    {
        if (!ProductFeatureKeys.All.Contains(key)) return NotFound();
        var flag = await db.ProductFeatureFlags.FindAsync(key);
        if (flag is null)
        {
            flag = new ProductFeatureFlag { Key = key };
            db.ProductFeatureFlags.Add(flag);
        }
        flag.IsEnabled = request.IsEnabled;
        flag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(flag);
    }
}

public sealed record UpdateFeatureFlagRequest(bool IsEnabled);

[ApiController, Authorize, Route("api/membership")]
public sealed class MembershipController(AppDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var subscription = await db.UserSubscriptions.AsNoTracking().Include(x => x.Plan)
            .Where(x => x.UserId == userId && x.Status == "Active" && x.EndsAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.EndsAt).FirstOrDefaultAsync();
        var plan = subscription?.Plan ?? await db.SubscriptionPlans.AsNoTracking().SingleAsync(x => x.Code == "free");
        return Ok(new { plan.Code, plan.Name, plan.LanguageLimit, plan.MonthlyAiCredits, plan.HasMockExams,
            IsPremium = plan.Code != "free", EndsAt = subscription?.EndsAt });
    }
}
