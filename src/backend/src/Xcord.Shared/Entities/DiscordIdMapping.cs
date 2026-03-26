namespace Xcord.Entities;

public sealed class DiscordIdMapping
{
    public long Id { get; set; }
    public long MigrationId { get; set; }
    public string DiscordId { get; set; } = null!;
    public long XcordId { get; set; }
    public string EntityType { get; set; } = null!; // User, Channel, Group, Message, Emoji, Category

    public DiscordMigration Migration { get; set; } = null!;
}
