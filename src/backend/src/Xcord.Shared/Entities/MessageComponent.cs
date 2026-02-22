using Xcord;

namespace Xcord.Entities;

public sealed class MessageComponent : ISoftDeletable
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public ComponentType ComponentType { get; set; }
    public string? CustomId { get; set; }
    public string? Label { get; set; }
    public int? Style { get; set; }
    public string? OptionsJson { get; set; }
    public bool Disabled { get; set; }
    public int Row { get; set; }
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Message Message { get; set; } = null!;
}

public enum ComponentType
{
    ActionRow = 1,
    Button = 2,
    SelectMenu = 3,
    TextInput = 4,
    Modal = 5
}
