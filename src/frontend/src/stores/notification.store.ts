import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { NotificationSettings, ChannelNotificationOverride, NotificationSettingDto, NotificationLevel } from '../types/notification';

const store = createRoot(() => {
  const [settings, setSettings] = createSignal<NotificationSettings | null>(null);
  const [channelOverrides, setChannelOverrides] = createSignal<ChannelNotificationOverride[]>([]);
  const [serverOverrides, setServerOverrides] = createSignal<NotificationSettingDto[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    settings,
    setSettings,
    channelOverrides,
    setChannelOverrides,
    serverOverrides,
    setServerOverrides,
    isLoading,
    setIsLoading,
  };
});

export function useNotifications() {
  return {
    get settings() { return store.settings(); },
    get channelOverrides() { return store.channelOverrides(); },
    get serverOverrides() { return store.serverOverrides(); },
    get isLoading() { return store.isLoading(); },

    async loadSettings(): Promise<void> {
      store.setIsLoading(true);
      try {
        const settings = await api.get<NotificationSettings>('/api/v1/users/@me/notification-settings');
        store.setSettings(settings);
      } finally {
        store.setIsLoading(false);
      }
    },

    async updateSettings(updates: Partial<NotificationSettings>): Promise<void> {
      const updated = await api.put<NotificationSettings>('/api/v1/users/@me/notification-settings', updates);
      store.setSettings(updated);
    },

    async muteServer(serverId: string): Promise<void> {
      const current = store.settings();
      if (!current) return;

      const mutedServerIds = [...current.mutedServerIds, serverId];
      await this.updateSettings({ mutedServerIds });
    },

    async unmuteServer(serverId: string): Promise<void> {
      const current = store.settings();
      if (!current) return;

      const mutedServerIds = current.mutedServerIds.filter((id) => id !== serverId);
      await this.updateSettings({ mutedServerIds });
    },

    async muteChannel(channelId: string): Promise<void> {
      const current = store.settings();
      if (!current) return;

      const mutedChannelIds = [...current.mutedChannelIds, channelId];
      await this.updateSettings({ mutedChannelIds });
    },

    async unmuteChannel(channelId: string): Promise<void> {
      const current = store.settings();
      if (!current) return;

      const mutedChannelIds = current.mutedChannelIds.filter((id) => id !== channelId);
      await this.updateSettings({ mutedChannelIds });
    },

    async setChannelOverride(channelId: string, level: 'All' | 'Mentions' | 'Nothing'): Promise<void> {
      const override = await api.put<ChannelNotificationOverride>(
        `/api/v1/channels/${channelId}/notifications`,
        { level }
      );

      const overrides = store.channelOverrides().filter((o) => o.channelId !== channelId);
      store.setChannelOverrides([...overrides, override]);
    },

    async loadChannelOverrides(serverId: string): Promise<void> {
      const overrides = await api.get<ChannelNotificationOverride[]>(
        `/api/v1/servers/${serverId}/notification-overrides`
      );
      store.setChannelOverrides(overrides);
    },

    /** Load all server-level notification overrides for the current user. */
    async loadServerOverrides(): Promise<void> {
      const overrides = await api.get<NotificationSettingDto[]>('/api/v1/users/@me/notification-settings');
      // Filter to only server-level settings (serverId set, channelId null)
      const serverLevel = overrides.filter((o) => o.serverId !== null && o.channelId === null);
      store.setServerOverrides(serverLevel);
    },

    /** Create or update a server-level notification level override. */
    async setServerNotificationLevel(serverId: string, level: NotificationLevel): Promise<void> {
      const result = await api.put<NotificationSettingDto>('/api/v1/users/@me/notification-settings', {
        serverId,
        channelId: null,
        level,
        suppressEveryone: false,
        suppressRoles: false,
        muteUntil: null,
      });
      const overrides = store.serverOverrides().filter((o) => o.serverId !== serverId);
      store.setServerOverrides([...overrides, result]);
    },

    /** Delete a server-level notification override by its setting ID. */
    async deleteServerNotificationOverride(settingId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/notification-settings/${settingId}`);
      store.setServerOverrides(store.serverOverrides().filter((o) => o.id !== settingId));
    },

    reset(): void {
      store.setSettings(null);
      store.setChannelOverrides([]);
      store.setServerOverrides([]);
      store.setIsLoading(false);
    },
  };
}
