import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { Sound } from '../components/Soundboard';
import { formatSoundDuration, validateSoundName, sortSounds } from '../components/Soundboard';

// ---- Test data factories ----

const makeSound = (overrides: Partial<Sound> = {}): Sound => ({
  id: 'snd-1',
  name: 'Airhorn',
  serverId: 'srv-1',
  uploadedBy: 'user-1',
  durationMs: 3000,
  isDefault: false,
  createdAt: new Date('2026-01-01T00:00:00Z').toISOString(),
  ...overrides,
});

// ---- Tests ----

describe('Soundboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Sound shape ----

  describe('sound data shape', () => {
    it('sound has id, name, serverId, and durationMs', () => {
      const sound = makeSound();
      expect(sound.id).toBe('snd-1');
      expect(sound.name).toBe('Airhorn');
      expect(sound.serverId).toBe('srv-1');
      expect(sound.durationMs).toBe(3000);
    });

    it('sound has isDefault flag', () => {
      const defaultSound = makeSound({ isDefault: true });
      const customSound = makeSound({ isDefault: false });
      expect(defaultSound.isDefault).toBe(true);
      expect(customSound.isDefault).toBe(false);
    });

    it('sound has uploadedBy and createdAt fields', () => {
      const sound = makeSound({ uploadedBy: 'user-abc', createdAt: '2026-01-15T12:00:00Z' });
      expect(sound.uploadedBy).toBe('user-abc');
      expect(sound.createdAt).toBe('2026-01-15T12:00:00Z');
    });
  });

  // ---- formatSoundDuration helper ----

  describe('formatSoundDuration', () => {
    it('formats sub-second duration as 0s', () => {
      expect(formatSoundDuration(500)).toBe('1s'); // rounds to 1
    });

    it('formats exactly 3000ms as 3s', () => {
      expect(formatSoundDuration(3000)).toBe('3s');
    });

    it('formats 60000ms as 1m 0s', () => {
      expect(formatSoundDuration(60000)).toBe('1m 0s');
    });

    it('formats 90500ms as 1m 31s', () => {
      expect(formatSoundDuration(90500)).toBe('1m 31s');
    });

    it('formats sub-minute durations without minutes prefix', () => {
      const result = formatSoundDuration(15000);
      expect(result).toBe('15s');
      expect(result).not.toContain('m');
    });
  });

  // ---- validateSoundName helper ----

  describe('validateSoundName', () => {
    it('returns null for a valid name', () => {
      expect(validateSoundName('Airhorn')).toBeNull();
    });

    it('returns error when name is empty', () => {
      expect(validateSoundName('')).toBe('Sound name is required.');
    });

    it('returns error when name is whitespace only', () => {
      expect(validateSoundName('   ')).toBe('Sound name is required.');
    });

    it('returns error when name exceeds 50 characters', () => {
      const longName = 'A'.repeat(51);
      expect(validateSoundName(longName)).toBe('Sound name must be 50 characters or fewer.');
    });

    it('accepts name of exactly 50 characters', () => {
      const name = 'A'.repeat(50);
      expect(validateSoundName(name)).toBeNull();
    });
  });

  // ---- sortSounds helper ----

  describe('sortSounds', () => {
    it('places default sounds before custom sounds', () => {
      const sounds: Sound[] = [
        makeSound({ id: 'custom', name: 'Custom', isDefault: false }),
        makeSound({ id: 'default', name: 'Default', isDefault: true }),
      ];
      const sorted = sortSounds(sounds);
      expect(sorted[0].isDefault).toBe(true);
      expect(sorted[1].isDefault).toBe(false);
    });

    it('sorts custom sounds alphabetically by name', () => {
      const sounds: Sound[] = [
        makeSound({ id: 's3', name: 'Zebra', isDefault: false }),
        makeSound({ id: 's1', name: 'Apple', isDefault: false }),
        makeSound({ id: 's2', name: 'Mango', isDefault: false }),
      ];
      const sorted = sortSounds(sounds);
      expect(sorted[0].name).toBe('Apple');
      expect(sorted[1].name).toBe('Mango');
      expect(sorted[2].name).toBe('Zebra');
    });

    it('does not mutate the original array', () => {
      const sounds: Sound[] = [
        makeSound({ id: 's2', name: 'Zebra', isDefault: false }),
        makeSound({ id: 's1', name: 'Apple', isDefault: false }),
      ];
      sortSounds(sounds);
      expect(sounds[0].name).toBe('Zebra'); // unchanged
    });
  });

  // ---- GET sounds API ----

  describe('load sounds API', () => {
    it('GET sounds hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => [makeSound()],
      });

      // Act
      const result = await api.get<Sound[]>(`/api/v1/servers/${serverId}/sounds`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/sounds`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Airhorn');
    });

    it('throws on API error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      await expect(
        api.get('/api/v1/servers/srv-missing/sounds'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });

  // ---- POST (upload) sound API ----

  describe('upload sound API', () => {
    it('POST sounds hits correct URL with correct payload', async () => {
      // Arrange
      const serverId = 'srv-1';
      const payload = {
        name: 'Airhorn',
        fileName: 'airhorn.mp3',
        fileSize: 51200,
        contentType: 'audio/mpeg',
      };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => makeSound({ name: 'Airhorn' }),
      });

      // Act
      const result = await api.post<Sound>(`/api/v1/servers/${serverId}/sounds`, payload);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/sounds`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify(payload),
        }),
      );
      expect(result.name).toBe('Airhorn');
    });

    it('upload error sets uploadError state', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'File too large' }),
      });

      // Act & Assert
      await expect(
        api.post('/api/v1/servers/srv-1/sounds', { name: 'Big Sound', fileName: 'big.mp3', fileSize: 99999999 }),
      ).rejects.toMatchObject({ error: 'File too large' });
    });
  });

  // ---- Play sound API ----

  describe('play sound API', () => {
    it('POST play hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      const soundId = 'snd-1';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await api.post(`/api/v1/servers/${serverId}/sounds/${soundId}/play`, {});

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/sounds/${soundId}/play`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('throws on play error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Not in voice channel' }),
      });

      await expect(
        api.post('/api/v1/servers/srv-1/sounds/snd-1/play', {}),
      ).rejects.toMatchObject({ error: 'Not in voice channel' });
    });
  });
});
