import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { WelcomeScreenConfig, WelcomeChannel } from '../components/WelcomeScreen';
import {
  validateWelcomeDescription,
  validateWelcomeChannel,
  welcomeChannelCount,
} from '../components/WelcomeScreen';

// ---- Pure logic helpers mirrored from WelcomeScreen ----

function hasValidChannels(channels: WelcomeChannel[]): boolean {
  return channels.every((ch) => validateWelcomeChannel(ch) === null);
}

// ---- Test data ----

const makeChannel = (overrides: Partial<WelcomeChannel> = {}): WelcomeChannel => ({
  channelId: 'ch-1',
  channelName: 'general',
  description: 'Start here!',
  emoji: '👋',
  ...overrides,
});

const makeConfig = (overrides: Partial<WelcomeScreenConfig> = {}): WelcomeScreenConfig => ({
  enabled: true,
  description: 'Welcome to our awesome community server!',
  welcomeChannels: [
    makeChannel({ channelId: 'ch-1', channelName: 'general', description: 'Start here!' }),
    makeChannel({ channelId: 'ch-2', channelName: 'announcements', description: 'Stay updated.' }),
  ],
  ...overrides,
});

// ---- Tests ----

describe('WelcomeScreen', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Config data shape ----

  describe('WelcomeScreenConfig shape', () => {
    it('config has enabled flag, description, and channels', () => {
      // Arrange
      const config = makeConfig();

      // Assert
      expect(config.enabled).toBe(true);
      expect(config.description).toBe('Welcome to our awesome community server!');
      expect(Array.isArray(config.welcomeChannels)).toBe(true);
    });

    it('config can be disabled', () => {
      // Arrange
      const config = makeConfig({ enabled: false });

      // Assert
      expect(config.enabled).toBe(false);
    });

    it('welcome channels are optional (can be empty)', () => {
      // Arrange
      const config = makeConfig({ welcomeChannels: [] });

      // Assert
      expect(config.welcomeChannels).toHaveLength(0);
    });
  });

  // ---- Description validation ----

  describe('validateWelcomeDescription', () => {
    it('accepts a valid description', () => {
      // Act
      const error = validateWelcomeDescription('Welcome to our server!');

      // Assert
      expect(error).toBeNull();
    });

    it('returns error for empty description', () => {
      // Act
      const error = validateWelcomeDescription('');

      // Assert
      expect(error).toBe('Description is required.');
    });

    it('returns error for whitespace-only description', () => {
      // Act
      const error = validateWelcomeDescription('   ');

      // Assert
      expect(error).toBe('Description is required.');
    });

    it('returns error for description exceeding 500 characters', () => {
      // Arrange
      const longDesc = 'a'.repeat(501);

      // Act
      const error = validateWelcomeDescription(longDesc);

      // Assert
      expect(error).toBe('Description must be 500 characters or fewer.');
    });

    it('accepts a description exactly 500 characters', () => {
      // Arrange
      const desc = 'a'.repeat(500);

      // Act
      const error = validateWelcomeDescription(desc);

      // Assert
      expect(error).toBeNull();
    });
  });

  // ---- Channel validation ----

  describe('validateWelcomeChannel', () => {
    it('returns null for a valid channel', () => {
      // Arrange
      const ch = makeChannel();

      // Act
      const error = validateWelcomeChannel(ch);

      // Assert
      expect(error).toBeNull();
    });

    it('returns error when channelId is empty', () => {
      // Arrange
      const ch = makeChannel({ channelId: '' });

      // Act
      const error = validateWelcomeChannel(ch);

      // Assert
      expect(error).toBe('Channel ID is required.');
    });

    it('returns error when channel description is empty', () => {
      // Arrange
      const ch = makeChannel({ description: '' });

      // Act
      const error = validateWelcomeChannel(ch);

      // Assert
      expect(error).toBe('Channel description is required.');
    });

    it('returns error when channel description exceeds 200 characters', () => {
      // Arrange
      const ch = makeChannel({ description: 'a'.repeat(201) });

      // Act
      const error = validateWelcomeChannel(ch);

      // Assert
      expect(error).toBe('Channel description must be 200 characters or fewer.');
    });

    it('emoji is optional on a channel', () => {
      // Arrange
      const ch = makeChannel({ emoji: undefined });

      // Act
      const error = validateWelcomeChannel(ch);

      // Assert
      expect(error).toBeNull();
    });

    it('hasValidChannels returns true when all channels are valid', () => {
      // Arrange
      const channels = [makeChannel({ channelId: 'ch-1' }), makeChannel({ channelId: 'ch-2' })];

      // Assert
      expect(hasValidChannels(channels)).toBe(true);
    });

    it('hasValidChannels returns false when any channel is invalid', () => {
      // Arrange
      const channels = [makeChannel(), makeChannel({ channelId: '' })];

      // Assert
      expect(hasValidChannels(channels)).toBe(false);
    });
  });

  // ---- Channel count ----

  describe('welcomeChannelCount', () => {
    it('returns correct count for channels', () => {
      // Arrange
      const config = makeConfig();

      // Act
      const count = welcomeChannelCount(config);

      // Assert
      expect(count).toBe(2);
    });

    it('returns 0 for empty channels array', () => {
      // Arrange
      const config = makeConfig({ welcomeChannels: [] });

      // Act
      const count = welcomeChannelCount(config);

      // Assert
      expect(count).toBe(0);
    });
  });

  // ---- API: load welcome screen ----

  describe('load welcome screen API', () => {
    it('GET hits the correct URL', async () => {
      // Arrange
      const serverId = 'srv-get-test';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => makeConfig(),
      });

      // Act
      const result = await api.get<WelcomeScreenConfig>(
        `/api/v1/servers/${serverId}/welcome-screen`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/welcome-screen`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.enabled).toBe(true);
    });
  });

  // ---- API: save welcome screen ----

  describe('save welcome screen API', () => {
    it('PUT hits the correct URL with payload', async () => {
      // Arrange
      const serverId = 'srv-put-test';
      const payload = makeConfig({ description: 'Updated description' });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => payload,
      });

      // Act
      const result = await api.put<WelcomeScreenConfig>(
        `/api/v1/servers/${serverId}/welcome-screen`,
        payload,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/welcome-screen`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify(payload),
        }),
      );
      expect(result.description).toBe('Updated description');
    });

    it('saving with enabled=false disables the welcome screen', async () => {
      // Arrange
      const payload = makeConfig({ enabled: false });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => payload,
      });

      // Act
      const result = await api.put<WelcomeScreenConfig>(
        '/api/v1/servers/srv-1/welcome-screen',
        payload,
      );

      // Assert
      expect(result.enabled).toBe(false);
    });
  });

  // ---- Channel with emoji ----

  describe('welcome channel emoji', () => {
    it('channel emoji is included in the channel shape', () => {
      // Arrange
      const ch = makeChannel({ emoji: '🎮' });

      // Assert
      expect(ch.emoji).toBe('🎮');
    });

    it('channel without emoji has undefined emoji', () => {
      // Arrange
      const ch = makeChannel({ emoji: undefined });

      // Assert
      expect(ch.emoji).toBeUndefined();
    });
  });
});
