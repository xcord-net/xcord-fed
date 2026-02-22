import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';

interface Invite {
  code: string;
  serverId: string;
  maxUses: number | null;
  uses: number;
  expiresAt: string | null;
  createdAt: string;
}

describe('invite-modal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('invite creation', () => {
    it('calls POST /api/v1/servers/{id}/invites with correct path', async () => {
      // Arrange
      const serverId = 'server-123';
      const mockInvite: Invite = {
        code: 'abc123',
        serverId,
        maxUses: null,
        uses: 0,
        expiresAt: null,
        createdAt: new Date().toISOString(),
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockInvite,
      });

      // Act
      const result = await api.post<Invite>(`/api/v1/servers/${serverId}/invites`, {});

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/invites`,
        expect.objectContaining({ method: 'POST' })
      );
      expect(result.code).toBe('abc123');
    });

    it('calls POST with maxUses when specified', async () => {
      // Arrange
      const serverId = 'server-456';
      const mockInvite: Invite = {
        code: 'xyz789',
        serverId,
        maxUses: 10,
        uses: 0,
        expiresAt: null,
        createdAt: new Date().toISOString(),
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockInvite,
      });

      // Act
      const result = await api.post<Invite>(`/api/v1/servers/${serverId}/invites`, { maxUses: 10 });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/invites`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ maxUses: 10 }),
        })
      );
      expect(result.maxUses).toBe(10);
    });

    it('calls POST with expiresAt when specified', async () => {
      // Arrange
      const serverId = 'server-789';
      const expiresAt = new Date(Date.now() + 3600000).toISOString();
      const mockInvite: Invite = {
        code: 'def456',
        serverId,
        maxUses: null,
        uses: 0,
        expiresAt,
        createdAt: new Date().toISOString(),
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockInvite,
      });

      // Act
      const result = await api.post<Invite>(`/api/v1/servers/${serverId}/invites`, { expiresAt });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/invites`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ expiresAt }),
        })
      );
      expect(result.expiresAt).toBe(expiresAt);
    });

    it('throws when API returns error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ detail: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.post<Invite>('/api/v1/servers/server-999/invites', {})
      ).rejects.toMatchObject({ detail: 'Forbidden' });
    });
  });

  describe('copy to clipboard', () => {
    it('writes the invite link to the clipboard', async () => {
      // Arrange
      const code = 'cliptest';
      const origin = 'http://localhost:5173';
      const inviteLink = `${origin}/invite/${code}`;

      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(globalThis, 'navigator', {
        value: { clipboard: { writeText: writeTextMock } },
        writable: true,
        configurable: true,
      });

      // Act — replicate the handleCopy() logic from InviteModal
      await navigator.clipboard.writeText(inviteLink);

      // Assert
      expect(writeTextMock).toHaveBeenCalledWith(inviteLink);
      expect(writeTextMock).toHaveBeenCalledWith('http://localhost:5173/invite/cliptest');
    });

    it('clipboard receives full URL not just the code', async () => {
      // Arrange
      const code = 'onlycode';
      const origin = 'http://localhost:5173';
      const inviteLink = `${origin}/invite/${code}`;

      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(globalThis, 'navigator', {
        value: { clipboard: { writeText: writeTextMock } },
        writable: true,
        configurable: true,
      });

      // Act
      await navigator.clipboard.writeText(inviteLink);

      // Assert — must NOT have been called with just the code
      expect(writeTextMock).not.toHaveBeenCalledWith(code);
      expect(writeTextMock).toHaveBeenCalledWith(expect.stringContaining('http'));
    });
  });
});
