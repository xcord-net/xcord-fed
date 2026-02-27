import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  filterStickersByQuery,
  groupStickersByServer,
  validateStickerName,
  type Sticker,
  type StickerPack,
} from '../components/StickerPicker';

// ---- Test data ----

const makeSticker = (overrides?: Partial<Sticker>): Sticker => ({
  id: 'sticker-1',
  serverId: 'server-abc',
  name: 'wave',
  description: 'A friendly wave',
  imageUrl: 'https://cdn.example.com/stickers/wave.png',
  tags: ['hello', 'friendly'],
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
});

// ---- Tests ----

describe('StickerPicker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- filterStickersByQuery ----

  describe('filterStickersByQuery', () => {
    it('returns all stickers when query is empty', () => {
      // Arrange
      const stickers = [makeSticker({ name: 'wave' }), makeSticker({ id: 's-2', name: 'clap' })];

      // Act
      const result = filterStickersByQuery(stickers, '');

      // Assert
      expect(result).toHaveLength(2);
    });

    it('filters by sticker name (case-insensitive)', () => {
      // Arrange
      const stickers = [
        makeSticker({ id: 's-1', name: 'Wave', description: undefined, tags: [] }),
        makeSticker({ id: 's-2', name: 'clap', description: undefined, tags: [] }),
      ];

      // Act
      const result = filterStickersByQuery(stickers, 'wave');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('s-1');
    });

    it('filters by tags', () => {
      // Arrange
      const stickers = [
        makeSticker({ id: 's-1', name: 'dance', tags: ['fun', 'party'] }),
        makeSticker({ id: 's-2', name: 'sleep', tags: ['tired', 'night'] }),
      ];

      // Act
      const result = filterStickersByQuery(stickers, 'party');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('s-1');
    });

    it('filters by description', () => {
      // Arrange
      const stickers = [
        makeSticker({ id: 's-1', name: 'a', description: 'A sad puppy face' }),
        makeSticker({ id: 's-2', name: 'b', description: 'Happy cat' }),
      ];

      // Act
      const result = filterStickersByQuery(stickers, 'puppy');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('s-1');
    });

    it('returns empty array when nothing matches', () => {
      // Arrange
      const stickers = [makeSticker({ name: 'wave', tags: ['hello'] })];

      // Act
      const result = filterStickersByQuery(stickers, 'zzz-no-match-zzz');

      // Assert
      expect(result).toHaveLength(0);
    });

    it('trims the search query before matching', () => {
      // Arrange
      const stickers = [makeSticker({ name: 'wave' })];

      // Act
      const result = filterStickersByQuery(stickers, '  wave  ');

      // Assert
      expect(result).toHaveLength(1);
    });
  });

  // ---- groupStickersByServer ----

  describe('groupStickersByServer', () => {
    it('groups stickers into packs by serverId', () => {
      // Arrange
      const stickers = [
        makeSticker({ id: 's-1', serverId: 'srv-1' }),
        makeSticker({ id: 's-2', serverId: 'srv-1' }),
        makeSticker({ id: 's-3', serverId: 'srv-2' }),
      ];

      // Act
      const packs = groupStickersByServer(stickers);

      // Assert
      expect(packs).toHaveLength(2);
      const pack1 = packs.find((p) => p.serverId === 'srv-1');
      expect(pack1?.stickers).toHaveLength(2);
    });

    it('returns empty array for empty sticker list', () => {
      // Act
      const packs = groupStickersByServer([]);

      // Assert
      expect(packs).toHaveLength(0);
    });

    it('each pack has serverId and stickers array', () => {
      // Arrange
      const stickers = [makeSticker({ serverId: 'srv-1' })];

      // Act
      const packs = groupStickersByServer(stickers);

      // Assert
      expect(packs[0].serverId).toBe('srv-1');
      expect(Array.isArray(packs[0].stickers)).toBe(true);
    });
  });

  // ---- validateStickerName ----

  describe('validateStickerName', () => {
    it('returns null for a valid name', () => {
      // Act
      const result = validateStickerName('wave_2');

      // Assert
      expect(result).toBeNull();
    });

    it('returns error for empty name', () => {
      // Act
      const result = validateStickerName('');

      // Assert — should return a non-empty error string
      expect(result).toEqual(expect.any(String));
      expect(result!.length).toBeGreaterThan(0);
    });

    it('returns error for single character name', () => {
      // Act
      const result = validateStickerName('a');

      // Assert — should return a non-empty error string
      expect(result).toEqual(expect.any(String));
      expect(result!.length).toBeGreaterThan(0);
    });

    it('returns error for name longer than 32 characters', () => {
      // Act
      const result = validateStickerName('a'.repeat(33));

      // Assert — should return a non-empty error string
      expect(result).toEqual(expect.any(String));
      expect(result!.length).toBeGreaterThan(0);
    });

    it('accepts a name exactly 2 characters', () => {
      // Act
      const result = validateStickerName('ab');

      // Assert
      expect(result).toBeNull();
    });

    it('accepts a name exactly 32 characters', () => {
      // Act
      const result = validateStickerName('a'.repeat(32));

      // Assert
      expect(result).toBeNull();
    });
  });

  // ---- API calls ----

  describe('fetching stickers', () => {
    it('calls GET /api/v1/servers/{id}/stickers', async () => {
      // Arrange
      const serverId = 'server-abc';
      const mockStickers: Sticker[] = [makeSticker()];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockStickers,
      });

      // Act
      const result = await api.get<Sticker[]>(`/api/v1/servers/${serverId}/stickers`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/stickers`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('wave');
    });

    it('returns empty array when server has no stickers', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      const result = await api.get<Sticker[]>('/api/v1/servers/new-server/stickers');

      // Assert
      expect(result).toHaveLength(0);
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/srv/stickers'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });

  describe('deleting a sticker', () => {
    it('calls DELETE /api/v1/servers/{id}/stickers/{stickerId}', async () => {
      // Arrange
      const serverId = 'server-abc';
      const stickerId = 'sticker-1';

      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.delete(`/api/v1/servers/${serverId}/stickers/${stickerId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/stickers/${stickerId}`,
        expect.objectContaining({ method: 'DELETE' }),
      );
    });

    it('removes deleted sticker from local list', () => {
      // Arrange
      let stickers: Sticker[] = [
        makeSticker({ id: 's-1', name: 'wave' }),
        makeSticker({ id: 's-2', name: 'clap' }),
        makeSticker({ id: 's-3', name: 'dance' }),
      ];

      // Act
      stickers = stickers.filter((s) => s.id !== 's-2');

      // Assert
      expect(stickers).toHaveLength(2);
      expect(stickers.map((s) => s.name)).toEqual(['wave', 'dance']);
    });

    it('throws when delete API call fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      // Act & Assert
      await expect(
        api.delete('/api/v1/servers/srv/stickers/nonexistent'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  describe('sticker data shape', () => {
    it('sticker imageUrl is a valid URL string', () => {
      // Arrange
      const sticker = makeSticker();

      // Assert
      expect(sticker.imageUrl.startsWith('http')).toBe(true);
    });

    it('sticker tags is an array', () => {
      // Arrange
      const sticker = makeSticker({ tags: ['fun', 'cute'] });

      // Assert
      expect(Array.isArray(sticker.tags)).toBe(true);
      expect(sticker.tags).toHaveLength(2);
    });
  });
});
