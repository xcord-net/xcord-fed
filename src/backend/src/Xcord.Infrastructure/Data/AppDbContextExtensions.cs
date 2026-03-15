using Microsoft.EntityFrameworkCore;

namespace Xcord.Infrastructure.Data;

public static class AppDbContextExtensions
{
    public static async Task<Result<bool>> EnsureMembership(
        this AppDbContext db,
        long serverId,
        long userId,
        CancellationToken ct)
    {
        var isMember = await db.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == serverId, ct);

        return isMember
            ? true
            : Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server");
    }
}
