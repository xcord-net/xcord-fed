namespace Xcord;

/// <summary>
/// Marker interface for entities that support soft deletion.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
