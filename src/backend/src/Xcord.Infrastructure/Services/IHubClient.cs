namespace Xcord.Infrastructure.Services;

public interface IHubClient
{
    Task<HubVersionsResponse?> GetVersionsAsync(CancellationToken ct = default);
    Task<bool> RequestUpgradeAsync(string targetVersion, CancellationToken ct = default);
    Task<bool> UpdateBatchPreferenceAsync(bool enabled, CancellationToken ct = default);
    Task<HubUpgradeHistoryResponse?> GetUpgradeHistoryAsync(CancellationToken ct = default);
}

public sealed record HubVersionItem(
    string Id, string Version, string Image, string? ReleaseNotes,
    bool IsMinimumVersion, string? MinimumEnforcementDate, string PublishedAt);

public sealed record HubVersionsResponse(
    List<HubVersionItem> Versions, string? CurrentVersion, bool BatchUpgradesEnabled);

public sealed record HubUpgradeHistoryItem(
    string Id, string Status, string? PreviousVersion, string? NewVersion,
    string TargetImage, string? ErrorMessage, string? StartedAt, string? CompletedAt);

public sealed record HubUpgradeHistoryResponse(List<HubUpgradeHistoryItem> Events);
