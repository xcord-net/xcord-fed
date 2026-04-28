import { createSignal, createRoot } from 'solid-js';
import type { PresenceStatus } from '../types/presence';

const store = createRoot(() => {
  const [presenceMap, setPresenceMap] = createSignal<Map<string, PresenceStatus>>(new Map());

  return { presenceMap, setPresenceMap };
});

export function usePresence() {
  return {
    get presenceMap() { return store.presenceMap(); },

    updatePresence(userId: string, status: PresenceStatus): void {
      const map = new Map(store.presenceMap());
      map.set(userId, status);
      store.setPresenceMap(map);
    },

    getPresence(userId: string): PresenceStatus {
      return store.presenceMap().get(userId) || 'offline';
    },

    clearPresence(): void {
      store.setPresenceMap(new Map());
    },

    reset(): void {
      store.setPresenceMap(new Map());
    },
  };
}
