import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { validateServerName } from '../components/OwnershipTransfer';

// Logic mirroring OwnershipTransfer.tsx

interface Member {
  userId: string;
  username: string;
  displayName?: string;
}

async function loadMembers(
  serverId: string,
  currentUserId: string
): Promise<{ members: Member[]; error: string }> {
  try {
    const data = await api.get<Member[]>(`/api/v1/servers/${serverId}/members`);
    return {
      members: data.filter((m) => m.userId !== currentUserId),
      error: '',
    };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { members: [], error: errObj?.error || 'Failed to load members' };
  }
}

async function transferOwnership(
  serverId: string,
  targetUserId: string,
  serverNameInput: string,
  actualServerName: string
): Promise<{ error: string; success: boolean }> {
  const validationError = validateServerName(serverNameInput, actualServerName);
  if (validationError) {
    return { error: validationError, success: false };
  }

  try {
    await api.post(`/api/v1/servers/${serverId}/transfer-ownership`, {
      targetUserId,
    });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Failed to transfer ownership', success: false };
  }
}

describe('ownership-transfer', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.setAuthenticated(false);
  });

  describe('member selector', () => {
    it('should load members from API and exclude current user', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [
          { userId: 'user-1', username: 'alice', displayName: 'Alice' },
          { userId: 'user-2', username: 'bob', displayName: 'Bob' },
          { userId: 'current-user', username: 'me', displayName: 'Me' },
        ],
      });

      const result = await loadMembers('server-1', 'current-user');

      expect(result.error).toBe('');
      expect(result.members).toHaveLength(2);
      expect(result.members.find((m) => m.userId === 'current-user')).toBeUndefined();
    });

    it('should return error when member loading fails', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ error: 'Not a member' }),
      });

      const result = await loadMembers('server-1', 'current-user');

      expect(result.error).toBe('Not a member');
      expect(result.members).toHaveLength(0);
    });

  });

  describe('two-step confirmation', () => {
    it('should fail validation when server name does not match', () => {
      const error = validateServerName('Wrong Name', 'My Server');
      expect(error).toBe(
        'Server name does not match. Please type the exact server name.'
      );
    });

    it('should pass validation when server name matches exactly', () => {
      const error = validateServerName('My Server', 'My Server');
      expect(error).toBe('');
    });

    it('should pass validation when server name has leading/trailing whitespace (trim applied)', () => {
      // The component trims the input before comparing, so "  My Server  " matches "My Server".
      // This verifies the trim() behavior is present — without it the user would need to
      // type the name with exact whitespace to match, which would be a confusing UX bug.
      const error = validateServerName('  My Server  ', 'My Server');
      expect(error).toBe('');
    });

    it('should fail when server name is empty', () => {
      const error = validateServerName('', 'My Server');
      expect(error).toBe(
        'Server name does not match. Please type the exact server name.'
      );
    });
  });

  describe('API call on confirm', () => {
    it('should call transfer-ownership endpoint on successful confirmation', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ serverId: 'server-1', newOwnerId: 'user-1' }),
      });

      const result = await transferOwnership(
        'server-1',
        'user-1',
        'My Server',
        'My Server'
      );

      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/server-1/transfer-ownership',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ targetUserId: 'user-1' }),
        })
      );
    });

    it('should not call API when server name validation fails', async () => {
      globalThis.fetch = vi.fn();

      const result = await transferOwnership(
        'server-1',
        'user-1',
        'Wrong Name',
        'My Server'
      );

      expect(result.success).toBe(false);
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });
  });

  describe('error handling', () => {
    it('should return API error on forbidden', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ error: 'Only the server owner can transfer ownership' }),
      });

      const result = await transferOwnership(
        'server-1',
        'user-1',
        'My Server',
        'My Server'
      );

      expect(result.success).toBe(false);
      expect(result.error).toBe('Only the server owner can transfer ownership');
    });

    it('should return generic error when API fails with no message', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({}),
      });

      const result = await transferOwnership(
        'server-1',
        'user-1',
        'My Server',
        'My Server'
      );

      expect(result.success).toBe(false);
      expect(result.error).toBe('Failed to transfer ownership');
    });

    it('should return error when target is not a member', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Target user is not a member of this server' }),
      });

      const result = await transferOwnership(
        'server-1',
        'non-member',
        'My Server',
        'My Server'
      );

      expect(result.success).toBe(false);
      expect(result.error).toBe('Target user is not a member of this server');
    });
  });
});
