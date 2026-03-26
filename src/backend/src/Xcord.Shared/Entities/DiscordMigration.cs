namespace Xcord.Entities;

public sealed class DiscordMigration : ISoftDeletable
{
    public long Id { get; set; }
    public string DiscordGuildId { get; set; } = null!;
    public long ServerId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Running, Paused, Completed, Failed
    public string? CurrentPhase { get; set; }
    public string? CheckpointJson { get; set; }
    public string OptionsJson { get; set; } = "{}";
    public int TotalChannels { get; set; }
    public int MigratedChannels { get; set; }
    public long TotalMessages { get; set; }
    public long MigratedMessages { get; set; }
    public int TotalMembers { get; set; }
    public int MigratedMembers { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
}
