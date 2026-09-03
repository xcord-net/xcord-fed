import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';

export interface UnreadInfo {
  count: number;
  lastMessageId: string;
}

const store = createRoot(() => {
  const [unreadMap, setUnreadMap] = createSignal<Map<string, UnreadInfo>>(new Map());

  return { unreadMap, setUnreadMap };
});

export function useUnread() {
  return {
    get unreadMap() { return store.unreadMap(); },

    /**
     * Mark a conversation read.
     *
     * Kept at zero rather than forgotten. Consumers fall back to a server-side
     * aggregate when this store knows nothing about a conversation, so deleting
     * the entry made "you have read this" indistinguishable from "no idea", and
     * the badge sprang straight back to whatever the last aggregate said.
     */
    markRead(conversationId: string): void {
      const map = new Map(store.unreadMap());
      const lastMessageId = map.get(conversationId)?.lastMessageId ?? '';
      map.set(conversationId, { count: 0, lastMessageId });
      store.setUnreadMap(map);

      // Tell the server too. Clearing this only in memory meant the badge came
      // straight back: the next unread push, or the next time the deck aggregate
      // was fetched, still described the conversation as unread because nothing
      // had ever said otherwise. The endpoint has always been there.
      if (!lastMessageId) return;
      void api.put(`/api/v1/conversations/${conversationId}/read-state`, { messageId: lastMessageId })
        .catch(() => { /* non-fatal: the local clear still stands */ });
    },

    updateUnread(conversationId: string, count: number, lastMessageId: string): void {
      const map = new Map(store.unreadMap());
      map.set(conversationId, { count, lastMessageId });
      store.setUnreadMap(map);
    },

    getUnreadCount(conversationId: string): number {
      return store.unreadMap().get(conversationId)?.count || 0;
    },

    /** Whether this store has a first-hand count for a conversation. */
    isTracked(conversationId: string): boolean {
      return store.unreadMap().has(conversationId);
    },

    getTotalUnreadCount(): number {
      let total = 0;
      store.unreadMap().forEach(info => {
        total += info.count;
      });
      return total;
    },

    clearUnreads(): void {
      store.setUnreadMap(new Map());
    },

    reset(): void {
      store.setUnreadMap(new Map());
    },
  };
}
