import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Server } from '../types/server';

function normalizeServer(s: Server): Server {
  return normalizeIds(s, 'id', 'ownerId');
}

const store = createRoot(() => {
  const [servers, setServers] = createSignal<Server[]>([]);
  const [selectedServerId, setSelectedServerId] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);

  return { servers, setServers, selectedServerId, setSelectedServerId, isLoading, setIsLoading };
});

export function useServers() {
  return {
    get servers() { return store.servers(); },
    get selectedServerId() { return store.selectedServerId(); },
    get isLoading() { return store.isLoading(); },

    async fetchServers(): Promise<void> {
      store.setIsLoading(true);
      try {
        const response = await api.get<{ servers: Server[]; nextCursor?: string | null }>(
          '/api/v1/users/@me/servers',
        );
        const servers = response.servers ?? [];
        store.setServers(servers.map(normalizeServer));
      } finally {
        store.setIsLoading(false);
      }
    },

    async createServer(name: string, iconUrl?: string): Promise<Server> {
      const server = await api.post<Server>('/api/v1/servers', { name, iconUrl });
      const normalized = normalizeServer(server);
      store.setServers([...store.servers(), normalized]);
      return normalized;
    },

    async joinByInvite(code: string): Promise<Server> {
      const server = await api.post<Server>(`/api/v1/invites/${code}/accept`);
      const normalized = normalizeServer(server);
      store.setServers([...store.servers(), normalized]);
      return normalized;
    },

    updateServer(serverId: string, updates: Partial<Server>): void {
      store.setServers(store.servers().map(s =>
        s.id === serverId ? { ...s, ...updates } : s
      ));
    },

    async leaveServer(serverId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/members/@me`);
      store.setServers(store.servers().filter(s => s.id !== serverId));
      if (store.selectedServerId() === serverId) {
        store.setSelectedServerId(null);
      }
    },

    async deleteServer(serverId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}`);
      store.setServers(store.servers().filter(s => s.id !== serverId));
      if (store.selectedServerId() === serverId) {
        store.setSelectedServerId(null);
      }
    },

    selectServer(id: string | null): void {
      store.setSelectedServerId(id);
    },

    reset(): void {
      store.setServers([]);
      store.setSelectedServerId(null);
      store.setIsLoading(false);
    },
  };
}
