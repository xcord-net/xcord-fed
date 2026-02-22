import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';

export interface BlockedUser {
  blockerId: string;
  blockedId: string;
  blockedUsername: string;
  blockedDisplayName: string;
  blockedAvatarUrl?: string;
  createdAt: string;
}

const store = createRoot(() => {
  const [blockedUsers, setBlockedUsers] = createSignal<BlockedUser[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    blockedUsers,
    setBlockedUsers,
    isLoading,
    setIsLoading,
  };
});

export function useBlocks() {
  return {
    get blockedUsers() { return store.blockedUsers(); },
    get isLoading() { return store.isLoading(); },

    async loadBlockedUsers(): Promise<void> {
      store.setIsLoading(true);
      try {
        const blocked = await api.get<BlockedUser[]>('/api/v1/users/@me/blocks');
        store.setBlockedUsers(blocked);
      } finally {
        store.setIsLoading(false);
      }
    },

    async blockUser(userId: string): Promise<void> {
      const blocked = await api.put<BlockedUser>(`/api/v1/users/@me/blocks/${userId}`, {});
      store.setBlockedUsers([...store.blockedUsers(), blocked]);
    },

    async blockUserByUsername(username: string): Promise<void> {
      const blocked = await api.post<BlockedUser>('/api/v1/users/@me/blocks', { username });
      store.setBlockedUsers([...store.blockedUsers(), blocked]);
    },

    async unblockUser(userId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/blocks/${userId}`);
      store.setBlockedUsers(store.blockedUsers().filter((u) => u.blockedId !== userId));
    },

    isBlocked(userId: string): boolean {
      return store.blockedUsers().some((u) => u.blockedId === userId);
    },

    reset(): void {
      store.setBlockedUsers([]);
      store.setIsLoading(false);
    },
  };
}
