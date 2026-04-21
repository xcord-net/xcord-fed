namespace Xcord.Entities;

/// <summary>
/// Bitfield flags for channel capabilities. A channel can support multiple
/// capabilities simultaneously (e.g., Chat | Voice | Video).
/// </summary>
[Flags]
public enum ChannelCapability : long
{
    None = 0,
    Chat = 1 << 0,
    Voice = 1 << 1,
    Video = 1 << 2,
    Forum = 1 << 3,
    Announcement = 1 << 4,
    Streaming = 1 << 5,
}
