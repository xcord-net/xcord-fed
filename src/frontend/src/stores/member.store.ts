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

    async assignRole(serverId: string, userId: string, roleId: string): Promise<void> {
      await api.post(`/api/v1/servers/${serverId}/members/${userId}/roles/${roleId}`);
    },

    async removeRole(serverId: string, userId: string, roleId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/members/${userId}/roles/${roleId}`);
    },

    reset(): void {
      store.setMembers([]);
      store.setIsLoading(false);
    },
  };
}
