namespace Unidemix.Api.Models;

public sealed class Language
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string NativeName { get; set; }
    public required string FlagEmoji { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public string Currency { get; set; } = "EUR";
    public int LanguageLimit { get; set; } = 1;
    public int MonthlyAiCredits { get; set; }
    public bool HasMockExams { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class AdPlacement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Page { get; set; }
    public required string Position { get; set; }
    public decimal DailyPrice { get; set; }
    public string Currency { get; set; } = "EUR";
    public int MinDays { get; set; } = 1;
    public int MaxDays { get; set; } = 90;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool AllowsGif { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class AdBooking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlacementId { get; set; }
    public AdPlacement Placement { get; set; } = null!;
    public Guid? UserId { get; set; }
    public required string ApplicantType { get; set; }
    public required string ContactName { get; set; }
    public required string Phone { get; set; }
    public required string Email { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyNumber { get; set; }
    public required string TargetUrl { get; set; }
    public required string AssetUrl { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public decimal AgreedPrice { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "PendingPayment";
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductFeatureFlag
{
    public required string Key { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalId { get; set; }
    public required string Type { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Body { get; set; }
    public string Category { get; set; } = "عمومی";
    public string Source { get; set; } = "Unidemix";
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsPublished { get; set; } = true;
}
