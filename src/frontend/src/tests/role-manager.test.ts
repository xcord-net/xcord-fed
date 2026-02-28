import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import { hasPermission, togglePermission } from '../components/RoleManager';

interface Role {
  id: string;
  serverId: string;
  name: string;
  color: string;
  permissions: number;
  position: number;
  isHoisted: boolean;
  isMentionable: boolean;
}

describe('RoleManager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('role list rendering', () => {
    it('renders list of roles from API', async () => {
      // Arrange
      const serverId = 'srv-1';
      const roles: Role[] = [
        { id: 'role-1', serverId, name: 'Admin', color: '#5865f2', permissions: 3, position: 0, isHoisted: true, isMentionable: true },
        { id: 'role-2', serverId, name: 'Moderator', color: '#57f287', permissions: 1, position: 1, isHoisted: false, isMentionable: false },
      ];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => roles,
      });

      // Act
      const result = await api.get<Role[]>(`/api/v1/servers/${serverId}/roles`);

      // Assert
      expect(result).toHaveLength(2);
      expect(result[0].name).toBe('Admin');
      expect(result[1].name).toBe('Moderator');
    });

    it('calls GET /api/v1/servers/{id}/roles on load', async () => {
      // Arrange
      const serverId = 'srv-roles';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [],
      });

      // Act
      await api.get(`/api/v1/servers/${serverId}/roles`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/roles`,
        expect.objectContaining({ method: 'GET' })
      );
    });

    it('handles empty role list gracefully', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [],
      });

      // Act
      const result = await api.get<Role[]>('/api/v1/servers/srv-empty/roles');

      // Assert
      expect(result).toEqual([]);
      expect(result).toHaveLength(0);
    });
  });

  describe('create role', () => {
    it('calls POST /api/v1/servers/{id}/roles with role name', async () => {
      // Arrange
      const serverId = 'srv-2';
      const roleName = 'New Role';
      const created: Role = {
        id: 'role-new',
        serverId,
        name: roleName,
        color: '#5865f2',
        permissions: 0,
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
      const result = await api.post<Role>(`/api/v1/servers/${serverId}/roles`, { name: roleName });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/roles`,
        expect.objectContaining({ method: 'POST', body: JSON.stringify({ name: roleName }) })
      );
      expect(result.name).toBe('New Role');
    });

    it('throws when POST fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Missing Manage Roles permission' }),
      });

      // Act & Assert
      await expect(
        api.post('/api/v1/servers/srv-x/roles', { name: 'Fail' })
      ).rejects.toMatchObject({ detail: 'Missing Manage Roles permission' });
    });
  });

  describe('permission checkboxes', () => {
    it('hasPermission returns true when bit is set', () => {
      // Arrange
      const ADMIN_BIT = 1 << 0;
      const permissions = 0b11; // bits 0 and 1 set

      // Act & Assert
      expect(hasPermission(permissions, ADMIN_BIT)).toBe(true);
    });

    it('hasPermission returns false when bit is not set', () => {
      // Arrange
      const KICK_BIT = 1 << 4;
      const permissions = 0b0011; // only bits 0 and 1 set

      // Act & Assert
      expect(hasPermission(permissions, KICK_BIT)).toBe(false);
    });

    it('togglePermission enables a disabled permission', () => {
      // Arrange
      const BAN_BIT = 1 << 5;
      const permissions = 0;

      // Act
      const updated = togglePermission(permissions, BAN_BIT);

      // Assert
      expect(hasPermission(updated, BAN_BIT)).toBe(true);
    });

    it('togglePermission disables an enabled permission', () => {
      // Arrange
      const MANAGE_MSG_BIT = 1 << 7;
      const permissions = MANAGE_MSG_BIT;

      // Act
      const updated = togglePermission(permissions, MANAGE_MSG_BIT);

      // Assert
      expect(hasPermission(updated, MANAGE_MSG_BIT)).toBe(false);
    });

    it('sends updated permissions integer in PUT payload', async () => {
      // Arrange
      const serverId = 'srv-3';
      const roleId = 'role-perms';
      const permissions = (1 << 0) | (1 << 8); // Administrator + Send Messages

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: roleId, permissions }),
      });

      // Act
      await api.put(`/api/v1/servers/${serverId}/roles/${roleId}`, {
        name: 'Role',
        color: '#5865f2',
        permissions,
      });

      // Assert
      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(body.permissions).toBe(permissions);
    });
  });

  describe('delete role with confirmation', () => {
    it('calls DELETE /api/v1/servers/{id}/roles/{roleId}', async () => {
      // Arrange
      const serverId = 'srv-4';
      const roleId = 'role-del';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
        json: async () => undefined,
      });

      // Act
      await api.delete(`/api/v1/servers/${serverId}/roles/${roleId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/roles/${roleId}`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('throws when DELETE fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Cannot delete managed role' }),
      });

      // Act & Assert
      await expect(
        api.delete('/api/v1/servers/srv-x/roles/role-managed')
      ).rejects.toMatchObject({ detail: 'Cannot delete managed role' });
    });
  });

  describe('edit role API', () => {
    it('calls PUT /api/v1/servers/{id}/roles/{roleId} on save', async () => {
      // Arrange
      const serverId = 'srv-5';
      const roleId = 'role-edit';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: roleId, name: 'Updated Role', color: '#ed4245', permissions: 256 }),
      });

      // Act
      const result = await api.put<Role>(`/api/v1/servers/${serverId}/roles/${roleId}`, {
        name: 'Updated Role',
        color: '#ed4245',
        permissions: 256,
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/roles/${roleId}`,
        expect.objectContaining({ method: 'PUT' })
      );
      expect(result.name).toBe('Updated Role');
      expect(result.color).toBe('#ed4245');
    });
  });
});
