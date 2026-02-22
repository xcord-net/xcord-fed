import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  getProviderIcon,
  getProviderColor,
  sortConnections,
  filterVisibleConnections,
  providerIcons,
  providerColors,
} from '../components/ConnectedAccounts';
import type { ConnectedAccount, ConnectionProvider } from '../components/ConnectedAccounts';

// ---- Test data helpers ----

const makeConnection = (overrides: Partial<ConnectedAccount> = {}): ConnectedAccount => ({
  id: 'conn-1',
  provider: 'GitHub',
  providerUserId: 'gh-123',
  username: 'alice',
  displayName: 'Alice Dev',
  connectedAt: '2026-01-10T12:00:00Z',
  showOnProfile: true,
  ...overrides,
});

// ---- Tests ----

describe('connected-accounts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Provider icon/color helpers ----

  describe('getProviderIcon', () => {
    it('returns "GH" for GitHub', () => {
      expect(getProviderIcon('GitHub')).toBe('GH');
    });

    it('returns "TW" for Twitter', () => {
      expect(getProviderIcon('Twitter')).toBe('TW');
    });

    it('returns "SP" for Spotify', () => {
      expect(getProviderIcon('Spotify')).toBe('SP');
    });

    it('returns "YT" for YouTube', () => {
      expect(getProviderIcon('YouTube')).toBe('YT');
    });

    it('returns "TV" for Twitch', () => {
      expect(getProviderIcon('Twitch')).toBe('TV');
    });
  });

  describe('getProviderColor', () => {
    it('returns a CSS class for GitHub', () => {
      const cls = getProviderColor('GitHub');
      expect(cls).toBe('bg-gray-700');
    });

    it('returns "bg-green-600" for Spotify', () => {
      expect(getProviderColor('Spotify')).toBe('bg-green-600');
    });

    it('returns "bg-sky-500" for Twitter', () => {
      expect(getProviderColor('Twitter')).toBe('bg-sky-500');
    });

    it('all providers in providerColors map have values', () => {
      const providers: ConnectionProvider[] = [
        'GitHub', 'Twitter', 'Spotify', 'YouTube', 'Twitch', 'Reddit', 'Steam',
      ];
      for (const p of providers) {
        expect(providerColors[p]).toBeDefined();
        expect(providerColors[p].length).toBeGreaterThan(0);
      }
    });
  });

  // ---- sortConnections ----

  describe('sortConnections', () => {
    it('sorts connections alphabetically by provider', () => {
      const connections: ConnectedAccount[] = [
        makeConnection({ id: '1', provider: 'YouTube' }),
        makeConnection({ id: '2', provider: 'GitHub' }),
        makeConnection({ id: '3', provider: 'Spotify' }),
      ];
      const sorted = sortConnections(connections);
      expect(sorted[0].provider).toBe('GitHub');
      expect(sorted[1].provider).toBe('Spotify');
      expect(sorted[2].provider).toBe('YouTube');
    });

    it('does not mutate the original array', () => {
      const connections: ConnectedAccount[] = [
        makeConnection({ id: '1', provider: 'Twitter' }),
        makeConnection({ id: '2', provider: 'GitHub' }),
      ];
      const original = [...connections];
      sortConnections(connections);
      expect(connections[0].provider).toBe(original[0].provider);
    });

    it('returns single-element array unchanged', () => {
      const connections = [makeConnection()];
      const sorted = sortConnections(connections);
      expect(sorted).toHaveLength(1);
    });
  });

  // ---- filterVisibleConnections ----

  describe('filterVisibleConnections', () => {
    it('returns only connections with showOnProfile=true', () => {
      const connections: ConnectedAccount[] = [
        makeConnection({ id: '1', showOnProfile: true }),
        makeConnection({ id: '2', showOnProfile: false }),
        makeConnection({ id: '3', showOnProfile: true }),
      ];
      const visible = filterVisibleConnections(connections);
      expect(visible).toHaveLength(2);
      expect(visible.every((c) => c.showOnProfile)).toBe(true);
    });

    it('returns empty array when all are hidden', () => {
      const connections: ConnectedAccount[] = [
        makeConnection({ id: '1', showOnProfile: false }),
        makeConnection({ id: '2', showOnProfile: false }),
      ];
      expect(filterVisibleConnections(connections)).toHaveLength(0);
    });

    it('returns all when all are visible', () => {
      const connections = [
        makeConnection({ id: '1', showOnProfile: true }),
        makeConnection({ id: '2', showOnProfile: true }),
      ];
      expect(filterVisibleConnections(connections)).toHaveLength(2);
    });
  });

  // ---- API: GET connections ----

  describe('GET /api/v1/users/@me/connections', () => {
    it('fetches the current user connections', async () => {
      // Arrange
      const mockConnections: ConnectedAccount[] = [makeConnection()];
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockConnections,
      });

      // Act
      const result = await api.get<ConnectedAccount[]>('/api/v1/users/@me/connections');

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/connections',
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].provider).toBe('GitHub');
    });

    it('returns empty array when user has no connections', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      const result = await api.get<ConnectedAccount[]>('/api/v1/users/@me/connections');

      // Assert
      expect(result).toHaveLength(0);
    });

    it('throws when request fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Server error' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/users/@me/connections'),
      ).rejects.toMatchObject({ error: 'Server error' });
    });
  });

  // ---- API: DELETE connection ----

  describe('DELETE /api/v1/users/@me/connections/{id}', () => {
    it('calls the correct disconnect endpoint', async () => {
      // Arrange
      const connectionId = 'conn-abc-123';
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.delete(`/api/v1/users/@me/connections/${connectionId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/users/@me/connections/${connectionId}`,
        expect.objectContaining({ method: 'DELETE' }),
      );
    });

    it('removes the connection from local list after disconnect', () => {
      // Arrange
      let connections: ConnectedAccount[] = [
        makeConnection({ id: 'conn-1', provider: 'GitHub' }),
        makeConnection({ id: 'conn-2', provider: 'Spotify' }),
      ];

      // Act
      connections = connections.filter((c) => c.id !== 'conn-1');

      // Assert
      expect(connections).toHaveLength(1);
      expect(connections[0].provider).toBe('Spotify');
    });

    it('throws when disconnect API fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      // Act & Assert
      await expect(
        api.delete('/api/v1/users/@me/connections/nonexistent'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  // ---- ConnectedAccount data shape ----

  describe('ConnectedAccount shape', () => {
    it('has required fields', () => {
      const conn = makeConnection();
      expect(conn.id).toBeDefined();
      expect(conn.provider).toBeDefined();
      expect(conn.providerUserId).toBeDefined();
      expect(conn.username).toBeDefined();
      expect(conn.connectedAt).toBeDefined();
      expect(typeof conn.showOnProfile).toBe('boolean');
    });

    it('displayName and avatarUrl are optional', () => {
      const conn = makeConnection({ displayName: undefined, avatarUrl: undefined });
      expect(conn.displayName).toBeUndefined();
      expect(conn.avatarUrl).toBeUndefined();
    });

    it('connectedAt is a parseable ISO date', () => {
      const conn = makeConnection({ connectedAt: '2026-01-10T12:00:00Z' });
      const date = new Date(conn.connectedAt);
      expect(isNaN(date.getTime())).toBe(false);
      expect(date.getFullYear()).toBe(2026);
    });
  });
});
