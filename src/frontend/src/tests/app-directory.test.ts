import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { BotListing, AppDirectoryResponse } from '../components/AppDirectory';
import {
  formatInstallCount,
  filterBots,
  sortBotsByInstalls,
} from '../components/AppDirectory';

// ---- Test data ----

const makeBot = (overrides: Partial<BotListing> = {}): BotListing => ({
  id: 'bot-1',
  name: 'ModBot',
  description: 'A comprehensive moderation bot with ban, kick, mute, and automod features.',
  shortDescription: 'Powerful moderation tools',
  category: 'Moderation',
  installCount: 12500,
  permissions: ['Manage Messages', 'Kick Members', 'Ban Members'],
  isVerified: true,
  developerName: 'Xcord Labs',
  tags: ['mod', 'automod', 'safety'],
  ...overrides,
});

const makeBotList = (): BotListing[] => [
  makeBot({ id: 'bot-1', name: 'ModBot', category: 'Moderation', installCount: 12500, tags: ['mod'] }),
  makeBot({ id: 'bot-2', name: 'MusicBot', category: 'Music', installCount: 50000, tags: ['music', 'player'] }),
  makeBot({ id: 'bot-3', name: 'GameBot', category: 'Games', installCount: 3000, tags: ['games', 'fun'] }),
  makeBot({ id: 'bot-4', name: 'UtilBot', category: 'Utility', installCount: 8200, tags: ['utility', 'tools'] }),
];

// ---- Tests ----

describe('AppDirectory', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- formatInstallCount ----

  describe('formatInstallCount', () => {
    it('formats counts under 1000 as plain numbers', () => {
      expect(formatInstallCount(999)).toBe('999');
      expect(formatInstallCount(1)).toBe('1');
    });

    it('formats thousands with K suffix', () => {
      // Act
      const result = formatInstallCount(12500);

      // Assert
      expect(result).toContain('K');
    });

    it('formats millions with M suffix', () => {
      // Act
      const result = formatInstallCount(1_500_000);

      // Assert
      expect(result).toContain('M');
    });

    it('returns "0" for zero installs', () => {
      expect(formatInstallCount(0)).toBe('0');
    });

    it('formats exactly 1000 as 1.0K', () => {
      expect(formatInstallCount(1000)).toBe('1.0K');
    });
  });

  // ---- filterBots ----

  describe('filterBots', () => {
    it('returns all bots when query and category are empty', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const result = filterBots(bots, '', '');

      // Assert
      expect(result).toHaveLength(4);
    });

    it('filters by name (case-insensitive)', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const result = filterBots(bots, 'music', '');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('MusicBot');
    });

    it('filters by description text', () => {
      // Arrange
      const bots = [
        makeBot({ name: 'Alpha', description: 'Plays your favourite tracks', tags: [] }),
        makeBot({ name: 'Beta', description: 'Manages server roles', tags: [] }),
      ];

      // Act
      const result = filterBots(bots, 'tracks', '');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Alpha');
    });

    it('filters by tag', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const result = filterBots(bots, 'automod', '');

      // Assert — ModBot has 'automod' tag
      expect(result.some((b) => b.id === 'bot-1')).toBe(true);
    });

    it('filters by category when category is set', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const result = filterBots(bots, '', 'Music');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('MusicBot');
    });

    it('applies both query and category filter simultaneously', () => {
      // Arrange
      const bots = [
        makeBot({ id: '1', name: 'Alpha', category: 'Utility', tags: ['tool'], installCount: 1 }),
        makeBot({ id: '2', name: 'Beta', category: 'Utility', tags: ['helper'], installCount: 2 }),
        makeBot({ id: '3', name: 'Alpha', category: 'Music', tags: ['tool'], installCount: 3 }),
      ];

      // Act
      const result = filterBots(bots, 'Alpha', 'Utility');

      // Assert — only the Utility Alpha bot
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('1');
    });

    it('returns empty array when nothing matches', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const result = filterBots(bots, 'zzznomatch', '');

      // Assert
      expect(result).toHaveLength(0);
    });
  });

  // ---- sortBotsByInstalls ----

  describe('sortBotsByInstalls', () => {
    it('sorts bots in descending install count order', () => {
      // Arrange
      const bots = makeBotList();

      // Act
      const sorted = sortBotsByInstalls(bots);

      // Assert
      expect(sorted[0].installCount).toBeGreaterThanOrEqual(sorted[1].installCount);
      expect(sorted[1].installCount).toBeGreaterThanOrEqual(sorted[2].installCount);
    });

    it('most-installed bot is first', () => {
      // Arrange
      const bots = makeBotList(); // MusicBot has 50000

      // Act
      const sorted = sortBotsByInstalls(bots);

      // Assert
      expect(sorted[0].name).toBe('MusicBot');
    });

    it('does not mutate the original array', () => {
      // Arrange
      const bots = makeBotList();
      const originalFirst = bots[0].id;

      // Act
      sortBotsByInstalls(bots);

      // Assert
      expect(bots[0].id).toBe(originalFirst);
    });
  });

  // ---- BotListing shape ----

  describe('BotListing data shape', () => {
    it('has all required fields', () => {
      // Arrange
      const bot = makeBot();

      // Assert
      expect(bot.id).toBeDefined();
      expect(bot.name).toBeDefined();
      expect(bot.description).toBeDefined();
      expect(bot.shortDescription).toBeDefined();
      expect(bot.category).toBeDefined();
      expect(bot.installCount).toBeDefined();
      expect(bot.permissions).toBeInstanceOf(Array);
      expect(bot.isVerified).toBeDefined();
      expect(bot.developerName).toBeDefined();
      expect(bot.tags).toBeInstanceOf(Array);
    });

    it('avatarUrl is optional', () => {
      // Arrange
      const bot = makeBot({ avatarUrl: undefined });

      // Assert
      expect(bot.avatarUrl).toBeUndefined();
    });

    it('isVerified can be false for unverified bots', () => {
      // Arrange
      const bot = makeBot({ isVerified: false });

      // Assert
      expect(bot.isVerified).toBe(false);
    });
  });

  // ---- API: GET /api/v1/app-directory ----

  describe('app directory API', () => {
    it('GET /api/v1/app-directory returns bots and total', async () => {
      // Arrange
      const bots = makeBotList();
      const response: AppDirectoryResponse = { bots, total: bots.length };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => response,
      });

      // Act
      const result = await api.get<AppDirectoryResponse>('/api/v1/app-directory');

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/app-directory',
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.bots).toHaveLength(4);
      expect(result.total).toBe(4);
    });

    it('POST /api/v1/servers/{id}/bots/{botId}/install sends install request', async () => {
      // Arrange
      const serverId = 'srv-1';
      const botId = 'bot-2';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/bots/${botId}/install`, {});

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/bots/${botId}/install`,
        expect.objectContaining({
          method: 'POST',
        }),
      );
    });

    it('install API uses the correct server and bot IDs in URL', async () => {
      // Arrange
      const serverId = 'srv-abc';
      const botId = 'bot-xyz';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/bots/${botId}/install`, {});

      // Assert
      const url = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][0] as string;
      expect(url).toContain(serverId);
      expect(url).toContain(botId);
    });
  });

  // ---- Empty / error states ----

  describe('empty state', () => {
    it('empty bot list has length 0', () => {
      const bots: BotListing[] = [];
      expect(bots.length).toBe(0);
    });

    it('filtering an empty list returns empty list', () => {
      expect(filterBots([], 'search', '')).toHaveLength(0);
    });
  });
});
