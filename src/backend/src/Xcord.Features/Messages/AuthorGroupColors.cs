using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages;

/// <summary>
/// Resolves the colour a message author's username is rendered in: the colour of
/// their highest-positioned group that defines one.
/// </summary>
/// <remarks>
/// Follows the same "highest non-@everyone group wins" rule as ListMembersHandler,
/// with the extra condition that the group must actually define a colour - a
/// coloured group below an uncoloured one still wins the username.
/// </remarks>
public static class AuthorGroupColors
{
    /// <summary>
    /// Maps author id to display colour within one server, in a single query.
    /// Returns empty for DM conversations, which have no groups.
    /// </summary>
    public static async Task<Dictionary<long, string>> ResolveAsync(
        AppDbContext dbContext,
        long serverId,
        IReadOnlyCollection<long> authorIds,
        CancellationToken cancellationToken)
    {
        if (serverId == 0) return [];

        // Distinct here rather than relying on callers: a repeated author id would
        // otherwise throw building the dictionary below.
        var pairs = authorIds.Distinct().Select(id => (serverId, id)).ToList();
        var byPair = await ResolveManyAsync(dbContext, pairs, cancellationToken);

        return pairs
            .Where(p => byPair.ContainsKey(p))
            .ToDictionary(p => p.id, p => byPair[p]);
    }

    /// <summary>
    /// Maps (server, author) to display colour across any number of servers, in a
    /// single query. Group colour is a per-server attribute, so search results that
    /// span servers have to be keyed this way rather than by author alone.
    /// </summary>
    public static async Task<Dictionary<(long ServerId, long UserId), string>> ResolveManyAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<(long ServerId, long UserId)> pairs,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0) return [];

        var serverIds = pairs.Select(p => p.ServerId).Where(id => id != 0).Distinct().ToList();
        var userIds = pairs.Select(p => p.UserId).Distinct().ToList();
        if (serverIds.Count == 0) return [];

        // Fetched as a cross product of the servers and authors in play, then narrowed
        // to the pairs actually asked for. EF cannot translate a tuple Contains, and
        // for one page of messages the surplus rows are negligible.
        var candidates = await dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => serverIds.Contains(mg.ServerId)
                && userIds.Contains(mg.UserId)
                && !mg.Group.IsEveryone
                && mg.Group.Color != null)
            .Select(mg => new { mg.ServerId, mg.UserId, mg.Group.Color, mg.Group.Position })
            .ToListAsync(cancellationToken);

        var wanted = pairs.ToHashSet();

        return candidates
            .Where(c => wanted.Contains((c.ServerId, c.UserId)))
            .GroupBy(c => (c.ServerId, c.UserId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.Position).First().Color!);
    }

    /// <summary>Resolves the colour for a single author.</summary>
    public static async Task<string?> ResolveOneAsync(
        AppDbContext dbContext,
        long serverId,
        long? authorId,
        CancellationToken cancellationToken)
    {
        if (authorId is null) return null;

        var colors = await ResolveAsync(dbContext, serverId, [authorId.Value], cancellationToken);
        return colors.GetValueOrDefault(authorId.Value);
    }
}
