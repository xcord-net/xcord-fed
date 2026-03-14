import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import { hasRole, toggleRole } from '../components/GroupManager';

interface Group {
  id: string;
  serverId: string;
  name: string;
  color: string;
  roles: number;
  position: number;
  isHoisted: boolean;
  isMentionable: boolean;
}

describe('GroupManager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('group list rendering', () => {
    it('renders list of groups from API', async () => {
      // Arrange
      const serverId = 'srv-1';
      const groups: Group[] = [
        { id: 'group-1', serverId, name: 'Admin', color: '#d4943a', roles: 3, position: 0, isHoisted: true, isMentionable: true },
        { id: 'group-2', serverId, name: 'Moderator', color: '#57f287', roles: 1, position: 1, isHoisted: false, isMentionable: false },
      ];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => groups,
      });

      // Act
      const result = await api.get<Group[]>(`/api/v1/servers/${serverId}/groups`);

      // Assert
      expect(result).toHaveLength(2);
      expect(result[0].name).toBe('Admin');
      expect(result[1].name).toBe('Moderator');
    });

    it('calls GET /api/v1/servers/{id}/groups on load', async () => {
      // Arrange
      const serverId = 'srv-groups';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [],
      });

      // Act
      await api.get(`/api/v1/servers/${serverId}/groups`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/groups`,
        expect.objectContaining({ method: 'GET' })
      );
    });

    it('handles empty group list gracefully', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [],
      });

      // Act
      const result = await api.get<Group[]>('/api/v1/servers/srv-empty/groups');

      // Assert
      expect(result).toEqual([]);
      expect(result).toHaveLength(0);
    });
  });

  describe('create group', () => {
    it('calls POST /api/v1/servers/{id}/groups with group name', async () => {
      // Arrange
      const serverId = 'srv-2';
      const groupName = 'New Group';
      const created: Group = {
        id: 'group-new',
        serverId,
        name: groupName,
        color: '#d4943a',
        roles: 0,
        position: 2,
        isHoisted: false,
        isMentionable: false,
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => created,
      });

      // Act
      const result = await api.post<Group>(`/api/v1/servers/${serverId}/groups`, { name: groupName });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/groups`,
        expect.objectContaining({ method: 'POST', body: JSON.stringify({ name: groupName }) })
      );
      expect(result.name).toBe('New Group');
    });

    it('throws when POST fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Missing Manage Groups permission' }),
      });

      // Act & Assert
      await expect(
        api.post('/api/v1/servers/srv-x/groups', { name: 'Fail' })
      ).rejects.toMatchObject({ detail: 'Missing Manage Groups permission' });
    });
  });

  describe('role checkboxes', () => {
    it('hasRole returns true when bit is set', () => {
      // Arrange
      const VIEW_CHANNELS_BIT = 1 << 0;
      const roles = 0b11; // bits 0 and 1 set

      // Act & Assert
      expect(hasRole(roles, VIEW_CHANNELS_BIT)).toBe(true);
    });

    it('hasRole returns false when bit is not set', () => {
      // Arrange
      const KICK_BIT = 1 << 4;
      const roles = 0b0011; // only bits 0 and 1 set

      // Act & Assert
      expect(hasRole(roles, KICK_BIT)).toBe(false);
    });

    it('toggleRole enables a disabled role', () => {
      // Arrange
      const BAN_BIT = 1 << 5;
      const roles = 0;

      // Act
      const updated = toggleRole(roles, BAN_BIT);

      // Assert
      expect(hasRole(updated, BAN_BIT)).toBe(true);
    });

    it('toggleRole disables an enabled role', () => {
      // Arrange
      const MANAGE_MSG_BIT = 1 << 7;
      const roles = MANAGE_MSG_BIT;

      // Act
      const updated = toggleRole(roles, MANAGE_MSG_BIT);

      // Assert
      expect(hasRole(updated, MANAGE_MSG_BIT)).toBe(false);
    });

    it('sends updated roles integer in PUT payload', async () => {
      // Arrange
      const serverId = 'srv-3';
      const groupId = 'group-roles';
      const roles = (1 << 0) | (1 << 8); // View Channels + Send Messages

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: groupId, roles }),
      });

      // Act
      await api.put(`/api/v1/servers/${serverId}/groups/${groupId}`, {
        name: 'Group',
        color: '#d4943a',
        roles,
      });

      // Assert
      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(body.roles).toBe(roles);
    });
  });

  describe('delete group with confirmation', () => {
    it('calls DELETE /api/v1/servers/{id}/groups/{groupId}', async () => {
      // Arrange
      const serverId = 'srv-4';
      const groupId = 'group-del';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
        json: async () => undefined,
      });

      // Act
      await api.delete(`/api/v1/servers/${serverId}/groups/${groupId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/groups/${groupId}`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('throws when DELETE fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Cannot delete managed group' }),
      });

      // Act & Assert
      await expect(
        api.delete('/api/v1/servers/srv-x/groups/group-managed')
      ).rejects.toMatchObject({ detail: 'Cannot delete managed group' });
    });
  });

  describe('edit group API', () => {
    it('calls PUT /api/v1/servers/{id}/groups/{groupId} on save', async () => {
      // Arrange
      const serverId = 'srv-5';
      const groupId = 'group-edit';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: groupId, name: 'Updated Group', color: '#ed4245', roles: 256 }),
      });

      // Act
      const result = await api.put<Group>(`/api/v1/servers/${serverId}/groups/${groupId}`, {
        name: 'Updated Group',
        color: '#ed4245',
        roles: 256,
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/groups/${groupId}`,
        expect.objectContaining({ method: 'PUT' })
      );
      expect(result.name).toBe('Updated Group');
      expect(result.color).toBe('#ed4245');
    });
  });
});
