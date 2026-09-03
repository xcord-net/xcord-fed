import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { Member } from '../types/member';

const store = createRoot(() => {
  const [members, setMembers] = createSignal<Member[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);

  return { members, setMembers, isLoading, setIsLoading };
});

export function useMembers() {
  return {
    get members() { return store.members(); },
    get isLoading() { return store.isLoading(); },

    async fetchMembers(serverId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        const members = await api.get<Member[]>(`/api/v1/servers/${serverId}/members`);
        store.setMembers(members);
      } finally {
        store.setIsLoading(false);
      }
    },

    async banMember(serverId: string, userId: string, reason?: string): Promise<void> {
      await api.post(`/api/v1/servers/${serverId}/bans`, { userId, reason });
      store.setMembers(store.members().filter(m => m.userId !== userId));
    },

    /**
     * Drop someone from the roster on screen, in response to a realtime event.
     *
     * Leaving, being kicked and being banned all reach other people this way.
     * Without it the roster only ever changed for whoever performed the action;
     * everyone else went on seeing the departed member until they reloaded.
     */
    removeMember(userId: string): void {
      store.setMembers(store.members().filter(m => m.userId !== userId));
    },

    /** Refetch the roster after someone joins, which the event does not carry. */
    async refreshMembers(serverId: string): Promise<void> {
      await this.fetchMembers(serverId).catch(() => undefined);
    },

    async assignGroup(serverId: string, userId: string, groupId: string): Promise<void> {
      await api.post(`/api/v1/servers/${serverId}/members/${userId}/groups/${groupId}`);
    },

    async removeGroup(serverId: string, userId: string, groupId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/members/${userId}/groups/${groupId}`);
    },

    reset(): void {
      store.setMembers([]);
      store.setIsLoading(false);
    },
  };
}
