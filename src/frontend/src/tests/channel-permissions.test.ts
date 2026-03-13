import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  cyclePermissionState,
  permissionStateColor,
  permissionStateLabel,
  createDefaultPermissions,
  ALL_PERMISSIONS,
  PERMISSION_LABELS,
  type PermissionState,
  type PermissionKey,
  type PermissionOverride,
  type ChannelPermissionsData,
} from '../components/ChannelPermissions';

// ---- Test data ----

const makeOverride = (overrides?: Partial<PermissionOverride>): PermissionOverride => ({
  subjectType: 'Role',
  subjectId: 'role-1',
  subjectName: '@everyone',
  permissions: createDefaultPermissions(),
  ...overrides,
});

const makePermissionsData = (overrides?: Partial<ChannelPermissionsData>): ChannelPermissionsData => ({
  overrides: [
    makeOverride({ subjectId: 'role-1', subjectName: '@everyone', subjectType: 'Role' }),
    makeOverride({ subjectId: 'role-2', subjectName: 'Moderator', subjectType: 'Role' }),
    makeOverride({ subjectId: 'user-1', subjectName: 'Alice', subjectType: 'Member' }),
  ],
  ...overrides,
});

// ---- Tests ----

describe('ChannelPermissions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- cyclePermissionState ----

  describe('cyclePermissionState', () => {
    it('cycles Inherit -> Allow', () => {
      // Act
      const result = cyclePermissionState('Inherit');

      // Assert
      expect(result).toBe('Allow');
    });

    it('cycles Allow -> Deny', () => {
      // Act
      const result = cyclePermissionState('Allow');

      // Assert
      expect(result).toBe('Deny');
    });

    it('cycles Deny -> Inherit', () => {
      // Act
      const result = cyclePermissionState('Deny');

      // Assert
      expect(result).toBe('Inherit');
    });

    it('cycling three times returns to the original state', () => {
      // Arrange
      const start: PermissionState = 'Inherit';

      // Act
      const result = cyclePermissionState(cyclePermissionState(cyclePermissionState(start)));

      // Assert
      expect(result).toBe(start);
    });
  });

  // ---- permissionStateColor ----

  describe('permissionStateColor', () => {
    it('returns green-related class for Allow', () => {
      // Act
      const result = permissionStateColor('Allow');

      // Assert
      expect(result).toContain('green');
    });

    it('returns red-related class for Deny', () => {
      // Act
      const result = permissionStateColor('Deny');

      // Assert
      expect(result).toContain('red');
    });

    it('returns muted class for Inherit', () => {
      // Act
      const result = permissionStateColor('Inherit');

      // Assert - Inherit should return a muted/gray color class, visually distinct
      // from Allow (green) and Deny (red). Check for a muted token that proves
      // the design intent rather than just the absence of green/red.
      expect(result).not.toContain('green');
      expect(result).not.toContain('red');
      expect(result).toMatch(/muted|gray|tertiary/);
    });
  });

  // ---- permissionStateLabel ----

  describe('permissionStateLabel', () => {
    it('returns the permission state as a string', () => {
      // Act & Assert
      expect(permissionStateLabel('Allow')).toBe('Allow');
      expect(permissionStateLabel('Deny')).toBe('Deny');
      expect(permissionStateLabel('Inherit')).toBe('Inherit');
    });
  });

  // ---- createDefaultPermissions ----

  describe('createDefaultPermissions', () => {
    it('creates an object with Inherit for every permission key', () => {
      // Act
      const defaults = createDefaultPermissions();

      // Assert
      for (const key of ALL_PERMISSIONS) {
        expect(defaults[key]).toBe('Inherit');
      }
    });

    it('contains all expected permission keys', () => {
      // Act
      const defaults = createDefaultPermissions();
      const keys = Object.keys(defaults) as PermissionKey[];

      // Assert
      for (const key of ALL_PERMISSIONS) {
        expect(keys).toContain(key);
      }
    });
  });

  // ---- Permission catalog ----

  describe('ALL_PERMISSIONS and PERMISSION_LABELS', () => {
    it('ALL_PERMISSIONS contains all required permission keys', () => {
      // Assert - every permission that an admin needs to configure must be present.
      // A count floor would still pass even if critical permissions are removed.
      const requiredKeys: PermissionKey[] = [
        'ViewChannel',
        'SendMessages',
        'ManageMessages',
        'AttachFiles',
        'EmbedLinks',
        'MentionEveryone',
        'ManageChannel',
        'Connect',
        'Speak',
      ];
      for (const key of requiredKeys) {
        expect(ALL_PERMISSIONS).toContain(key);
      }
    });

    it('every permission has a non-empty human-readable label', () => {
      // Assert
      for (const key of ALL_PERMISSIONS) {
        expect(PERMISSION_LABELS[key]).toEqual(expect.any(String));
        expect(PERMISSION_LABELS[key].length).toBeGreaterThan(0);
      }
    });
  });

  // ---- API calls ----

  describe('loading permissions', () => {
    it('calls GET /api/v1/servers/{id}/channels/{channelId}/permissions', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';
      const mockData = makePermissionsData();

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockData,
      });

      // Act
      const result = await api.get<ChannelPermissionsData>(
        `/api/v1/servers/${serverId}/channels/${channelId}/permissions`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/permissions`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.overrides).toHaveLength(3);
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/srv/channels/ch/permissions'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  describe('saving permissions', () => {
    it('calls PUT /api/v1/servers/{id}/channels/{channelId}/permissions', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';
      const perms = createDefaultPermissions();
      perms['ViewChannel'] = 'Allow';
      perms['SendMessages'] = 'Deny';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => makePermissionsData(),
      });

      // Act
      await api.put<ChannelPermissionsData>(
        `/api/v1/servers/${serverId}/channels/${channelId}/permissions`,
        { subjectId: 'role-1', permissions: perms },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/permissions`,
        expect.objectContaining({ method: 'PUT' }),
      );
    });

    it('throws when save API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.put('/api/v1/servers/srv/channels/ch/permissions', {}),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });

});
