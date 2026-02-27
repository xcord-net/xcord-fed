import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { BotCommand, CommandParameter } from '../components/CommandPalette';
import {
  filterCommands,
  buildCommandPreview,
  validateCommandArgs,
} from '../components/CommandPalette';

// ---- Test data ----

const makeParam = (overrides: Partial<CommandParameter> = {}): CommandParameter => ({
  name: 'user',
  description: 'The target user',
  required: true,
  type: 'user',
  ...overrides,
});

const makeCommand = (overrides: Partial<BotCommand> = {}): BotCommand => ({
  id: 'cmd-1',
  name: 'ban',
  description: 'Ban a user from the server',
  parameters: [makeParam()],
  botId: 'bot-1',
  botName: 'ModBot',
  ...overrides,
});

const makeCommandList = (): BotCommand[] => [
  makeCommand({ id: 'cmd-1', name: 'ban', description: 'Ban a user', botName: 'ModBot' }),
  makeCommand({ id: 'cmd-2', name: 'kick', description: 'Kick a user', botName: 'ModBot', parameters: [] }),
  makeCommand({ id: 'cmd-3', name: 'play', description: 'Play a song', botName: 'MusicBot', parameters: [makeParam({ name: 'query', type: 'string', required: true })] }),
  makeCommand({ id: 'cmd-4', name: 'skip', description: 'Skip current song', botName: 'MusicBot', parameters: [] }),
];

// ---- Tests ----

describe('CommandPalette', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- filterCommands ----

  describe('filterCommands', () => {
    it('returns all commands when query is empty', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, '');

      // Assert
      expect(result).toHaveLength(4);
    });

    it('filters commands by name (case-insensitive)', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, 'ban');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('ban');
    });

    it('strips leading slash from query before filtering', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, '/kick');

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('kick');
    });

    it('filters commands by description', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, 'song');

      // Assert
      expect(result.every((c) => c.botName === 'MusicBot')).toBe(true);
    });

    it('filters commands by bot name', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, 'musicbot');

      // Assert
      expect(result).toHaveLength(2);
      expect(result.every((c) => c.botName === 'MusicBot')).toBe(true);
    });

    it('returns empty array when no commands match', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, 'xyznotfound');

      // Assert
      expect(result).toHaveLength(0);
    });

    it('returns all commands when query is just a slash', () => {
      // Arrange
      const commands = makeCommandList();

      // Act
      const result = filterCommands(commands, '/');

      // Assert
      expect(result).toHaveLength(4);
    });
  });

  // ---- buildCommandPreview ----

  describe('buildCommandPreview', () => {
    it('returns slash + name for command with no parameters', () => {
      // Arrange
      const cmd = makeCommand({ name: 'kick', parameters: [] });

      // Act
      const preview = buildCommandPreview(cmd);

      // Assert
      expect(preview).toBe('/kick');
    });

    it('wraps required parameters in angle brackets', () => {
      // Arrange
      const cmd = makeCommand({
        name: 'ban',
        parameters: [makeParam({ name: 'user', required: true })],
      });

      // Act
      const preview = buildCommandPreview(cmd);

      // Assert
      expect(preview).toContain('<user>');
    });

    it('wraps optional parameters in square brackets', () => {
      // Arrange
      const cmd = makeCommand({
        name: 'ban',
        parameters: [makeParam({ name: 'reason', required: false })],
      });

      // Act
      const preview = buildCommandPreview(cmd);

      // Assert
      expect(preview).toContain('[reason]');
    });

    it('includes all parameters in preview', () => {
      // Arrange
      const cmd = makeCommand({
        name: 'ban',
        parameters: [
          makeParam({ name: 'user', required: true }),
          makeParam({ name: 'reason', required: false, type: 'string' }),
        ],
      });

      // Act
      const preview = buildCommandPreview(cmd);

      // Assert
      expect(preview).toContain('<user>');
      expect(preview).toContain('[reason]');
    });
  });

  // ---- validateCommandArgs ----

  describe('validateCommandArgs', () => {
    it('returns null when all required parameters are provided', () => {
      // Arrange
      const cmd = makeCommand({
        parameters: [makeParam({ name: 'user', required: true })],
      });

      // Act
      const error = validateCommandArgs(cmd, { user: 'someuser' });

      // Assert
      expect(error).toBeNull();
    });

    it('returns error message when required parameter is missing', () => {
      // Arrange
      const cmd = makeCommand({
        parameters: [makeParam({ name: 'user', required: true })],
      });

      // Act
      const error = validateCommandArgs(cmd, {});

      // Assert
      expect(typeof error).toBe('string');
      expect(error).toContain('user');
    });

    it('returns null when optional parameter is omitted', () => {
      // Arrange
      const cmd = makeCommand({
        parameters: [makeParam({ name: 'reason', required: false })],
      });

      // Act
      const error = validateCommandArgs(cmd, {});

      // Assert
      expect(error).toBeNull();
    });

    it('treats whitespace-only required parameter value as missing', () => {
      // Arrange
      const cmd = makeCommand({
        parameters: [makeParam({ name: 'user', required: true })],
      });

      // Act
      const error = validateCommandArgs(cmd, { user: '   ' });

      // Assert
      expect(typeof error).toBe('string');
      expect((error as string).length).toBeGreaterThan(0);
    });

    it('returns null for a command with no parameters', () => {
      // Arrange
      const cmd = makeCommand({ parameters: [] });

      // Act
      const error = validateCommandArgs(cmd, {});

      // Assert
      expect(error).toBeNull();
    });
  });

  // ---- API ----

  describe('commands API', () => {
    it('GET /api/v1/servers/{id}/commands fetches command list', async () => {
      // Arrange
      const serverId = 'srv-1';
      const commands = makeCommandList();

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => commands,
      });

      // Act
      const result = await api.get<BotCommand[]>(`/api/v1/servers/${serverId}/commands`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/commands`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(4);
    });

    it('API result maps to BotCommand shape', async () => {
      // Arrange
      const cmd = makeCommand();

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => [cmd],
      });

      // Act
      const result = await api.get<BotCommand[]>('/api/v1/servers/srv-1/commands');

      // Assert
      expect(result[0].name).toBe('ban');
      expect(result[0].parameters).toHaveLength(1);
    });
  });

  // ---- BotCommand shape ----

  describe('BotCommand data shape', () => {
    it('has all expected fields', () => {
      // Arrange
      const cmd = makeCommand();

      // Assert
      expect(cmd.id).toBeDefined();
      expect(cmd.name).toBeDefined();
      expect(cmd.description).toBeDefined();
      expect(cmd.parameters).toBeDefined();
      expect(cmd.botId).toBeDefined();
      expect(cmd.botName).toBeDefined();
    });

    it('parameter has name, description, required, and type', () => {
      // Arrange
      const param = makeParam({ name: 'target', required: false, type: 'channel' });

      // Assert
      expect(param.name).toBe('target');
      expect(param.required).toBe(false);
      expect(param.type).toBe('channel');
      expect(param.description).toBeDefined();
    });
  });
});
