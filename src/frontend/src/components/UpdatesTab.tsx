import { Show, For, createSignal, onMount, createMemo } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './UpdatesTab.module.css';

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

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'success') return styles.historyStatusCompleted;
  if (s === 'failed' || s === 'error') return styles.historyStatusFailed;
  if (s === 'inprogress' || s === 'running') return styles.historyStatusInProgress;
  return styles.historyStatusDefault;
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
    <div data-testid="updates-tab" class={styles.container}>
      <Show when={isLoading()}>
        <div class={styles.loadingCenter}>
          <p class={styles.loadingText}>Loading version information...</p>
        </div>
      </Show>

      <Show when={loadError()}>
        <div
          data-testid="updates-load-error"
          role="alert"
          class={styles.errorBanner}
        >
          {loadError()}
        </div>
      </Show>

      <Show when={!isLoading() && versionData() !== null}>
        {/* Hub disconnected banner */}
        <Show when={!versionData()!.hubConnected}>
          <div
            data-testid="updates-hub-disconnected"
            class={styles.hubDisconnectedBanner}
          >
            Updates are unavailable - this instance is not connected to a hub. Connect to a hub to
            receive version updates.
          </div>
        </Show>

        {/* Automatic updates toggle */}
        <div
          data-testid="updates-auto-toggle-section"
          class={styles.autoToggleSection}
        >
          <div>
            <p class={styles.autoToggleLabel}>Automatic Updates</p>
            <p class={styles.autoToggleDescription}>
              Automatically apply updates when new versions are published by the hub.
            </p>
          </div>
          <button
            data-testid="updates-auto-toggle-button"
            type="button"
            disabled={isTogglingAuto() || !versionData()!.hubConnected}
            onClick={handleToggleAutoUpdates}
            class={`${styles.toggleButton} ${versionData()!.batchUpgradesEnabled ? styles.toggleButtonOn : styles.toggleButtonOff}`}
            role="switch"
            aria-checked={versionData()!.batchUpgradesEnabled}
            aria-label="Toggle automatic updates"
          >
            <span
              class={`${styles.toggleThumb} ${versionData()!.batchUpgradesEnabled ? styles.toggleThumbOn : styles.toggleThumbOff}`}
            />
          </button>
        </div>

        <Show when={toggleError()}>
          <div
            data-testid="updates-toggle-error"
            role="alert"
            class={styles.toggleError}
          >
            {toggleError()}
          </div>
        </Show>

        {/* Current version display */}
        <div class={styles.currentVersionRow}>
          <span class={styles.currentVersionLabel}>Current version:</span>
          <span
            data-testid="updates-current-version"
            class={styles.currentVersionValue}
          >
            v{versionData()!.currentVersion}
          </span>
        </div>

        {/* Available versions - only shown when hub is connected */}
        <Show when={versionData()!.hubConnected}>
          <Show
            when={versionData()!.availableVersions && versionData()!.availableVersions!.length > 0}
            fallback={
              <p data-testid="updates-no-versions" class={styles.noVersionsText}>
                No version updates available.
              </p>
            }
          >
            <div class={styles.versionsPanel} style="min-height: 320px;">
              {/* Left panel - version list */}
              <div
                data-testid="updates-version-list"
                class={styles.versionList}
              >
                <div class={styles.versionListHeader}>
                  <p class={styles.versionListHeaderText}>
                    Available Versions
                  </p>
                </div>
                <For each={versionData()!.availableVersions!}>
                  {(v) => (
                    <button
                      data-testid={`updates-version-item-${v.version}`}
                      type="button"
                      class={`${styles.versionItem} ${selectedVersionId() === v.id ? styles.versionItemSelected : styles.versionItemDefault}`}
                      onClick={() => setSelectedVersionId(v.id)}
                    >
                      <div class={styles.versionItemRow}>
                        <span class={styles.versionMono}>v{v.version}</span>
                        <Show when={v.version === versionData()!.currentVersion}>
                          <span
                            data-testid={`updates-current-badge-${v.version}`}
                            class={styles.badgeCurrent}
                          >
                            current
                          </span>
                        </Show>
                        <Show when={v.isMinimumVersion}>
                          <span
                            data-testid={`updates-minimum-badge-${v.version}`}
                            class={styles.badgeMinimum}
                          >
                            min
                          </span>
                        </Show>
                      </div>
                      <p class={styles.versionItemDate}>{formatDate(v.publishedAt)}</p>
                    </button>
                  )}
                </For>
              </div>

              {/* Right panel - release notes */}
              <div
                data-testid="updates-release-notes-panel"
                class={styles.releaseNotesPanel}
              >
                <Show when={selectedVersion()} fallback={<p class={styles.releaseNotesPlaceholder}>Select a version to view release notes.</p>}>
                  <div class={styles.releaseNotesHeader}>
                    <h3 class={styles.releaseNotesTitle}>
                      v{selectedVersion()!.version}
                    </h3>
                    <Show when={!isCurrent()}>
                      <button
                        data-testid="updates-upgrade-button"
                        type="button"
                        disabled={isUpgrading()}
                        onClick={handleUpgrade}
                        class={styles.upgradeButton}
                      >
                        {isUpgrading() ? 'Upgrading...' : 'Update to this version'}
                      </button>
                    </Show>
                    <Show when={isCurrent()}>
                      <span class={styles.currentlyInstalled}>Currently installed</span>
                    </Show>
                  </div>

                  <Show when={selectedVersion()!.isMinimumVersion && selectedVersion()!.minimumEnforcementDate}>
                    <div class={styles.minimumVersionBanner}>
                      This is a minimum required version. Enforcement date: {formatDate(selectedVersion()!.minimumEnforcementDate)}
                    </div>
                  </Show>

                  <Show when={upgradeError()}>
                    <div
                      data-testid="updates-upgrade-error"
                      role="alert"
                      class={styles.upgradeError}
                    >
                      {upgradeError()}
                    </div>
                  </Show>

                  <Show when={upgradeSuccess()}>
                    <div
                      data-testid="updates-upgrade-success"
                      role="status"
                      class={styles.upgradeSuccess}
                    >
                      {upgradeSuccess()}
                    </div>
                  </Show>

                  <Show
                    when={parsedNotes()}
                    fallback={
                      <Show when={selectedVersion()!.releaseNotes}>
                        <p class={styles.rawReleaseNotes}>
                          {selectedVersion()!.releaseNotes}
                        </p>
                      </Show>
                    }
                  >
                    <div class={styles.releaseNotesContent}>
                      <Show when={parsedNotes()!.breakingChanges?.length > 0}>
                        <div data-testid="updates-breaking-changes" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleBreaking}`}>
                            Breaking Changes
                          </h4>
                          <ul class={styles.releaseNotesList}>
                            <For each={parsedNotes()!.breakingChanges}>
                              {(item) => (
                                <li class={styles.releaseNotesListItem}>
                                  <span class={styles.listIconBreaking}>!</span>
                                  {item}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.features?.length > 0}>
                        <div data-testid="updates-features" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleFeatures}`}>
                            New Features
                          </h4>
                          <ul class={styles.releaseNotesList}>
                            <For each={parsedNotes()!.features}>
                              {(item) => (
                                <li class={styles.releaseNotesListItem}>
                                  <span class={styles.listIconFeature}>+</span>
                                  {item.summary}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.fixes?.length > 0}>
                        <div data-testid="updates-fixes" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleFixes}`}>
                            Bug Fixes
                          </h4>
                          <ul class={styles.releaseNotesList}>
                            <For each={parsedNotes()!.fixes}>
                              {(item) => (
                                <li class={styles.releaseNotesListItem}>
                                  <span class={styles.listIconFix}>*</span>
                                  {item.summary}
                                </li>
                              )}
                            </For>
                          </ul>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.other?.length > 0}>
                        <div data-testid="updates-other" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleOther}`}>
                            Other Changes
                          </h4>
                          <ul class={styles.releaseNotesList}>
                            <For each={parsedNotes()!.other}>
                              {(item) => (
                                <li class={styles.releaseNotesListItem}>
                                  <span class={styles.listIconOther}>
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
                        <div data-testid="updates-migration-notes" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleMigration}`}>
                            Migration Notes
                          </h4>
                          <p class={styles.releaseNotesPreWrap}>
                            {parsedNotes()!.migrationNotes}
                          </p>
                        </div>
                      </Show>

                      <Show when={parsedNotes()!.knownIssues}>
                        <div data-testid="updates-known-issues" class={styles.releaseNotesSection}>
                          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleKnownIssues}`}>
                            Known Issues
                          </h4>
                          <p class={styles.releaseNotesPreWrap}>
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
                        <p class={styles.noReleaseNotes}>No release notes available for this version.</p>
                      </Show>
                    </div>
                  </Show>
                </Show>
              </div>
            </div>
          </Show>
        </Show>

        {/* Upgrade history */}
        <div data-testid="updates-history-section" class={styles.historySection}>
          <button
            data-testid="updates-history-toggle"
            type="button"
            class={styles.historyToggleButton}
            onClick={() => setHistoryExpanded((v) => !v)}
            aria-expanded={historyExpanded()}
          >
            <span class={styles.historyToggleLabel}>Upgrade History</span>
            <span
              class={`${styles.historyToggleChevron} ${historyExpanded() ? styles.historyToggleChevronExpanded : ''}`}
              aria-hidden="true"
            >
              ▼
            </span>
          </button>

          <Show when={historyExpanded()}>
            <div data-testid="updates-history-list" class={styles.historyList}>
              <Show
                when={versionData()!.upgradeHistory && versionData()!.upgradeHistory!.length > 0}
                fallback={
                  <p class={styles.historyEmpty}>No upgrade history available.</p>
                }
              >
                <For each={versionData()!.upgradeHistory!}>
                  {(entry) => (
                    <div
                      data-testid={`updates-history-entry-${entry.id}`}
                      class={styles.historyEntry}
                    >
                      <div class={styles.historyEntryRow}>
                        <div class={styles.historyEntryLeft}>
                          <span class={statusClass(entry.status)}>
                            {entry.status}
                          </span>
                          <Show when={entry.previousVersion && entry.newVersion}>
                            <span class={styles.historyVersions}>
                              v{entry.previousVersion} <span class={styles.historyVersionArrow}>→</span> v{entry.newVersion}
                            </span>
                          </Show>
                          <Show when={!entry.previousVersion && entry.newVersion}>
                            <span class={styles.historyVersions}>v{entry.newVersion}</span>
                          </Show>
                        </div>
                        <div class={styles.historyTimestamp}>
                          <Show when={entry.completedAt} fallback={<span>{formatDateTime(entry.startedAt)}</span>}>
                            <span>{formatDateTime(entry.completedAt)}</span>
                          </Show>
                        </div>
                      </div>
                      <Show when={entry.errorMessage}>
                        <p class={styles.historyErrorMessage}>{entry.errorMessage}</p>
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
