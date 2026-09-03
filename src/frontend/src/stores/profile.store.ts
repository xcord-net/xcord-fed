import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { UserProfile, ServerProfile } from '../types/profile';

const store = createRoot(() => {
  const [userProfile, setUserProfile] = createSignal<UserProfile | null>(null);
  const [serverProfiles, setServerProfiles] = createSignal<Map<string, ServerProfile>>(new Map());
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    userProfile,
    setUserProfile,
    serverProfiles,
    setServerProfiles,
    isLoading,
    setIsLoading,
  };
});

export function useProfiles() {
  return {
    get userProfile() { return store.userProfile(); },
    get serverProfiles() { return store.serverProfiles(); },
    get isLoading() { return store.isLoading(); },

    async loadUserProfile(): Promise<void> {
      store.setIsLoading(true);
      try {
        const profile = await api.get<UserProfile>('/api/v1/users/@me');
        store.setUserProfile(profile);
      } finally {
        store.setIsLoading(false);
      }
    },

    /**
     * Replace the cached profile with one the server has already confirmed.
     * For flows that upload through their own endpoint and come back holding
     * the finished profile, so the shell does not have to re-fetch it.
     */
    setUserProfile(profile: UserProfile): void {
      store.setUserProfile(profile);
    },

    async updateUserProfile(updates: Partial<UserProfile>): Promise<void> {
      const updated = await api.patch<UserProfile>('/api/v1/users/@me', updates);
      store.setUserProfile(updated);
    },

    async loadServerProfile(serverId: string): Promise<void> {
      const profile = await api.get<ServerProfile>(`/api/v1/servers/${serverId}/members/me`);
      const profiles = new Map(store.serverProfiles());
      profiles.set(serverId, profile);
      store.setServerProfiles(profiles);
    },

    async updateServerProfile(serverId: string, updates: Partial<ServerProfile>): Promise<void> {
      const updated = await api.patch<ServerProfile>(
        `/api/v1/servers/${serverId}/members/me`,
        updates
      );
      const profiles = new Map(store.serverProfiles());
      profiles.set(serverId, updated);
      store.setServerProfiles(profiles);
    },

    getServerProfile(serverId: string): ServerProfile | undefined {
      return store.serverProfiles().get(serverId);
    },

    reset(): void {
      store.setUserProfile(null);
      store.setServerProfiles(new Map());
      store.setIsLoading(false);
    },
  };
}
