namespace Xcord.Infrastructure.Options;

public sealed class TierOptions
{
    public const string SectionName = "Tier";

    // Feature flags - default to true (permissive for standalone instances not managed by hub)
    public bool CanUseVoiceChannels { get; set; } = true;
    public bool CanUseVideoChannels { get; set; } = true;
    public bool CanCreateBots { get; set; } = true;
    public bool CanUseWebhooks { get; set; } = true;
    public bool CanUseCustomEmoji { get; set; } = true;
    public bool CanUseThreads { get; set; } = true;
    public bool CanUseForumChannels { get; set; } = true;
    public bool CanUseScheduledEvents { get; set; } = true;
    public bool CanUseHdVideo { get; set; }
    public bool CanUseSimulcast { get; set; }
    public bool CanUseRecording { get; set; }
    public bool CanUseMemberTiers { get; set; } = true;

    // Resource limits - 0 means unlimited (for standalone instances)
    public int MaxUsers { get; set; }
    public int MaxServers { get; set; }
    public int MaxStorageMb { get; set; }
    public int MaxRateLimit { get; set; }
    public int MaxVoiceConcurrency { get; set; }
    public int MaxVideoConcurrency { get; set; }

    // Quality limits - 0 means unlimited (for standalone instances)
    public int MaxAudioBitrateKbps { get; set; }
    public int MaxVideoBitrateKbps { get; set; }
    public int MaxVideoWidth { get; set; }
    public int MaxVideoHeight { get; set; }
    public int MaxVideoFps { get; set; }
    public int MaxScreenShareBitrateKbps { get; set; }
}
