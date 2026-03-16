import { Show, For, createSignal, onMount, createMemo } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';

interface AvailableVersion {
  id: string;
  version: string;
  image: string;
  releaseNotes: string | null;
  isMinimumVersion: boolean;
  minimumEnforcementDate: string | null;
  publishedAt: string;
}

interface UpgradeHistoryEntry {
  id: string;
  status: string;
  previousVersion: string | null;
  newVersion: string | null;
  targetImage: string;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
}

interface SystemVersionResponse {
  currentVersion: string;
  hubConnected: boolean;
  batchUpgradesEnabled: boolean;
  availableVersions: AvailableVersion[] | null;
  upgradeHistory: UpgradeHistoryEntry[] | null;
}

interface ReleaseNotes {
  version: string;
  features: { summary: string; commit: string }[];
  fixes: { summary: string; commit: string }[];
  other: { summary: string; commit: string; type: string }[];
  breakingChanges: string[];
  migrationNotes: string;
  knownIssues: string;
}

function parseReleaseNotes(raw: string | null): ReleaseNotes | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as ReleaseNotes;
  } catch {
    return null;
  }
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  const d = new Date(dateStr);
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function formatDateTime(dateStr: string | null): string {
  if (!dateStr) return '-';
  const d = new Date(dateStr);
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'success') return 'text-green-400';
  if (s === 'failed' || s === 'error') return 'text-red-400';
  if (s === 'inprogress' || s === 'running') return 'text-yellow-400';
  return 'text-xcord-text-muted';
}

export default function UpdatesTab() {
  const [versionData, setVersionData] = createSignal<SystemVersionResponse | null>(null);
  const [isLoading, setIsLoading] = createSignal(true);
  const [loadError, setLoadError] = createSignal<string | null>(null);
  const [selectedVersionId, setSelectedVersionId] = createSignal<string | null>(null);
  const [isUpgrading, setIsUpgrading] = createSignal(false);
  const [upgradeError, setUpgradeError] = createSignal<string | null>(null);
  const [upgradeSuccess, setUpgradeSuccess] = createSignal<string | null>(null);
  const [isTogglingAuto, setIsTogglingAuto] = createSignal(false);
  const [toggleError, setToggleError] = createSignal<string | null>(null);
  const [historyExpanded, setHistoryExpanded] = createSignal(false);

  onMount(async () => {
    await loadVersionData();
  });

  async function loadVersionData() {
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await api.get<SystemVersionResponse>('/api/v1/admin/system/version');
      setVersionData(data);
      // Pre-select current version in the list if available
      if (data.availableVersions && data.availableVersions.length > 0) {
        const current = data.availableVersions.find((v) => v.version === data.currentVersion);
        setSelectedVersionId(current?.id ?? data.availableVersions[0].id);
      }
    } catch (err: unknown) {
      setLoadError(getErrorMessage(err, 'Failed to load version information.'));
    } finally {
      setIsLoading(false);
    }
  }

  async function handleToggleAutoUpdates() {
    const data = versionData();
    if (!data) return;
    setIsTogglingAuto(true);
    setToggleError(null);
    try {
      await api.patch('/api/v1/admin/system/batch-upgrades', { enabled: !data.batchUpgradesEnabled });
      setVersionData({ ...data, batchUpgradesEnabled: !data.batchUpgradesEnabled });
    } catch (err: unknown) {
      setToggleError(getErrorMessage(err, 'Failed to update automatic upgrades setting.'));
    } finally {
      setIsTogglingAuto(false);
    }
  }

  async function handleUpgrade() {
    const data = versionData();
    const versionId = selectedVersionId();
    if (!data || !versionId) return;
    const version = data.availableVersions?.find((v) => v.id === versionId);
    if (!version) return;
    setIsUpgrading(true);
    setUpgradeError(null);
    setUpgradeSuccess(null);
    try {
      await api.post('/api/v1/admin/system/upgrade', { targetVersion: version.version });
      setUpgradeSuccess(`Upgrade to v${version.version} has been initiated.`);
      // Reload to get updated history
      await loadVersionData();
    } catch (err: unknown) {
      setUpgradeError(getErrorMessage(err, 'Failed to initiate upgrade.'));
    } finally {
      setIsUpgrading(false);
    }
  }

  const selectedVersion = createMemo(() => {
    const data = versionData();
    const id = selectedVersionId();
    if (!data?.availableVersions || !id) return null;
    return data.availableVersions.find((v) => v.id === id) ?? null;
  });

  const parsedNotes = createMemo(() => parseReleaseNotes(selectedVersion()?.releaseNotes ?? null));

  const isCurrent = createMemo(() => {
    const data = versionData();
    const v = selectedVersion();
    if (!data || !v) return false;
    return v.version === data.currentVersion;
  });

  return (
    <div data-testid="updates-tab" class="flex flex-col gap-6">
      <Show when={isLoading()}>
        <div class="flex items-center justify-center py-12">
          <p class="text-xcord-text-muted">Loading version information...</p>
        </div>
      </Show>

      <Show when={loadError()}>
        <div
          data-testid="updates-load-error"
          role="alert"
          class="px-4 py-3 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm"
        >
          {loadError()}
        </div>
      </Show>

      <Show when={!isLoading() && versionData() !== null}>
        {/* Hub disconnected banner */}
        <Show when={!versionData()!.hubConnected}>
          <div
            data-testid="updates-hub-disconnected"
            class="px-4 py-3 bg-yellow-500/10 border border-yellow-500/30 rounded text-yellow-400 text-sm"
          >
            Updates are unavailable - this instance is not connected to a hub. Connect to a hub to
            receive version updates.
          </div>
        </Show>

        {/* Automatic updates toggle */}
        <div
          data-testid="updates-auto-toggle-section"
          class="flex items-center justify-between bg-xcord-bg-secondary rounded-lg px-4 py-3 border border-xcord-border"
        >
          <div>
            <p class="text-xcord-text-primary text-sm font-medium">Automatic Updates</p>
            <p class="text-xcord-text-muted text-xs mt-0.5">
              Automatically apply updates when new versions are published by the hub.
            </p>
          </div>
          <button
            data-testid="updates-auto-toggle-button"
            type="button"
            disabled={isTogglingAuto() || !versionData()!.hubConnected}
            onClick={handleToggleAutoUpdates}
            class={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand disabled:opacity-50 ${
              versionData()!.batchUpgradesEnabled ? 'bg-xcord-brand' : 'bg-xcord-bg-tertiary'
            }`}
            role="switch"
            aria-checked={versionData()!.batchUpgradesEnabled}
            aria-label="Toggle automatic updates"
          >
            <span
              class={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                versionData()!.batchUpgradesEnabled ? 'translate-x-6' : 'translate-x-1'
              }`}
            />
          </button>
        </div>

        <Show when={toggleError()}>
          <div
            data-testid="updates-toggle-error"
            role="alert"
            class="px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm"
          >
            {toggleError()}
          </div>
        </Show>

        {/* Current version display */}
        <div class="flex items-center gap-3">
          <span class="text-xcord-text-muted text-sm">Current version:</span>
          <span
            data-testid="updates-current-version"
            class="text-white font-mono text-sm font-semibold"
          >
            v{versionData()!.currentVersion}
          </span>
        </div>

        {/* Available versions - only shown when hub is connected */}
        <Show when={versionData()!.hubConnected}>
          <Show
            when={versionData()!.availableVersions && versionData()!.availableVersions!.length > 0}
            fallback={
              <p data-testid="updates-no-versions" class="text-xcord-text-muted text-sm">
                No version updates available.
              </p>
            }
          >
            <div class="flex gap-4" style="min-height: 320px;">
              {/* Left panel - version list */}
              <div
                data-testid="updates-version-list"
                class="w-48 flex-shrink-0 bg-xcord-bg-secondary rounded-lg border border-xcord-border overflow-y-auto"
              >
                <div class="px-3 py-2 border-b border-xcord-border">
                  <p class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide">
                    Available Versions
                  </p>
                </div>
                <For each={versionData()!.availableVersions!}>
                  {(v) => (
                    <button
                      data-testid={`updates-version-item-${v.version}`}
                      type="button"
                      class={`w-full text-left px-3 py-2.5 text-sm transition-colors border-b border-xcord-border/50 last:border-0 ${
                        selectedVersionId() === v.id
                          ? 'bg-xcord-brand/20 text-white'
                          : 'text-xcord-text-secondary hover:bg-xcord-bg-tertiary hover:text-white'
                      }`}
                      onClick={() => setSelectedVersionId(v.id)}
                    >
                      <div class="flex items-center gap-2">
                        <span class="font-mono font-medium">v{v.version}</span>
                        <Show when={v.version === versionData()!.currentVersion}>
                          <span
                            data-testid={`updates-current-badge-${v.version}`}
                            class="text-[10px] bg-xcord-brand text-white px-1.5 py-0.5 rounded-full font-semibold"
                          >
                            current
                          </span>
                        </Show>
                        <Show when={v.isMinimumVersion}>
                          <span
                            data-testid={`updates-minimum-badge-${v.version}`}
                            class="text-[10px] bg-red-600 text-white px-1.5 py-0.5 rounded-full font-semibold"
                          >
                            min
                          </span>
                        </Show>
                      </div>
                      <p class="text-xcord-text-muted text-xs mt-0.5">{formatDate(v.publishedAt)}</p>
                    </button>
                  )}
                </For>
              </div>

              {/* Right panel - release notes */}
              <div
                data-testid="updates-release-notes-panel"
                class="flex-1 bg-xcord-bg-secondary rounded-lg border border-xcord-border overflow-y-auto p-4"
              >
                <Show when={selectedVersion()} fallback={<p class="text-xcord-text-muted text-sm">Select a version to view release notes.</p>}>
                  <div class="flex items-center justify-between mb-3">
                    <h3 class="text-white font-semibold text-base">
                      v{selectedVersion()!.version}
                    </h3>
                    <Show when={!isCurrent()}>
                      <button
                        data-testid="updates-upgrade-button"
                        type="button"
                        disabled={isUpgrading()}
                        onClick={handleUpgrade}
                        class="px-4 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                      >
                        {isUpgrading() ? 'Upgrading...' : 'Update to this version'}
                      </button>
                    </Show>
                    <Show when={isCurrent()}>
                      <span class="text-xcord-text-muted text-sm italic">Currently installed</span>
                    </Show>
                  </div>

                  <Show when={selectedVersion()!.isMinimumVersion && selectedVersion()!.minimumEnforcementDate}>
                    <div class="mb-3 px-3 py-2 bg-red-500/10 border border-red-500/30 rounded text-red-400 text-xs">
                      This is a minimum required version. Enforcement date: {formatDate(selectedVersion()!.minimumEnforcementDate)}
                    </div>
                  </Show>

                  <Show when={upgradeError()}>
                    <div
                      data-testid="updates-upgrade-error"
                      role="alert"
                      class="mb-3 px-3 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm"
                    >
                      {upgradeError()}
                    </div>
                  </Show>

                  <Show when={upgradeSuccess()}>
                    <div
                      data-testid="updates-upgrade-success"
                      role="status"
                      class="mb-3 px-3 py-2 bg-green-500/20 border border-green-500/30 rounded text-green-400 text-sm"
                    >
                      {upgradeSuccess()}
                    </div>
                  </Show>

                  <Show
                    when={parsedNotes()}
                    fallback={
                      <Show when={selectedVersion()!.releaseNotes}>
                        <p class="text-xcord-text-secondary text-sm whitespace-pre-wrap">
                          {selectedVersion()!.releaseNotes}
                        </p>
                      </Show>
                    }
                  >
                    <div class="space-y-4 text-sm">
                      <Show when={parsedNotes()!.breakingChanges?.length > 0}>
                        <div data-testid="updates-breaking-changes">
                          <h4 class="text-red-400 font-semibold uppercase text-xs tracking-wide mb-1.5">
                            Breaking Changes
                          </h4>
                          <ul class="space-y-1">
                            <For each={parsedNotes()!.breakingChanges}>
                              {(item) => (
                                <li class="text-xcord-text-secondary flex gap-2">
                                  <span class="text-red-400 flex-shrink-0">!</span>
                                  {item}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.features?.length > 0}>
                        <div data-testid="updates-features">
                          <h4 class="text-green-400 font-semibold uppercase text-xs tracking-wide mb-1.5">
                            New Features
                          </h4>
                          <ul class="space-y-1">
                            <For each={parsedNotes()!.features}>
                              {(item) => (
                                <li class="text-xcord-text-secondary flex gap-2">
                                  <span class="text-green-400 flex-shrink-0">+</span>
                                  {item.summary}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.fixes?.length > 0}>
                        <div data-testid="updates-fixes">
                          <h4 class="text-yellow-400 font-semibold uppercase text-xs tracking-wide mb-1.5">
                            Bug Fixes
                          </h4>
                          <ul class="space-y-1">
                            <For each={parsedNotes()!.fixes}>
                              {(item) => (
                                <li class="text-xcord-text-secondary flex gap-2">
                                  <span class="text-yellow-400 flex-shrink-0">*</span>
                                  {item.summary}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.other?.length > 0}>
                        <div data-testid="updates-other">
                          <h4 class="text-xcord-text-muted font-semibold uppercase text-xs tracking-wide mb-1.5">
                            Other Changes
                          </h4>
                          <ul class="space-y-1">
                            <For each={parsedNotes()!.other}>
                              {(item) => (
                                <li class="text-xcord-text-secondary flex gap-2">
                                  <span class="text-xcord-text-muted flex-shrink-0 capitalize">
                                    [{item.type}]
                                  </span>
                                  {item.summary}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.migrationNotes}>
                        <div data-testid="updates-migration-notes">
                          <h4 class="text-xcord-brand font-semibold uppercase text-xs tracking-wide mb-1.5">
                            Migration Notes
                          </h4>
                          <p class="text-xcord-text-secondary whitespace-pre-wrap">
                            {parsedNotes()!.migrationNotes}
                          </p>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.knownIssues}>
                        <div data-testid="updates-known-issues">
                          <h4 class="text-orange-400 font-semibold uppercase text-xs tracking-wide mb-1.5">
                            Known Issues
                          </h4>
                          <p class="text-xcord-text-secondary whitespace-pre-wrap">
                            {parsedNotes()!.knownIssues}
                          </p>
                        </div>
                      </Show>

                      <Show
                        when={
                          !parsedNotes()!.features?.length &&
                          !parsedNotes()!.fixes?.length &&
                          !parsedNotes()!.other?.length &&
                          !parsedNotes()!.breakingChanges?.length &&
                          !parsedNotes()!.migrationNotes &&
                          !parsedNotes()!.knownIssues
                        }
                      >
                        <p class="text-xcord-text-muted text-sm">No release notes available for this version.</p>
                      </Show>
                    </div>
                  </Show>
                </Show>
              </div>
            </div>
          </Show>
        </Show>

        {/* Upgrade history */}
        <div data-testid="updates-history-section" class="border border-xcord-border rounded-lg overflow-hidden">
          <button
            data-testid="updates-history-toggle"
            type="button"
            class="w-full flex items-center justify-between px-4 py-3 bg-xcord-bg-secondary hover:bg-xcord-bg-tertiary transition-colors text-left"
            onClick={() => setHistoryExpanded((v) => !v)}
            aria-expanded={historyExpanded()}
          >
            <span class="text-sm font-medium text-xcord-text-primary">Upgrade History</span>
            <span
              class={`text-xcord-text-muted text-xs transition-transform ${historyExpanded() ? 'rotate-180' : ''}`}
              aria-hidden="true"
            >
              ▼
            </span>
          </button>

          <Show when={historyExpanded()}>
            <div data-testid="updates-history-list" class="divide-y divide-xcord-border/50">
              <Show
                when={versionData()!.upgradeHistory && versionData()!.upgradeHistory!.length > 0}
                fallback={
                  <p class="px-4 py-3 text-xcord-text-muted text-sm">No upgrade history available.</p>
                }
              >
                <For each={versionData()!.upgradeHistory!}>
                  {(entry) => (
                    <div
                      data-testid={`updates-history-entry-${entry.id}`}
                      class="px-4 py-3 bg-xcord-bg-primary text-sm"
                    >
                      <div class="flex items-center justify-between">
                        <div class="flex items-center gap-3">
                          <span class={`font-semibold ${statusColor(entry.status)}`}>
                            {entry.status}
                          </span>
                          <Show when={entry.previousVersion && entry.newVersion}>
                            <span class="text-xcord-text-muted">
                              v{entry.previousVersion} <span class="text-xcord-text-secondary">→</span> v{entry.newVersion}
                            </span>
                          </Show>
                          <Show when={!entry.previousVersion && entry.newVersion}>
                            <span class="text-xcord-text-muted">v{entry.newVersion}</span>
                          </Show>
                        </div>
                        <div class="text-xcord-text-muted text-xs">
                          <Show when={entry.completedAt} fallback={<span>{formatDateTime(entry.startedAt)}</span>}>
                            <span>{formatDateTime(entry.completedAt)}</span>
                          </Show>
                        </div>
                      </div>
                      <Show when={entry.errorMessage}>
                        <p class="mt-1 text-red-400 text-xs">{entry.errorMessage}</p>
                      </Show>
                    </div>
                  )}
                </For>
              </Show>
            </div>
          </Show>
        </div>
      </Show>
    </div>
  );
}
