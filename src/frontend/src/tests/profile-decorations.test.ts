import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  groupDecorationsByType,
  buildProfilePayload,
  countAnimatedDecorations,
  filterByType,
  getRarityColor,
  rarityColors,
} from '../components/ProfileDecorations';
import type { Decoration, DecorationType, UserProfileDecorations } from '../components/ProfileDecorations';

// ---- Test data helpers ----

const makeDecoration = (overrides: Partial<Decoration> = {}): Decoration => ({
  id: 'deco-1',
  type: 'Banner',
  name: 'Flame Banner',
  previewUrl: 'https://cdn.example.com/banner/flame.png',
  animated: false,
  rarity: 'Rare',
  ...overrides,
});

// ---- Tests ----

describe('profile-decorations', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- groupDecorationsByType ----

  describe('groupDecorationsByType', () => {
    it('groups decorations into Banner, AvatarFrame, ProfileEffect buckets', () => {
      const decorations: Decoration[] = [
        makeDecoration({ id: '1', type: 'Banner' }),
        makeDecoration({ id: '2', type: 'AvatarFrame' }),
        makeDecoration({ id: '3', type: 'ProfileEffect' }),
        makeDecoration({ id: '4', type: 'Banner' }),
      ];

      const grouped = groupDecorationsByType(decorations);

      expect(grouped.Banner).toHaveLength(2);
      expect(grouped.AvatarFrame).toHaveLength(1);
      expect(grouped.ProfileEffect).toHaveLength(1);
    });

    it('returns empty arrays for missing types', () => {
      const decorations: Decoration[] = [makeDecoration({ type: 'Banner' })];
      const grouped = groupDecorationsByType(decorations);

      expect(grouped.AvatarFrame).toHaveLength(0);
      expect(grouped.ProfileEffect).toHaveLength(0);
    });

    it('returns all empty when input is empty', () => {
      const grouped = groupDecorationsByType([]);
      expect(grouped.Banner).toHaveLength(0);
      expect(grouped.AvatarFrame).toHaveLength(0);
      expect(grouped.ProfileEffect).toHaveLength(0);
    });
  });

  // ---- buildProfilePayload ----

  describe('buildProfilePayload', () => {
    it('includes all decoration IDs when set', () => {
      const decorations: UserProfileDecorations = {
        bannerId: 'ban-1',
        avatarFrameId: 'frame-2',
        profileEffectId: 'effect-3',
        bannerColor: '#7289DA',
      };
      const payload = buildProfilePayload(decorations);
      expect(payload.bannerId).toBe('ban-1');
      expect(payload.avatarFrameId).toBe('frame-2');
      expect(payload.profileEffectId).toBe('effect-3');
      expect(payload.bannerColor).toBe('#7289DA');
    });

    it('sets null for missing decoration IDs', () => {
      const payload = buildProfilePayload({});
      expect(payload.bannerId).toBeNull();
      expect(payload.avatarFrameId).toBeNull();
      expect(payload.profileEffectId).toBeNull();
      expect(payload.bannerColor).toBeNull();
    });

    it('partial payload has null for unset fields', () => {
      const payload = buildProfilePayload({ bannerId: 'ban-xyz' });
      expect(payload.bannerId).toBe('ban-xyz');
      expect(payload.avatarFrameId).toBeNull();
    });
  });

  // ---- countAnimatedDecorations ----

  describe('countAnimatedDecorations', () => {
    it('counts only animated decorations', () => {
      const decorations: Decoration[] = [
        makeDecoration({ id: '1', animated: true }),
        makeDecoration({ id: '2', animated: false }),
        makeDecoration({ id: '3', animated: true }),
      ];
      expect(countAnimatedDecorations(decorations)).toBe(2);
    });

    it('returns 0 when no decorations are animated', () => {
      const decorations = [makeDecoration({ animated: false })];
      expect(countAnimatedDecorations(decorations)).toBe(0);
    });

    it('returns 0 for empty array', () => {
      expect(countAnimatedDecorations([])).toBe(0);
    });
  });

  // ---- filterByType ----

  describe('filterByType', () => {
    it('returns only decorations of the given type', () => {
      const decorations: Decoration[] = [
        makeDecoration({ id: '1', type: 'Banner' }),
        makeDecoration({ id: '2', type: 'AvatarFrame' }),
        makeDecoration({ id: '3', type: 'Banner' }),
      ];
      const banners = filterByType(decorations, 'Banner');
      expect(banners).toHaveLength(2);
      expect(banners.every((d) => d.type === 'Banner')).toBe(true);
    });

    it('returns empty when no decorations match type', () => {
      const decorations = [makeDecoration({ type: 'Banner' })];
      expect(filterByType(decorations, 'ProfileEffect')).toHaveLength(0);
    });
  });

  // ---- getRarityColor ----

  describe('getRarityColor', () => {
    it('returns text-blue-400 for Rare', () => {
      expect(getRarityColor('Rare')).toBe('text-blue-400');
    });

    it('returns text-purple-400 for Epic', () => {
      expect(getRarityColor('Epic')).toBe('text-purple-400');
    });

    it('returns text-yellow-400 for Legendary', () => {
      expect(getRarityColor('Legendary')).toBe('text-yellow-400');
    });

    it('returns text-gray-400 for Common', () => {
      expect(getRarityColor('Common')).toBe('text-gray-400');
    });

    it('returns muted class for undefined rarity', () => {
      expect(getRarityColor(undefined)).toBe('text-xcord-text-muted');
    });
  });

  // ---- API calls ----

  describe('GET /api/v1/decorations', () => {
    it('fetches the decoration catalog', async () => {
      // Arrange
      const mockDecorations: Decoration[] = [makeDecoration()];
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockDecorations,
      });

      // Act
      const result = await api.get<Decoration[]>('/api/v1/decorations');

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/decorations',
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Flame Banner');
    });

    it('returns empty array when no decorations exist', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });

      // Act
      const result = await api.get<Decoration[]>('/api/v1/decorations');

      // Assert
      expect(result).toHaveLength(0);
    });
  });

  describe('PUT /api/v1/users/@me/profile', () => {
    it('saves the selected decorations', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });
      const payload = buildProfilePayload({ bannerId: 'ban-1' });

      // Act
      await api.put('/api/v1/users/@me/profile', payload);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/profile',
        expect.objectContaining({
          method: 'PUT',
          body: expect.stringContaining('ban-1'),
        }),
      );
    });

    it('throws when save fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.put('/api/v1/users/@me/profile', {}),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });
});
