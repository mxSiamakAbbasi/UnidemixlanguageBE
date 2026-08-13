using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;

namespace Unidemix.Api.Services;

public interface ISocialFollowerAccessService
{
    Task<bool> IsBlockedAsync(Guid firstUserId, Guid secondUserId);
    Task<bool> IsApprovedFollowerAsync(Guid viewerId, Guid profileOwnerId);
}

public sealed class SocialFollowerAccessService(AppDbContext db) : ISocialFollowerAccessService
{
    public Task<bool> IsBlockedAsync(Guid firstUserId, Guid secondUserId) => db.UserBlocks.AnyAsync(x =>
        (x.BlockerId == firstUserId && x.BlockedUserId == secondUserId) ||
        (x.BlockerId == secondUserId && x.BlockedUserId == firstUserId));

    public async Task<bool> IsApprovedFollowerAsync(Guid viewerId, Guid profileOwnerId) =>
        viewerId == profileOwnerId || (!await IsBlockedAsync(viewerId, profileOwnerId) &&
        await db.UserFollows.AnyAsync(x => x.FollowerId == viewerId && x.FollowedUserId == profileOwnerId));
}
