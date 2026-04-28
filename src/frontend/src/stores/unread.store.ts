import { createSignal, createRoot } from 'solid-js';

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

    markRead(conversationId: string): void {
      const map = new Map(store.unreadMap());
      map.delete(conversationId);
      store.setUnreadMap(map);
    },

    updateUnread(conversationId: string, count: number, lastMessageId: string): void {
      const map = new Map(store.unreadMap());
      if (count > 0) {
        map.set(conversationId, { count, lastMessageId });
      } else {
        map.delete(conversationId);
      }
      store.setUnreadMap(map);
    },

    getUnreadCount(conversationId: string): number {
      return store.unreadMap().get(conversationId)?.count || 0;
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
