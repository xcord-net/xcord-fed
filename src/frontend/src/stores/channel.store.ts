import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Channel, Category } from '../types/channel';
import { parseCapabilities } from '../types/channel';

function normalizeChannel(c: Channel): Channel {
  const normalized = normalizeIds(c, 'id', 'serverId', 'conversationId', 'categoryId');
  // API returns capabilities as string enum names ("Chat", "Chat, Forum"); convert to bitfield
  normalized.capabilities = parseCapabilities(normalized.capabilities as unknown as string | number);
  return normalized;
}

function normalizeCategory(cat: Category): Category {
  return normalizeIds(cat, 'id', 'serverId');
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

/**
 * Ordering guards for the channel list.
 *
 * `latestChannelFetch` discards a fetch response overtaken by a newer fetch.
 * `channelListVersion` discards one overtaken by a *local* change - creating or
 * deleting a channel, or a SignalR push - because a list fetched before that
 * change cannot describe the world after it. Both are needed: the first orders
 * fetches against each other, the second orders them against edits.
 */
let latestChannelFetch = 0;
let channelListVersion = 0;

/** Called by everything that edits the list, so an in-flight fetch cannot undo it. */
function markChannelListChanged(): void {
  channelListVersion++;
}

export function useChannels() {
  return {
    get channels() { return store.channels(); },
    get categories() { return store.categories(); },
    get selectedChannelId() { return store.selectedChannelId(); },
    get collapsedCategories() { return store.collapsedCategories(); },
    get isLoading() { return store.isLoading(); },

    /**
     * Load a server's channels and categories, replacing whatever is held.
     *
     * A response is only applied if it is still the newest one asked for. Two
     * fetches are easy to have in flight at once - the route effect starts one
     * whenever the server changes, and callers start their own - and the list
     * they return is a snapshot of the moment each request was served. Applying
     * an older snapshot last silently undid anything that happened in between:
     * create a channel while a fetch from before it was in flight, and the
     * channel appeared in the sidebar and then vanished, with the server holding
     * a channel the sidebar denied existed until the next reload.
     */
    async fetchChannels(serverId: string): Promise<void> {
      const request = ++latestChannelFetch;
      const versionAtStart = channelListVersion;
      store.setIsLoading(true);
      try {
        const response = await api.get<{ channels: Channel[]; categories: Category[] }>(
          `/api/v1/servers/${serverId}/channels`
        );
        if (request !== latestChannelFetch || versionAtStart !== channelListVersion) return;
        store.setChannels(response.channels.map(normalizeChannel));
        store.setCategories((response.categories ?? []).map(normalizeCategory));
      } finally {
        if (request === latestChannelFetch) store.setIsLoading(false);
      }
    },

    async createChannel(serverId: string, name: string, capabilities: number, accessGroupId?: string, categoryId?: string): Promise<Channel> {
      const body: Record<string, unknown> = { name, capabilities };
      if (accessGroupId) body.accessGroupId = accessGroupId;
      if (categoryId) body.categoryId = categoryId;
      const channel = await api.post<Channel>(`/api/v1/servers/${serverId}/channels`, body);
      const normalized = normalizeChannel(channel);
      markChannelListChanged();
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
      markChannelListChanged();
      if (!store.categories().some((c) => c.id === normalized.id)) {
        store.setCategories([...store.categories(), normalized]);
      }
      return normalized;
    },

    async deleteCategory(serverId: string, categoryId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/categories/${categoryId}`);
      markChannelListChanged();
      store.setCategories(store.categories().filter(c => c.id !== categoryId));
    },

    addChannel(channel: Channel): void {
      // Avoid duplicates - ignore if a channel with the same id already exists.
      if (store.channels().some((c) => c.id === channel.id)) return;
      const normalized = normalizeChannel(channel);
      markChannelListChanged();
      store.setChannels([...store.channels(), normalized]);
    },

    updateChannel(channelId: string, updates: Partial<Channel>): void {
      markChannelListChanged();
      store.setChannels(store.channels().map(c =>
        c.id === channelId ? { ...c, ...updates } : c
      ));
    },

    async deleteChannel(channelId: string): Promise<void> {
      await api.delete(`/api/v1/channels/${channelId}`);
      markChannelListChanged();
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
      markChannelListChanged();
      store.setChannels([]);
      store.setCategories([]);
      store.setSelectedChannelId(null);
      store.setCollapsedCategories(new Set<string>());
      store.setIsLoading(false);
    },
  };
}
