import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { LoginRequest, RegisterRequest, AuthResponse, User, UserInfo } from '../types/auth';
import { resetAllStores } from './index';
import { useProfiles } from './profile.store';

const store = createRoot(() => {
  const [user, setUser] = createSignal<User | null>(null);
  const [isAuthenticated, setIsAuthenticated] = createSignal(false);
  const [isLoading, setIsLoading] = createSignal(true);

  return { user, setUser, isAuthenticated, setIsAuthenticated, isLoading, setIsLoading };
});

async function fetchAndStoreProfile(): Promise<void> {
  const profiles = useProfiles();
  try {
    await profiles.loadUserProfile();
    const profile = profiles.userProfile;
    if (profile) {
      store.setUser({
        id: profile.userId,
        username: profile.username,
        email: '',
        avatarUrl: profile.avatarUrl,
      });
    }
  } catch {
    // Profile fetch failure is non-fatal — user is still authenticated
  }
}

export function useAuth() {
  return {
    get user() { return store.user(); },
    get isAuthenticated() { return store.isAuthenticated(); },
    get isLoading() { return store.isLoading(); },

    async login(request: LoginRequest): Promise<void> {
      const response = await api.post<AuthResponse>('/api/v1/auth/login', request);
      if (response.authenticated) {
        api.setAuthenticated(true);
        store.setIsAuthenticated(true);
        store.setUser({
          id: String(response.userId),
          username: response.username ?? '',
          email: '',
        });
        await fetchAndStoreProfile();
      }
    },

    async register(request: RegisterRequest): Promise<void> {
      const response = await api.post<AuthResponse>('/api/v1/auth/register', request);
      if (response.authenticated) {
        api.setAuthenticated(true);
        store.setIsAuthenticated(true);
        store.setUser({
          id: String(response.userId),
          username: response.username ?? '',
          email: '',
        });
        await fetchAndStoreProfile();
      }
    },

    async logout(): Promise<void> {
      try {
        await api.post('/api/v1/auth/logout');
      } catch {
        // Ignore logout API failures - clean up locally regardless
      } finally {
        api.setAuthenticated(false);
        store.setUser(null);
        store.setIsAuthenticated(false);
        // Reset all stores to prevent data leakage between sessions
        await resetAllStores();
      }
    },

    async validateAuth(): Promise<boolean> {
      store.setIsLoading(true);
      try {
        // Check if session is valid by calling /auth/me (cookie sent automatically)
        const userInfo = await api.get<UserInfo>('/api/v1/auth/me');
        api.setAuthenticated(true);
        store.setIsAuthenticated(true);
        store.setUser({
          id: String(userInfo.userId),
          username: userInfo.username,
          email: '',
          avatarUrl: userInfo.avatarUrl ?? undefined,
        });

        // Fetch full profile data
        const profileLoaded = useProfiles().userProfile;
        if (!profileLoaded) {
          await fetchAndStoreProfile();
        }
        return true;
      } catch {
        // Session invalid or expired — try refresh
        try {
          await api.post('/api/v1/auth/refresh');
          // Refresh succeeded — now fetch user info
          const userInfo = await api.get<UserInfo>('/api/v1/auth/me');
          api.setAuthenticated(true);
          store.setIsAuthenticated(true);
          store.setUser({
            id: String(userInfo.userId),
            username: userInfo.username,
            email: '',
            avatarUrl: userInfo.avatarUrl ?? undefined,
          });
          await fetchAndStoreProfile();
          return true;
        } catch {
          api.setAuthenticated(false);
          store.setIsAuthenticated(false);
          return false;
        }
      } finally {
        store.setIsLoading(false);
      }
    },
  };
}
