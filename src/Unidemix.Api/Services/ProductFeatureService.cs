using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;

namespace Unidemix.Api.Services;

public static class ProductFeatureKeys
{
    public const string AdvertisingEnabled = nameof(AdvertisingEnabled);
    public const string FixedAdReservationEnabled = nameof(FixedAdReservationEnabled);
    public const string SocialPromotionEnabled = nameof(SocialPromotionEnabled);

    public static readonly string[] All = [AdvertisingEnabled, FixedAdReservationEnabled, SocialPromotionEnabled];
}

public sealed class ProductFeatureService(AppDbContext db)
{
    public async Task<bool> IsEnabledAsync(string key) =>
        await db.ProductFeatureFlags.AsNoTracking().AnyAsync(x => x.Key == key && x.IsEnabled);

    public async Task<bool> IsFixedAdReservationEnabledAsync() =>
        await IsEnabledAsync(ProductFeatureKeys.AdvertisingEnabled) &&
        await IsEnabledAsync(ProductFeatureKeys.FixedAdReservationEnabled);

    public async Task<bool> IsSocialPromotionEnabledAsync() =>
        await IsEnabledAsync(ProductFeatureKeys.AdvertisingEnabled) &&
        await IsEnabledAsync(ProductFeatureKeys.SocialPromotionEnabled);
}
