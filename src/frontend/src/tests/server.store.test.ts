import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useServers } from '../stores/server.store';

describe('server.store', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    // Reset singleton store state by fetching empty data
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [],
    });
    const servers = useServers();
    await servers.fetchServers();
    vi.clearAllMocks();
  });

  describe('fetchServers', () => {
    it('should load servers from API', async () => {
      // Arrange
      const mockServers = [
        { id: '1', name: 'Server 1', iconUrl: null },
        { id: '2', name: 'Server 2', iconUrl: 'https://example.com/icon.png' },
      ];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockServers,
      });

      const servers = useServers();

      // Act
      await servers.fetchServers();

      // Assert
      expect(servers.servers.length).toBe(2);
      expect(servers.servers[0].name).toBe('Server 1');
      expect(servers.servers[1].name).toBe('Server 2');
    });

    it('should handle fetch errors', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      const servers = useServers();

      // Act & Assert
      await expect(servers.fetchServers()).rejects.toThrow();
    });
  });

  describe('createServer', () => {
    it('should add new server to store', async () => {
      // Arrange
      const newServer = { id: '3', name: 'New Server', iconUrl: null };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => newServer,
      });

      const servers = useServers();

      // Act
      const result = await servers.createServer('New Server');

      // Assert
      expect(result.id).toBe('3');
      expect(servers.servers.length).toBe(1);
      expect(servers.servers[0].name).toBe('New Server');
    });
  });

  describe('selectServer', () => {
    it('should update selected server ID', () => {
      // Arrange
      const servers = useServers();

      // Act
      servers.selectServer('server-123');

      // Assert
      expect(servers.selectedServerId).toBe('server-123');
    });

    it('should allow deselecting server', () => {
      // Arrange
      const servers = useServers();
      servers.selectServer('server-123');

      // Act
      servers.selectServer(null);

      // Assert
      expect(servers.selectedServerId).toBeNull();
    });
  });

  describe('joinByInvite', () => {
    it('should add server to store when joining via invite', async () => {
      // Arrange
      const inviteCode = 'abc123';
      const joinedServer = { id: '4', name: 'Invited Server', iconUrl: null };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => joinedServer,
      });

      const servers = useServers();

      // Act
      const result = await servers.joinByInvite(inviteCode);

      // Assert
      expect(result.id).toBe('4');
      expect(servers.servers.length).toBe(1);
      expect(servers.servers[0].name).toBe('Invited Server');
    });

    it('should handle invalid invite codes', async () => {
      // Arrange
      const inviteCode = 'invalid';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Invalid invite code' }),
      });

      const servers = useServers();

      // Act & Assert
      await expect(servers.joinByInvite(inviteCode)).rejects.toThrow();
    });
  });
});
