import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Channel, Category } from '../types/channel';

function normalizeChannel(c: Channel): Channel {
  return normalizeIds(c as unknown as Record<string, unknown>, 'id', 'serverId', 'conversationId', 'categoryId') as unknown as Channel;
}

function normalizeCategory(cat: Category): Category {
  return normalizeIds(cat as unknown as Record<string, unknown>, 'id', 'serverId') as unknown as Category;
}

const store = createRoot(() => {
  const [channels, setChannels] = createSignal<Channel[]>([]);
  const [categories, setCategories] = createSignal<Category[]>([]);
  const [selectedChannelId, setSelectedChannelId] = createSignal<string | null>(null);
  const [collapsedCategories, setCollapsedCategories] = createSignal<Set<string>>(new Set());
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    channels,
    setChannels,
    categories,
    setCategories,
    selectedChannelId,
    setSelectedChannelId,
    collapsedCategories,
    setCollapsedCategories,
    isLoading,
    setIsLoading,
  };
});

export function useChannels() {
  return {
    get channels() { return store.channels(); },
    get categories() { return store.categories(); },
    get selectedChannelId() { return store.selectedChannelId(); },
    get collapsedCategories() { return store.collapsedCategories(); },
    get isLoading() { return store.isLoading(); },

    async fetchChannels(serverId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        const response = await api.get<{ channels: Channel[]; categories: Category[] }>(
          `/api/v1/servers/${serverId}/channels`
        );
        store.setChannels(response.channels.map(normalizeChannel));
        store.setCategories((response.categories ?? []).map(normalizeCategory));
      } finally {
        store.setIsLoading(false);
      }
    },

    async createChannel(serverId: string, name: string, type: 'Text' | 'Voice' | 'Forum' = 'Text', categoryId?: string): Promise<Channel> {
      const body: Record<string, unknown> = { name, type };
      if (categoryId) body.categoryId = categoryId;
      const channel = await api.post<Channel>(`/api/v1/servers/${serverId}/channels`, body);
      const normalized = normalizeChannel(channel);
      // Guard against duplicates - the SignalR Chat_ChannelCreated broadcast may
      // have already added this channel to the store while the HTTP response was
      // in flight.
      if (!store.channels().some((c) => c.id === normalized.id)) {
        store.setChannels([...store.channels(), normalized]);
      }
      return normalized;
    },

    async createCategory(serverId: string, name: string): Promise<Category> {
      const category = await api.post<Category>(`/api/v1/servers/${serverId}/categories`, { name });
      const normalized = normalizeCategory(category);
      if (!store.categories().some((c) => c.id === normalized.id)) {
        store.setCategories([...store.categories(), normalized]);
      }
      return normalized;
    },

    async deleteCategory(serverId: string, categoryId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/categories/${categoryId}`);
      store.setCategories(store.categories().filter(c => c.id !== categoryId));
    },

    addChannel(channel: Channel): void {
      // Avoid duplicates - ignore if a channel with the same id already exists.
      if (store.channels().some((c) => c.id === channel.id)) return;
      store.setChannels([...store.channels(), channel]);
    },

    updateChannel(channelId: string, updates: Partial<Channel>): void {
      store.setChannels(store.channels().map(c =>
        c.id === channelId ? { ...c, ...updates } : c
      ));
    },

    async deleteChannel(channelId: string): Promise<void> {
      await api.delete(`/api/v1/channels/${channelId}`);
      store.setChannels(store.channels().filter(c => c.id !== channelId));
      if (store.selectedChannelId() === channelId) {
        store.setSelectedChannelId(null);
      }
    },

    selectChannel(id: string | null): void {
      store.setSelectedChannelId(id);
    },

    toggleCategory(categoryId: string): void {
      const collapsed = new Set(store.collapsedCategories());
      if (collapsed.has(categoryId)) {
        collapsed.delete(categoryId);
      } else {
        collapsed.add(categoryId);
      }
      store.setCollapsedCategories(collapsed);
    },

    reset(): void {
      store.setChannels([]);
      store.setCategories([]);
      store.setSelectedChannelId(null);
      store.setCollapsedCategories(new Set<string>());
      store.setIsLoading(false);
    },
  };
}
