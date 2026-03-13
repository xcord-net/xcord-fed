import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  TIER_PERKS,
  getNextTierRequirement,
  getBoostsToNextTier,
  formatUploadLimit,
  tierProgressPercent,
} from '../components/ServerBoost';
import type { BoostStatus } from '../components/ServerBoost';

// ---- Test data helpers ----

const makeBoostStatus = (overrides: Partial<BoostStatus> = {}): BoostStatus => ({
  serverId: 'server-1',
  tier: 0,
  boostCount: 0,
  boostedByMe: false,
  myBoostCount: 0,
  premiumSubscriberCount: 0,
  boostsToNextTier: 2,
  ...overrides,
});

// ---- Tests ----

describe('server-boost', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- TIER_PERKS constants ----

  describe('TIER_PERKS constants', () => {
    it('defines 4 tiers (0-3)', () => {
      expect(TIER_PERKS).toHaveLength(4);
      expect(TIER_PERKS[0].tier).toBe(0);
      expect(TIER_PERKS[3].tier).toBe(3);
    });

    it('tier 1 requires 2 boosts', () => {
      const tier1 = TIER_PERKS.find((t) => t.tier === 1);
      expect(tier1?.requiredBoosts).toBe(2);
    });

    it('each tier has at least one perk listed', () => {
      for (const tier of TIER_PERKS) {
        expect(tier.perks.length).toBeGreaterThan(0);
      }
    });

    it('tier 3 perks include animated server icon', () => {
      const tier3 = TIER_PERKS.find((t) => t.tier === 3);
      expect(tier3?.perks.some((p) => p.toLowerCase().includes('animated'))).toBe(true);
    });
  });

  // ---- getNextTierRequirement ----

  describe('getNextTierRequirement', () => {
    it('returns 2 as next requirement from tier 0', () => {
      expect(getNextTierRequirement(0)).toBe(2);
    });

    it('returns 7 as next requirement from tier 1', () => {
      expect(getNextTierRequirement(1)).toBe(7);
    });

    it('returns 14 as next requirement from tier 2', () => {
      expect(getNextTierRequirement(2)).toBe(14);
    });

    it('returns null at max tier 3', () => {
      expect(getNextTierRequirement(3)).toBeNull();
    });
  });

  // ---- getBoostsToNextTier ----

  describe('getBoostsToNextTier', () => {
    it('returns correct boosts needed from tier 0 with 0 boosts', () => {
      expect(getBoostsToNextTier(0, 0)).toBe(2);
    });

    it('returns 1 boost needed when at 6 with tier 1', () => {
      expect(getBoostsToNextTier(1, 6)).toBe(1);
    });

    it('returns 0 when enough boosts are met', () => {
      expect(getBoostsToNextTier(1, 7)).toBe(0);
    });

    it('returns 0 at max tier (no next tier)', () => {
      expect(getBoostsToNextTier(3, 20)).toBe(0);
    });
  });

  // ---- formatUploadLimit ----

  describe('formatUploadLimit', () => {
    it('extracts the upload limit perk string', () => {
      const perks = TIER_PERKS[1].perks;
      const limit = formatUploadLimit(perks);
      expect(limit).toBeDefined();
      expect(limit).toContain('upload limit');
    });

    it('returns undefined when no upload limit perk exists', () => {
      const perks = ['Custom emoji slots', '128 kbps audio quality'];
      expect(formatUploadLimit(perks)).toBeUndefined();
    });

    it('tier 3 upload limit is 100 MB', () => {
      const perks = TIER_PERKS[3].perks;
      const limit = formatUploadLimit(perks);
      expect(limit).toContain('100 MB');
    });
  });

  // ---- tierProgressPercent ----

  describe('tierProgressPercent', () => {
    it('returns 0% at tier 0 with 0 boosts', () => {
      expect(tierProgressPercent(0, 0)).toBe(0);
    });

    it('returns 50% halfway through tier 0 to tier 1', () => {
      // 0 boosts required for tier 0, 2 for tier 1 - 1 boost is 50%
      expect(tierProgressPercent(0, 1)).toBe(50);
    });

    it('returns 100% at tier 3 (max)', () => {
      expect(tierProgressPercent(3, 20)).toBe(100);
    });

    it('never exceeds 100%', () => {
      expect(tierProgressPercent(0, 100)).toBe(100);
    });

  });

  // ---- API: GET boost-status ----

  describe('GET /api/v1/servers/{id}/boost-status', () => {
    it('fetches boost status for a server', async () => {
      // Arrange
      const serverId = 'server-abc';
      const mockStatus = makeBoostStatus({ serverId, tier: 1, boostCount: 3, boostedByMe: true, boostsToNextTier: 4 });
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockStatus,
      });

      // Act
      const result = await api.get<BoostStatus>(`/api/v1/servers/${serverId}/boost-status`);

      // Assert - the full boost status is returned so all UI fields can be rendered
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/boost-status`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.tier).toBe(1);
      expect(result.boostCount).toBe(3);
      expect(result.boostedByMe).toBe(true);
      expect(result.boostsToNextTier).toBe(4);
      expect(result.serverId).toBe(serverId);
    });

    it('throws when server not found', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      // Act & Assert - the error payload is propagated so the UI can display it
      await expect(api.get('/api/v1/servers/nonexistent/boost-status')).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  // ---- API: POST boost ----

  describe('POST /api/v1/servers/{id}/boosts', () => {
    it('posts a boost to the correct endpoint', async () => {
      // Arrange
      const serverId = 'server-xyz';
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act - should resolve without throwing so the UI can update boost state
      await expect(api.post(`/api/v1/servers/${serverId}/boosts`)).resolves.not.toThrow();

      // Assert - correct endpoint was called
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/boosts`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('throws when boost fails (e.g. no boosts available)', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'No boosts available' }),
      });

      // Act & Assert - the error message must be propagated so the UI can show it to the user
      await expect(api.post('/api/v1/servers/server-1/boosts')).rejects.toMatchObject({ error: 'No boosts available' });
    });
  });

});
