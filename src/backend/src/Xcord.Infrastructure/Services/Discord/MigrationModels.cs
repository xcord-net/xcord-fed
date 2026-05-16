namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Configurable flags controlling what the Discord migration imports.
/// Persisted as JSON on the <see cref="Xcord.Entities.DiscordMigration"/> row.
/// </summary>
public sealed class MigrationOptions
{
    public bool ImportMessages { get; set; } = true;
    public bool ImportAttachments { get; set; } = true;
    public bool ImportReactions { get; set; } = false;
    public bool ImportThreads { get; set; } = true;
    public bool ImportEmoji { get; set; } = true;
    public bool ImportAvatars { get; set; } = false;
}

/// <summary>
/// Resumable checkpoint state for the Discord migration pipeline.
/// Persisted as JSON on the <see cref="Xcord.Entities.DiscordMigration"/> row
/// after every phase + paginated batch so a failed/cancelled migration can
/// be resumed without redoing work.
/// </summary>
public sealed class MigrationCheckpoint
{
    public HashSet<string> DonePhases { get; set; } = new();
    public HashSet<string> DoneChannels { get; set; } = new();
    public Dictionary<string, string> ChannelCursors { get; set; } = new();
    public string? LastMemberDiscordId { get; set; }
    public Dictionary<string, long> GroupDiscordToXcord { get; set; } = new();
    public Dictionary<string, long> ChannelDiscordToXcord { get; set; } = new();
}
