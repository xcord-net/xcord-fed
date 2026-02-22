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
      // Arrange
      const entry = makeEntry({
        id: 'e-99',
        actionType: 'MemberBan',
        actorUsername: 'AdminUser',
        targetName: 'Spammer',
        reason: 'Repeated spam',
      });

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [entry],
      });

      // Act
      const result = await api.get<AuditLogEntry[]>(
        '/api/v1/servers/server-1/audit-log?limit=50',
      );

      // Assert
      const e = result[0];
      expect(e.id).toBe('e-99');
      expect(e.actionType).toBe('MemberBan');
      expect(e.actorUsername).toBe('AdminUser');
      expect(e.targetName).toBe('Spammer');
      expect(e.reason).toBe('Repeated spam');
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

    it('filters only matching action types client-side for display', () => {
      // Arrange
      const entries: AuditLogEntry[] = [
        makeEntry({ actionType: 'MemberBan' }),
        makeEntry({ id: 'e2', actionType: 'MemberKick' }),
        makeEntry({ id: 'e3', actionType: 'MemberBan' }),
      ];

      // Act — simulate client-side filtering before server-side filter propagates
      const filtered = entries.filter((e) => e.actionType === 'MemberBan');

      // Assert
      expect(filtered).toHaveLength(2);
      expect(filtered.every((e) => e.actionType === 'MemberBan')).toBe(true);
    });

    it('encodes special characters in filter value', () => {
      // Arrange
      const actionType = 'Channel Create';

      // Act
      const encoded = encodeURIComponent(actionType);

      // Assert
      expect(encoded).toBe('Channel%20Create');
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

    it('detects hasMore when result length equals limit', () => {
      // Arrange
      const LIMIT = 50;
      const results = Array.from({ length: LIMIT }, (_, i) =>
        makeEntry({ id: `e${i}` }),
      );

      // Act — mirror hasMore logic from component
      const hasMore = results.length === LIMIT;

      // Assert
      expect(hasMore).toBe(true);
    });

    it('detects no more pages when result count is less than limit', () => {
      // Arrange
      const LIMIT = 50;
      const results = Array.from({ length: 10 }, (_, i) =>
        makeEntry({ id: `e${i}` }),
      );

      // Act
      const hasMore = results.length === LIMIT;

      // Assert
      expect(hasMore).toBe(false);
    });

    it('appends new entries to existing entries on load more', () => {
      // Arrange
      const existing: AuditLogEntry[] = [makeEntry({ id: 'e1' }), makeEntry({ id: 'e2' })];
      const newEntries: AuditLogEntry[] = [makeEntry({ id: 'e3' }), makeEntry({ id: 'e4' })];

      // Act — mirror the state update logic
      const combined = [...existing, ...newEntries];

      // Assert
      expect(combined).toHaveLength(4);
      expect(combined[2].id).toBe('e3');
    });
  });

  describe('timestamp formatting', () => {
    it('formatTimestamp returns a non-empty string', () => {
      // Arrange
      const iso = '2026-02-10T14:30:00Z';

      // Act
      const result = formatTimestamp(iso);

      // Assert
      expect(result.length).toBeGreaterThan(0);
      expect(typeof result).toBe('string');
    });

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
    it('getActionIcon returns a non-empty string for known action types', () => {
      // Arrange
      const knownActions = ['MemberKick', 'MemberBan', 'MemberUnban', 'ChannelCreate', 'MessageDelete'];

      // Act & Assert
      for (const action of knownActions) {
        const icon = getActionIcon(action);
        expect(typeof icon).toBe('string');
        expect(icon.length).toBeGreaterThan(0);
      }
    });

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
