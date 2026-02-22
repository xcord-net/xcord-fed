using Xcord;

namespace Xcord.Entities;

public sealed class SlashCommand : ISoftDeletable
{
    public long Id { get; set; }
    public long BotTokenId { get; set; }
    public long ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public BotToken BotToken { get; set; } = null!;
    public Server Server { get; set; } = null!;
}
