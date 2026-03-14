namespace Xcord.Infrastructure.Services;

public interface IGroupLimitService
{
    Task<int?> GetEffectiveLimit(long userId, long serverId, string limitKey, CancellationToken ct = default);
}
