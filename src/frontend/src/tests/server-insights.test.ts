import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  getMaxValue,
  formatInsightsDate,
  computeBarHeightPct,
  type InsightsRange,
  type DailyDataPoint,
  type ChannelActivity,
  type ServerInsightsData,
} from '../components/ServerInsights';

// ---- Test data ----

const makeDataPoint = (date: string, value: number): DailyDataPoint => ({ date, value });

const makeChannelActivity = (overrides?: Partial<ChannelActivity>): ChannelActivity => ({
  channelId: 'ch-1',
  channelName: 'general',
  messageCount: 42,
  ...overrides,
});

const makeInsightsData = (overrides?: Partial<ServerInsightsData>): ServerInsightsData => ({
  memberCount: 1200,
  memberGrowth: [
    makeDataPoint('2026-01-01', 5),
    makeDataPoint('2026-01-02', 8),
    makeDataPoint('2026-01-03', 3),
  ],
  messageActivity: [
    makeDataPoint('2026-01-01', 100),
    makeDataPoint('2026-01-02', 250),
    makeDataPoint('2026-01-03', 180),
  ],
  popularChannels: [
    makeChannelActivity({ channelId: 'ch-1', channelName: 'general', messageCount: 300 }),
    makeChannelActivity({ channelId: 'ch-2', channelName: 'off-topic', messageCount: 150 }),
  ],
  totalMessages: 8500,
  newMembersInRange: 32,
  ...overrides,
});

// ---- Tests ----

describe('ServerInsights', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- API fetching ----

  describe('fetching insights', () => {
    it('calls GET /api/v1/servers/{id}/insights with range param', async () => {
      // Arrange
      const serverId = 'server-abc';
      const range: InsightsRange = '30d';
      const mockData = makeInsightsData();

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockData,
      });

      // Act
      const result = await api.get<ServerInsightsData>(
        `/api/v1/servers/${serverId}/insights?range=${range}`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/insights?range=${range}`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.memberCount).toBe(1200);
    });

    it('includes all insight fields in response', async () => {
      // Arrange
      const mockData = makeInsightsData({ totalMessages: 9999, newMembersInRange: 77 });

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockData,
      });

      // Act
      const result = await api.get<ServerInsightsData>('/api/v1/servers/srv/insights?range=7d');

      // Assert
      expect(result.totalMessages).toBe(9999);
      expect(result.newMembersInRange).toBe(77);
      expect(result.memberGrowth).toHaveLength(3);
      expect(result.messageActivity).toHaveLength(3);
      expect(result.popularChannels).toHaveLength(2);
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/srv/insights?range=30d'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });

    it('supports all three time ranges', async () => {
      // Arrange
      const ranges: InsightsRange[] = ['7d', '30d', '90d'];
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => makeInsightsData(),
      });

      // Act & Assert
      for (const range of ranges) {
        await api.get(`/api/v1/servers/srv/insights?range=${range}`);
        expect(globalThis.fetch).toHaveBeenCalledWith(
          `/api/v1/servers/srv/insights?range=${range}`,
          expect.objectContaining({ method: 'GET' }),
        );
      }
    });
  });

  // ---- Chart helper: getMaxValue ----

  describe('getMaxValue', () => {
    it('returns the highest value from a data point array', () => {
      // Arrange
      const points = [makeDataPoint('d1', 10), makeDataPoint('d2', 50), makeDataPoint('d3', 30)];

      // Act
      const result = getMaxValue(points);

      // Assert
      expect(result).toBe(50);
    });

    it('returns 1 for an empty array (avoids divide-by-zero)', () => {
      // Act
      const result = getMaxValue([]);

      // Assert
      expect(result).toBe(1);
    });

    it('returns at least 1 even when all values are 0', () => {
      // Arrange
      const points = [makeDataPoint('d1', 0), makeDataPoint('d2', 0)];

      // Act
      const result = getMaxValue(points);

      // Assert
      expect(result).toBeGreaterThanOrEqual(1);
    });
  });

  // ---- Chart helper: computeBarHeightPct ----

  describe('computeBarHeightPct', () => {
    it('returns 100 when value equals max', () => {
      // Act
      const result = computeBarHeightPct(50, 50);

      // Assert
      expect(result).toBe(100);
    });

    it('returns 0 when max is 0', () => {
      // Act
      const result = computeBarHeightPct(0, 0);

      // Assert
      expect(result).toBe(0);
    });

    it('returns 50 for value half of max', () => {
      // Act
      const result = computeBarHeightPct(5, 10);

      // Assert
      expect(result).toBe(50);
    });

    it('returns an integer (rounded)', () => {
      // Act
      const result = computeBarHeightPct(1, 3);

      // Assert
      expect(Number.isInteger(result)).toBe(true);
    });
  });

  // ---- Date formatting ----

  describe('formatInsightsDate', () => {
    it('returns a non-empty string for a valid ISO date', () => {
      // Act
      const result = formatInsightsDate('2026-01-15T00:00:00Z');

      // Assert - must include the abbreviated month name and day number so chart axes are readable
      expect(result).toMatch(/Jan/i);
      expect(result).toMatch(/15/);
      expect(result).not.toContain('Invalid');
    });

    it('includes the day of month in the output', () => {
      // Act
      const result = formatInsightsDate('2026-01-15T00:00:00Z');

      // Assert
      expect(result).toMatch(/15/);
    });
  });

});
