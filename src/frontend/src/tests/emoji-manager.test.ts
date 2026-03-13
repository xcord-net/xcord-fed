import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import { validateEmojiName, deriveEmojiName } from '../components/EmojiManager';
import type { CustomEmoji } from '../types/emoji';

// ---- Tests ----

describe('emoji-manager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  const makeEmoji = (overrides?: Partial<CustomEmoji>): CustomEmoji => ({
    id: 'emoji-1',
    serverId: 'server-abc',
    name: 'cool_face',
    imageUrl: 'https://cdn.example.com/emojis/emoji-1.png',
    creatorId: 'user-123',
    requiresColons: true,
    isAnimated: false,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  });

  describe('fetching server emojis', () => {
    it('calls GET /api/v1/servers/{id}/emojis', async () => {
      // Arrange
      const serverId = 'server-abc';
      const mockEmojis: CustomEmoji[] = [makeEmoji()];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockEmojis,
      });

      // Act
      const result = await api.get<CustomEmoji[]>(`/api/v1/servers/${serverId}/emojis`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/emojis`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('cool_face');
    });

    it('returns empty array when server has no custom emojis', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      // Act
      const result = await api.get<CustomEmoji[]>('/api/v1/servers/server-new/emojis');

      // Assert
      expect(result).toHaveLength(0);
    });

    it('includes all emoji fields in response', async () => {
      // Arrange
      const emoji = makeEmoji({
        id: 'e-99',
        name: 'party_blob',
        imageUrl: 'https://cdn.example.com/party_blob.gif',
        isAnimated: true,
      });

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [emoji],
      });

      // Act
      const result = await api.get<CustomEmoji[]>('/api/v1/servers/server-1/emojis');

      // Assert
      const e = result[0];
      expect(e.id).toBe('e-99');
      expect(e.name).toBe('party_blob');
      expect(e.isAnimated).toBe(true);
      expect(e.imageUrl).toContain('party_blob.gif');
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/server-x/emojis'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });

  describe('emoji upload form validation', () => {
    it('validateEmojiName returns null for a valid name', () => {
      // Act & Assert
      expect(validateEmojiName('cool_face')).toBeNull();
      expect(validateEmojiName('UPPER99')).toBeNull();
      expect(validateEmojiName('ab')).toBeNull();
    });

    it('validateEmojiName rejects empty name', () => {
      // Act
      const result = validateEmojiName('');

      // Assert
      expect(result).toBe('Please enter a name for the emoji.');
    });

    it('validateEmojiName rejects name shorter than 2 characters', () => {
      // Act
      const result = validateEmojiName('a');

      // Assert
      expect(result).toBe('Emoji name must be 2-32 characters: letters, numbers, underscores only.');
    });

    it('validateEmojiName rejects names with special characters', () => {
      // Act
      const result = validateEmojiName('cool face!');

      // Assert
      expect(result).toBe('Emoji name must be 2-32 characters: letters, numbers, underscores only.');
    });

    it('validateEmojiName rejects names longer than 32 characters', () => {
      // Act
      const result = validateEmojiName('a'.repeat(33));

      // Assert
      expect(result).toBe('Emoji name must be 2-32 characters: letters, numbers, underscores only.');
    });

    it('deriveEmojiName converts filename to valid emoji name', () => {
      // Act & Assert
      expect(deriveEmojiName('cool-face.png')).toBe('cool_face');
      expect(deriveEmojiName('my emoji!.gif')).toBe('my_emoji_');
      expect(deriveEmojiName('SomeName.webp')).toBe('somename');
    });

  });

  describe('emoji delete', () => {
    it('calls DELETE /api/v1/servers/{id}/emojis/{emojiId}', async () => {
      // Arrange
      const serverId = 'server-abc';
      const emojiId = 'emoji-1';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      await api.delete(`/api/v1/servers/${serverId}/emojis/${emojiId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/emojis/${emojiId}`,
        expect.objectContaining({ method: 'DELETE' }),
      );
    });

    it('throws when delete API call fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      // Act & Assert
      await expect(
        api.delete('/api/v1/servers/server-abc/emojis/nonexistent'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  describe('emoji upload API', () => {
    it('POST /api/v1/servers/{id}/emojis returns the new emoji', async () => {
      // Arrange
      const serverId = 'server-abc';
      const newEmoji = makeEmoji({ id: 'emoji-new', name: 'fresh_emoji' });

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => newEmoji,
      });

      // Act - simulate FormData upload via raw fetch (multipart)
      const formData = new FormData();
      formData.append('name', 'fresh_emoji');
      formData.append('image', new Blob(['pixels'], { type: 'image/png' }), 'emoji.png');

      const response = await fetch(`/api/v1/servers/${serverId}/emojis`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });
      const result = await response.json() as CustomEmoji;

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/emojis`,
        expect.objectContaining({ method: 'POST' }),
      );
      expect(result.name).toBe('fresh_emoji');
      expect(result.id).toBe('emoji-new');
    });

    it('upload fails when name is empty', () => {
      // Arrange
      const name = '   ';

      // Act
      const error = validateEmojiName(name);

      // Assert
      expect(error).toBe('Please enter a name for the emoji.');
    });
  });

});
