import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import { filterBans, paginateBans, totalBanPages } from '../components/BanManager';

interface BannedUser {
  userId: string;
  username: string;
  avatarUrl?: string;
  reason?: string;
  bannedAt: string;
}

describe('ban-manager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  const makeBan = (overrides?: Partial<BannedUser>): BannedUser => ({
    userId: 'user-1',
    username: 'BadActor',
    bannedAt: '2026-01-15T10:00:00Z',
    reason: 'Spamming',
    ...overrides,
  });

  describe('fetching banned users', () => {
    it('calls GET /api/v1/servers/{id}/bans', async () => {
      const serverId = 'server-abc';
      const mockBans: BannedUser[] = [makeBan()];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockBans,
      });

      const result = await api.get<BannedUser[]>(`/api/v1/servers/${serverId}/bans`);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/bans`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].username).toBe('BadActor');
    });

    it('returns empty array when server has no bans', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      const result = await api.get<BannedUser[]>('/api/v1/servers/server-empty/bans');
      expect(result).toHaveLength(0);
    });

    it('throws when API returns an error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      await expect(
        api.get('/api/v1/servers/server-x/bans'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });

    it('includes avatarUrl and reason when present', async () => {
      const ban = makeBan({ avatarUrl: 'https://cdn.example.com/avatar.png', reason: 'Harassment' });
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [ban],
      });

      const result = await api.get<BannedUser[]>('/api/v1/servers/server-1/bans');
      expect(result[0].avatarUrl).toBe('https://cdn.example.com/avatar.png');
      expect(result[0].reason).toBe('Harassment');
    });
  });

  describe('unban user', () => {
    it('calls DELETE /api/v1/servers/{id}/bans/{userId}', async () => {
      const serverId = 'server-abc';
      const userId = 'user-banned-1';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      await api.delete(`/api/v1/servers/${serverId}/bans/${userId}`);

      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/bans/${userId}`,
        expect.objectContaining({ method: 'DELETE' }),
      );
    });

    it('throws when unban API call fails', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      await expect(
        api.delete('/api/v1/servers/server-abc/bans/nonexistent-user'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  describe('search/filter logic', () => {
    const bans: BannedUser[] = [
      makeBan({ userId: '1', username: 'Alice' }),
      makeBan({ userId: '2', username: 'Bob' }),
      makeBan({ userId: '3', username: 'alice_backup' }),
    ];

    it('returns all bans when search query is empty', () => {
      const result = filterBans(bans, '');
      expect(result).toHaveLength(3);
    });

    it('filters bans case-insensitively by username', () => {
      const result = filterBans(bans, 'alice');
      expect(result).toHaveLength(2);
      expect(result.map((b) => b.username)).toEqual(['Alice', 'alice_backup']);
    });

    it('returns empty array when no usernames match', () => {
      const result = filterBans(bans, 'zzz_nomatch');
      expect(result).toHaveLength(0);
    });

    it('partial match works mid-username', () => {
      const result = filterBans(bans, 'ob');
      expect(result).toHaveLength(1);
      expect(result[0].username).toBe('Bob');
    });
  });

  describe('pagination logic', () => {
    it('paginates to the correct page', () => {
      const allBans = Array.from({ length: 35 }, (_, i) =>
        makeBan({ userId: `u${i}`, username: `User${i}` }),
      );

      const page1 = paginateBans(allBans, 1, 20);
      const page2 = paginateBans(allBans, 2, 20);

      expect(page1).toHaveLength(20);
      expect(page2).toHaveLength(15);
      expect(page1[0].userId).toBe('u0');
      expect(page2[0].userId).toBe('u20');
    });

  });

});
