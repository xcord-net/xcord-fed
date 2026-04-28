import { Show, createSignal, onMount, createMemo } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import {
  type SystemVersionResponse,
  parseReleaseNotes,
} from './formatters';
import AutoUpdateToggle from './AutoUpdateToggle';
import VersionList from './VersionList';
import ReleaseNotesPanel from './ReleaseNotesPanel';
import UpgradeHistory from './UpgradeHistory';
import styles from './UpdatesTab.module.css';

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
        <Show when={!versionData()!.hubConnected}>
          <div
            data-testid="updates-hub-disconnected"
            class={styles.hubDisconnectedBanner}
          >
            Updates are unavailable - this instance is not connected to a hub. Connect to a hub to
            receive version updates.
          </div>
        </Show>

        <AutoUpdateToggle
          enabled={versionData()!.batchUpgradesEnabled}
          hubConnected={versionData()!.hubConnected}
          isToggling={isTogglingAuto()}
          toggleError={toggleError()}
          onToggle={handleToggleAutoUpdates}
        />

        <div class={styles.currentVersionRow}>
          <span class={styles.currentVersionLabel}>Current version:</span>
          <span
            data-testid="updates-current-version"
            class={styles.currentVersionValue}
          >
            v{versionData()!.currentVersion}
          </span>
        </div>

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
              <VersionList
                versions={versionData()!.availableVersions!}
                selectedId={selectedVersionId()}
                currentVersion={versionData()!.currentVersion}
                onSelect={setSelectedVersionId}
              />
              <ReleaseNotesPanel
                selectedVersion={selectedVersion()}
                parsedNotes={parsedNotes()}
                isCurrent={isCurrent()}
                isUpgrading={isUpgrading()}
                upgradeError={upgradeError()}
                upgradeSuccess={upgradeSuccess()}
                onUpgrade={handleUpgrade}
              />
            </div>
          </Show>
        </Show>

        <UpgradeHistory
          entries={versionData()!.upgradeHistory}
          expanded={historyExpanded()}
          onToggle={() => setHistoryExpanded((v) => !v)}
        />
      </Show>
    </div>
  );
}
