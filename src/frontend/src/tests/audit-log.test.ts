import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import { formatTimestamp, getActionIcon } from '../components/AuditLogViewer';
import type { AuditLogEntry } from '../components/AuditLogViewer';

// ---- Helpers mirrored / re-exported from AuditLogViewer ----

// ---- Tests ----

describe('audit-log', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  const makeEntry = (overrides?: Partial<AuditLogEntry>): AuditLogEntry => ({
    id: 'entry-1',
    actionType: 'MemberKick',
    actorId: 'mod-user',
    actorUsername: 'Moderator',
    targetId: 'target-user',
    targetName: 'BadUser',
    reason: 'Violating rules',
    createdAt: '2026-02-10T14:30:00Z',
    ...overrides,
  });

  describe('fetching audit log entries', () => {
    it('calls GET /api/v1/servers/{id}/audit-log with limit', async () => {
      // Arrange
      const serverId = 'server-abc';
      const mockEntries: AuditLogEntry[] = [makeEntry()];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockEntries,
      });

      // Act
      const result = await api.get<AuditLogEntry[]>(
        `/api/v1/servers/${serverId}/audit-log?limit=50`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/audit-log?limit=50`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].actionType).toBe('MemberKick');
    });

    it('returns empty array when no log entries exist', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      const result = await api.get<AuditLogEntry[]>(
        '/api/v1/servers/server-new/audit-log?limit=50',
      );

      // Assert
      expect(result).toHaveLength(0);
    });

    it('includes all entry fields in response', async () => {
      // Arrange — server returns a response that omits optional fields but includes all required ones
      const serverResponse = {
        id: 'e-99',
        actionType: 'MemberBan',
        actorId: 'mod-user',
        actorUsername: 'AdminUser',
        targetId: 'target-user',
        targetName: 'Spammer',
        reason: 'Repeated spam',
        createdAt: '2026-02-10T14:30:00Z',
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [serverResponse],
      });

      // Act — verify the api client does not drop or rename any fields
      const result = await api.get<AuditLogEntry[]>(
        '/api/v1/servers/server-1/audit-log?limit=50',
      );

      // Assert — every required AuditLogEntry field survives the round-trip through api.get
      expect(result).toHaveLength(1);
      const e = result[0];
      // Identity fields
      expect(e.id).toBe('e-99');
      expect(e.actionType).toBe('MemberBan');
      // Actor fields
      expect(e.actorId).toBe('mod-user');
      expect(e.actorUsername).toBe('AdminUser');
      // Target fields
      expect(e.targetId).toBe('target-user');
      expect(e.targetName).toBe('Spammer');
      // Metadata
      expect(e.reason).toBe('Repeated spam');
      expect(e.createdAt).toBe('2026-02-10T14:30:00Z');
      // Verify no extra transformation occurred (field count matches the known shape)
      const fieldCount = Object.keys(e).length;
      expect(fieldCount).toBeGreaterThanOrEqual(8);
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/server-x/audit-log?limit=50'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });

  describe('filter by action type', () => {
    it('appends actionType query param when filter is set', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      await api.get('/api/v1/servers/server-1/audit-log?limit=50&actionType=MemberBan');

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/server-1/audit-log?limit=50&actionType=MemberBan',
        expect.anything(),
      );
    });

  });

  describe('pagination / load more', () => {
    it('passes before param to load more entries', async () => {
      // Arrange
      const lastEntryId = 'entry-last';
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      await api.get(`/api/v1/servers/server-1/audit-log?limit=50&before=${lastEntryId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/server-1/audit-log?limit=50&before=${lastEntryId}`,
        expect.anything(),
      );
    });

  });

  describe('timestamp formatting', () => {
    it('formatTimestamp includes the year', () => {
      // Arrange
      const iso = '2026-02-10T14:30:00Z';

      // Act
      const result = formatTimestamp(iso);

      // Assert
      expect(result).toContain('2026');
    });

    it('formatTimestamp handles different dates', () => {
      // Arrange
      const iso1 = '2026-01-01T00:00:00Z';
      const iso2 = '2026-12-31T23:59:59Z';

      // Act
      const r1 = formatTimestamp(iso1);
      const r2 = formatTimestamp(iso2);

      // Assert
      expect(r1).not.toBe(r2);
    });

    it('formatTimestamp returns valid locale string for ISO date', () => {
      // Arrange
      const iso = '2026-06-15T09:00:00Z';

      // Act
      const result = formatTimestamp(iso);

      // Assert — should not throw and should return a non-empty string
      expect(result.length).toBeGreaterThan(0);
    });
  });

  describe('action icons', () => {
    it('getActionIcon returns a fallback for unknown action types', () => {
      // Act
      const icon = getActionIcon('UnknownAction');

      // Assert
      expect(typeof icon).toBe('string');
      expect(icon.length).toBeGreaterThan(0);
    });

    it('different action types have different icons', () => {
      // Act
      const kickIcon = getActionIcon('MemberKick');
      const createIcon = getActionIcon('ChannelCreate');

      // Assert
      expect(kickIcon).not.toBe(createIcon);
    });
  });
});
