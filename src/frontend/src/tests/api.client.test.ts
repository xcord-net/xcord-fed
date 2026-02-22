import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { api } from '../api/client';

beforeEach(() => {
  api.setAuthenticated(false);
  vi.restoreAllMocks();
});

describe('ApiClient', () => {
  describe('setAuthenticated / isAuthenticated', () => {
    it('should track authentication state', () => {
      api.setAuthenticated(true);
      expect(api.isAuthenticated()).toBe(true);
    });

    it('should clear authentication state', () => {
      api.setAuthenticated(true);
      api.setAuthenticated(false);
      expect(api.isAuthenticated()).toBe(false);
    });
  });

  describe('request methods', () => {
    it('should send requests with credentials: include', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: 'test' }),
      });
      vi.stubGlobal('fetch', mockFetch);
      api.setAuthenticated(true);

      await api.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/v1/test',
        expect.objectContaining({
          credentials: 'include',
        })
      );
    });

    it('should not include Authorization header', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: 'test' }),
      });
      vi.stubGlobal('fetch', mockFetch);
      api.setAuthenticated(true);

      await api.get('/api/v1/test');

      const callHeaders = mockFetch.mock.calls[0][1].headers;
      expect(callHeaders['Authorization']).toBeUndefined();
    });

    it('should handle POST with body', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ id: 1 }),
      });
      vi.stubGlobal('fetch', mockFetch);
      api.setAuthenticated(true);

      await api.post('/api/v1/test', { name: 'test' });

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/v1/test',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ name: 'test' }),
        })
      );
    });

    it('should handle 204 No Content', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      }));
      api.setAuthenticated(true);

      const result = await api.delete('/api/v1/test/1');
      expect(result).toBeUndefined();
    });

    it('should throw on error responses', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'Bad request' }),
      }));
      api.setAuthenticated(true);

      await expect(api.get('/api/v1/test')).rejects.toEqual({ error: 'Bad request' });
    });
  });

  describe('token refresh on 401', () => {
    it('should retry request after successful refresh', async () => {
      api.setAuthenticated(true);

      let callCount = 0;
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/refresh')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ authenticated: true }),
          });
        }
        callCount++;
        if (callCount === 1) {
          return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ data: 'success' }),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      const result = await api.get<{ data: string }>('/api/v1/test');
      expect(result.data).toBe('success');
      expect(api.isAuthenticated()).toBe(true);
    });

    it('should redirect to login on refresh failure', async () => {
      api.setAuthenticated(true);

      const mockLocation = { href: '' };
      vi.stubGlobal('window', { location: mockLocation });

      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/refresh')) {
          return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
        }
        return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
      });
      vi.stubGlobal('fetch', mockFetch);

      await expect(api.get('/api/v1/test')).rejects.toThrow('Session expired');
      expect(api.isAuthenticated()).toBe(false);
      expect(mockLocation.href).toBe('/login');
    });

    it('should redirect to login on double 401', async () => {
      api.setAuthenticated(true);

      const mockLocation = { href: '' };
      vi.stubGlobal('window', { location: mockLocation });

      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/refresh')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ authenticated: true }),
          });
        }
        return Promise.resolve({ ok: false, status: 401, json: () => Promise.resolve({}) });
      });
      vi.stubGlobal('fetch', mockFetch);

      await expect(api.get('/api/v1/test')).rejects.toThrow('Session expired');
      expect(api.isAuthenticated()).toBe(false);
      expect(mockLocation.href).toBe('/login');
    });

    it('should deduplicate concurrent refresh attempts', async () => {
      api.setAuthenticated(true);

      let refreshCount = 0;
      const mockFetch = vi.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/refresh')) {
          refreshCount++;
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ authenticated: true }),
          });
        }
        return Promise.resolve({
          ok: false,
          status: 401,
          json: () => Promise.resolve({}),
        });
      });
      vi.stubGlobal('fetch', mockFetch);

      // Fire two requests simultaneously that both get 401
      const results = await Promise.allSettled([
        api.get('/api/v1/test1'),
        api.get('/api/v1/test2'),
      ]);

      // Only one refresh call should have been made
      expect(refreshCount).toBe(1);
    });
  });
});
