import { Show, For, createSignal, onMount } from 'solid-js';
import { useNotifications } from '../stores/notification.store';
import { useServers } from '../stores/server.store';
import { requestPermission } from '../services/notification.service';
import type { NotificationLevel } from '../types/notification';

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
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 data-testid="notification-settings-heading" class="text-white font-semibold">Notification Settings</h2>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-6">
        <Show when={notifStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={notifStore.settings}>
          <div class="space-y-4">
            <div>
              <h3 class="text-white font-semibold mb-2">Desktop Notifications</h3>

              <Show when={permissionState() === 'granted'}>
                <p class="text-sm text-green-400 py-2">Desktop notifications are enabled.</p>
              </Show>

              <Show when={permissionState() === 'denied'}>
                <p class="text-sm text-red-400 py-2">
                  Desktop notifications are blocked. Allow them in your browser settings.
                </p>
              </Show>

              <Show when={permissionState() === 'default'}>
                <button
                  class="px-3 py-2 bg-xcord-brand text-white rounded text-sm hover:opacity-90 transition"
                  onClick={handleRequestPermission}
                >
                  Enable desktop notifications
                </button>
              </Show>
            </div>

            <div>
              <h3 class="text-white font-semibold mb-2">General</h3>

              <label class="flex items-center justify-between py-2">
                <span class="text-xcord-text-primary">Mute all notifications</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.muteAll}
                  onChange={(e) => notifStore.updateSettings({ muteAll: e.currentTarget.checked })}
                  class="w-5 h-5"
                />
              </label>

              <label class="flex items-center justify-between py-2">
                <span class="text-xcord-text-primary">Show online status</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.showOnlineStatus}
                  onChange={(e) => notifStore.updateSettings({ showOnlineStatus: e.currentTarget.checked })}
                  class="w-5 h-5"
                />
              </label>
            </div>

            <div>
              <h3 class="text-white font-semibold mb-2">Privacy</h3>

              <label class="flex items-center justify-between py-2">
                <span class="text-xcord-text-primary">Allow direct messages</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.allowDirectMessages}
                  onChange={(e) => notifStore.updateSettings({ allowDirectMessages: e.currentTarget.checked })}
                  class="w-5 h-5"
                />
              </label>

              <label class="flex items-center justify-between py-2">
                <span class="text-xcord-text-primary">Allow friend requests</span>
                <input
                  type="checkbox"
                  checked={notifStore.settings!.allowFriendRequests}
                  onChange={(e) => notifStore.updateSettings({ allowFriendRequests: e.currentTarget.checked })}
                  class="w-5 h-5"
                />
              </label>
            </div>

            <div>
              <h3 class="text-white font-semibold mb-2">Mention Keywords</h3>
              <p class="text-xs text-xcord-text-muted mb-2">
                Get notified when someone mentions these keywords
              </p>

              <div class="space-y-2">
                <Show when={(notifStore.settings!.mentionKeywords ?? []).length > 0}>
                  <div class="flex flex-wrap gap-2">
                    {(notifStore.settings!.mentionKeywords ?? []).map((keyword) => (
                      <span class="bg-xcord-bg-primary text-xcord-text-primary px-2 py-1 rounded text-sm">
                        {keyword}
                      </span>
                    ))}
                  </div>
                </Show>

                <input
                  type="text"
                  placeholder="Add keyword..."
                  class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand"
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
            <h3 class="text-white font-semibold mb-2">Server Notification Overrides</h3>
            <p class="text-xs text-xcord-text-muted mb-3">
              Customise notification levels per server. Defaults to All Messages when no override is set.
            </p>

            <div class="space-y-2" aria-label="Server notification overrides">
              <For each={serverStore.servers}>
                {(server) => {
                  const currentLevel = () => getServerOverride(server.id);
                  const hasOverride = () => currentLevel() !== null;

                  return (
                    <div
                      class="flex items-center justify-between py-2 px-3 rounded bg-xcord-bg-primary"
                      aria-label={`Notification settings for ${server.name}`}
                    >
                      <span class="text-xcord-text-primary text-sm font-medium">{server.name}</span>

                      <div class="flex items-center gap-2">
                        <select
                          aria-label={`Notification level for ${server.name}`}
                          class="bg-xcord-bg-secondary text-xcord-text-primary text-sm rounded px-2 py-1 border border-xcord-border focus:outline-none focus:ring-2 focus:ring-xcord-brand"
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
                            class="text-xs text-xcord-text-muted hover:text-red-400 transition px-2 py-1 rounded hover:bg-xcord-bg-secondary"
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
