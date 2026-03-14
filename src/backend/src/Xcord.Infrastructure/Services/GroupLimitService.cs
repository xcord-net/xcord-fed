using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

public sealed class GroupLimitService(AppDbContext dbContext) : IGroupLimitService
{
    public async Task<int?> GetEffectiveLimit(long userId, long serverId, string limitKey, CancellationToken ct = default)
    {
        var memberGroupIds = await dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && mg.ServerId == serverId)
            .Select(mg => mg.GroupId)
            .ToListAsync(ct);

        var groups = await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.ServerId == serverId && (g.IsEveryone || memberGroupIds.Contains(g.Id)))
            .Where(g => g.LimitsJson != null)
            .Select(g => g.LimitsJson!)
            .ToListAsync(ct);

        int? maxLimit = null;
        foreach (var limitsJson in groups)
        {
            try
            {
                var limits = JsonSerializer.Deserialize<Dictionary<string, int>>(limitsJson);
                if (limits != null && limits.TryGetValue(limitKey, out var value))
                {
                    maxLimit = maxLimit.HasValue ? Math.Max(maxLimit.Value, value) : value;
                }
            }
            catch (JsonException)
            {
                // Skip malformed JSON
            }
        }

        return maxLimit;
    }
}
