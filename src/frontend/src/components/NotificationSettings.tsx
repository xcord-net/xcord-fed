import { Show, For, createSignal, onMount } from 'solid-js';
import { useNotifications } from '../stores/notification.store';
import { useServers } from '../stores/server.store';
import { requestPermission } from '../services/notification.service';
import type { NotificationLevel } from '../types/notification';
import styles from './NotificationSettings.module.css';

export default function NotificationSettings() {
  const notifStore = useNotifications();
  const serverStore = useServers();

  const getPermission = () =>
    typeof Notification !== 'undefined' ? Notification.permission : 'denied';

  const [permissionState, setPermissionState] = createSignal<NotificationPermission>(getPermission());

  async function handleRequestPermission() {
    const result = await requestPermission();
    setPermissionState(result);
  }

  onMount(() => {
    notifStore.loadSettings();
    notifStore.loadServerOverrides();
    setPermissionState(getPermission());
    // Ensure servers are loaded so the per-server overrides section is visible.
    // Layout.onMount also calls fetchServers but may not have resolved yet.
    if (serverStore.servers.length === 0) {
      serverStore.fetchServers();
    }
  });

  function getServerOverride(serverId: string): NotificationLevel | null {
    const override = notifStore.serverOverrides.find((o) => o.serverId === serverId);
    return override ? override.level : null;
  }

  async function handleServerLevelChange(serverId: string, level: NotificationLevel) {
    await notifStore.setServerNotificationLevel(serverId, level);
  }

  async function handleDeleteServerOverride(serverId: string) {
    const override = notifStore.serverOverrides.find((o) => o.serverId === serverId);
    if (override) {
      await notifStore.deleteServerNotificationOverride(override.id);
    }
  }

  return (
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 data-testid="notification-settings-heading" class={styles.heading}>Notification Settings</h2>
      </div>

      <div class={styles.scrollBody}>
        <Show when={notifStore.isLoading}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <Show when={notifStore.settings}>
          <div class={styles.settingsBlock}>
            <div>
              <h3 class={styles.sectionTitle}>Desktop Notifications</h3>

              <Show when={permissionState() === 'granted'}>
                <p class={styles.permGranted}>Desktop notifications are enabled.</p>
              </Show>

              <Show when={permissionState() === 'denied'}>
                <p class={styles.permDenied}>
                  Desktop notifications are blocked. Allow them in your browser settings.
                </p>
              </Show>

              <Show when={permissionState() === 'default'}>
                <button
                  class={styles.enableButton}
                  onClick={handleRequestPermission}
                >
                  Enable desktop notifications
                </button>
              </Show>
            </div>

            <div>
              <h3 class={styles.sectionTitle}>General</h3>

              <label class={styles.toggleRow}>
                <span class={styles.toggleRowLabel}>Mute all notifications</span>
                <input
                  data-testid="notification-mute-all-checkbox"
                  type="checkbox"
                  checked={notifStore.settings!.muteAll}
                  onChange={(e) => notifStore.updateSettings({ muteAll: e.currentTarget.checked })}
                  class={styles.checkbox}
                />
              </label>

              <label class={styles.toggleRow}>
                <span class={styles.toggleRowLabel}>Show online status</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.showOnlineStatus}
                  onChange={(e) => notifStore.updateSettings({ showOnlineStatus: e.currentTarget.checked })}
                  class={styles.checkbox}
                />
              </label>
            </div>

            <div>
              <h3 class={styles.sectionTitle}>Privacy</h3>

              <label class={styles.toggleRow}>
                <span class={styles.toggleRowLabel}>Allow direct messages</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.allowDirectMessages}
                  onChange={(e) => notifStore.updateSettings({ allowDirectMessages: e.currentTarget.checked })}
                  class={styles.checkbox}
                />
              </label>

              <label class={styles.toggleRow}>
                <span class={styles.toggleRowLabel}>Allow friend requests</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.allowFriendRequests}
                  onChange={(e) => notifStore.updateSettings({ allowFriendRequests: e.currentTarget.checked })}
                  class={styles.checkbox}
                />
              </label>
            </div>

            <div>
              <h3 class={styles.sectionTitle}>Mention Keywords</h3>
              <p class={styles.keywordHint}>
                Get notified when someone mentions these keywords
              </p>

              <div class={styles.keywordsSection}>
                <Show when={(notifStore.settings!.mentionKeywords ?? []).length > 0}>
                  <div class={styles.keywordChips}>
                    {(notifStore.settings!.mentionKeywords ?? []).map((keyword) => (
                      <span class={styles.keywordChip}>
                        {keyword}
                      </span>
                    ))}
                  </div>
                </Show>

                <input
                  type="text"
                  placeholder="Add keyword..."
                  class={styles.keywordInput}
                  onKeyPress={(e) => {
                    if (e.key === 'Enter' && e.currentTarget.value.trim()) {
                      const keywords = [...(notifStore.settings!.mentionKeywords ?? []), e.currentTarget.value.trim()];
                      notifStore.updateSettings({ mentionKeywords: keywords });
                      e.currentTarget.value = '';
                    }
                  }}
                />
              </div>
            </div>
          </div>
        </Show>

        {/* Per-server notification levels */}
        <Show when={serverStore.servers.length > 0}>
          <div>
            <h3 class={styles.sectionTitle}>Server Notification Overrides</h3>
            <p class={styles.serverOverrideHint}>
              Customise notification levels per server. Defaults to All Messages when no override is set.
            </p>

            <div class={styles.serverOverrideList} aria-label="Server notification overrides">
              <For each={serverStore.servers}>
                {(server) => {
                  const currentLevel = () => getServerOverride(server.id);
                  const hasOverride = () => currentLevel() !== null;

                  return (
                    <div
                      class={styles.serverOverrideRow}
                      aria-label={`Notification settings for ${server.name}`}
                    >
                      <span class={styles.serverName}>{server.name}</span>

                      <div class={styles.serverOverrideControls}>
                        <select
                          aria-label={`Notification level for ${server.name}`}
                          class={styles.levelSelect}
                          value={currentLevel() ?? 'All'}
                          onChange={(e) => handleServerLevelChange(server.id, e.currentTarget.value as NotificationLevel)}
                        >
                          <option value="All">All Messages</option>
                          <option value="MentionsOnly">Only Mentions</option>
                          <option value="None">Nothing</option>
                        </select>

                        <Show when={hasOverride()}>
                          <button
                            aria-label={`Reset notifications for ${server.name}`}
                            class={styles.resetButton}
                            onClick={() => handleDeleteServerOverride(server.id)}
                          >
                            Reset
                          </button>
                        </Show>
                      </div>
                    </div>
                  );
                }}
              </For>
            </div>
          </div>
        </Show>
      </div>
    </div>
  );
}
