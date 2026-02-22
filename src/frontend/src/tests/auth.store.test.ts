import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { useAuth } from '../stores/auth.store';

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('auth store', () => {
  describe('login', () => {
    it('should authenticate on successful login', async () => {
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/login')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              authenticated: true,
              userId: 'test-user-123',
              username: 'testuser',
              emailConfirmed: true,
            }),
          });
        }
        // Profile endpoint
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'test-user-123',
            username: 'testuser',
            displayName: 'Test User',
            avatarUrl: null,
          }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      await auth.login({ email: 'test@example.com', password: 'password123' });

      expect(auth.isAuthenticated).toBe(true);
      expect(auth.user).toBeTruthy();
    });
  });

  describe('logout', () => {
    it('should clear authentication state on logout', async () => {
      // First login
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/login')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              authenticated: true,
              userId: 'test-user-123',
              username: 'testuser',
              emailConfirmed: true,
            }),
          });
        }
        if (url.includes('/auth/logout')) {
          return Promise.resolve({ ok: true, status: 204 });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'test-user-123',
            username: 'testuser',
            displayName: 'Test User',
            avatarUrl: null,
          }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      await auth.login({ email: 'test@example.com', password: 'password123' });
      expect(auth.isAuthenticated).toBe(true);

      await auth.logout();
      expect(auth.isAuthenticated).toBe(false);
      expect(auth.user).toBeNull();
    });

    it('should clear state even if logout API fails', async () => {
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/login')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              authenticated: true,
              userId: 'test-user-123',
              username: 'testuser',
              emailConfirmed: true,
            }),
          });
        }
        if (url.includes('/auth/logout')) {
          return Promise.reject(new Error('Network error'));
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'test-user-123',
            username: 'testuser',
            displayName: 'Test User',
            avatarUrl: null,
          }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      await auth.login({ email: 'test@example.com', password: 'password123' });
      await auth.logout();

      expect(auth.isAuthenticated).toBe(false);
      expect(auth.user).toBeNull();
    });
  });

  describe('validateAuth', () => {
    it('should validate session via /auth/me', async () => {
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              userId: 'test-user-123',
              username: 'testuser',
              displayName: 'Test User',
              avatarUrl: null,
              emailConfirmed: true,
              twoFactorEnabled: false,
              isAdmin: false,
              isBot: false,
            }),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'test-user-123',
            username: 'testuser',
            displayName: 'Test User',
            avatarUrl: null,
          }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      const result = await auth.validateAuth();

      expect(result).toBe(true);
      expect(auth.isAuthenticated).toBe(true);
    });

    it('should try refresh if /auth/me fails, then succeed', async () => {
      let meCallCount = 0;
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          meCallCount++;
          if (meCallCount === 1) {
            return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
          }
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              userId: 'test-user-123',
              username: 'testuser',
              displayName: 'Test User',
              avatarUrl: null,
              emailConfirmed: true,
              twoFactorEnabled: false,
              isAdmin: false,
              isBot: false,
            }),
          });
        }
        if (url.includes('/auth/refresh')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ authenticated: true }),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'test-user-123',
            username: 'testuser',
            displayName: 'Test User',
            avatarUrl: null,
          }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      const result = await auth.validateAuth();

      expect(result).toBe(true);
      expect(auth.isAuthenticated).toBe(true);
    });

    it('should return false when both /auth/me and refresh fail', async () => {
      const mockFetch = vi.fn().mockImplementation(() => {
        return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
      });
      vi.stubGlobal('fetch', mockFetch);

      const auth = useAuth();
      const result = await auth.validateAuth();

      expect(result).toBe(false);
      expect(auth.isAuthenticated).toBe(false);
    });
  });
});
