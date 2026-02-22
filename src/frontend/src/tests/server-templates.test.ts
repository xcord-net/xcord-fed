import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { ServerTemplate, TemplateChannel, TemplateRole } from '../components/ServerTemplates';
import {
  validateTemplateName,
  templateChannelCount,
  templateRoleCount,
} from '../components/ServerTemplates';

// ---- Pure logic helpers mirrored from ServerTemplates ----

function isCreateFormValid(serverName: string): boolean {
  return serverName.trim().length > 0;
}

// ---- Test data ----

const makeChannel = (overrides: Partial<TemplateChannel> = {}): TemplateChannel => ({
  name: 'general',
  type: 'Text',
  position: 0,
  ...overrides,
});

const makeRole = (overrides: Partial<TemplateRole> = {}): TemplateRole => ({
  name: 'Member',
  permissions: ['SendMessages', 'ReadMessages'],
  ...overrides,
});

const makeTemplate = (overrides: Partial<ServerTemplate> = {}): ServerTemplate => ({
  id: 'tpl-1',
  name: 'Community Server',
  description: 'A great starter template',
  sourceServerId: 'srv-1',
  channels: [
    makeChannel({ name: 'general', type: 'Text', position: 0 }),
    makeChannel({ name: 'announcements', type: 'Text', position: 1 }),
    makeChannel({ name: 'Voice Lounge', type: 'Voice', position: 2 }),
  ],
  roles: [
    makeRole({ name: 'Admin', permissions: ['Administrator'] }),
    makeRole({ name: 'Member', permissions: ['SendMessages'] }),
  ],
  usageCount: 42,
  createdAt: new Date('2026-01-01T00:00:00Z').toISOString(),
  ...overrides,
});

// ---- Tests ----

describe('ServerTemplates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Template shape ----

  describe('template data shape', () => {
    it('template has name and id', () => {
      // Arrange
      const template = makeTemplate({ id: 'tpl-abc', name: 'Gaming Server' });

      // Assert
      expect(template.id).toBe('tpl-abc');
      expect(template.name).toBe('Gaming Server');
    });

    it('template description is optional', () => {
      // Arrange
      const template = makeTemplate({ description: undefined });

      // Assert
      expect(template.description).toBeUndefined();
    });

    it('template tracks usage count', () => {
      // Arrange
      const template = makeTemplate({ usageCount: 7 });

      // Assert
      expect(template.usageCount).toBe(7);
    });

    it('template contains channels array', () => {
      // Arrange
      const template = makeTemplate();

      // Assert
      expect(Array.isArray(template.channels)).toBe(true);
    });

    it('template contains roles array', () => {
      // Arrange
      const template = makeTemplate();

      // Assert
      expect(Array.isArray(template.roles)).toBe(true);
    });
  });

  // ---- Channel and role counts ----

  describe('templateChannelCount and templateRoleCount', () => {
    it('returns the correct channel count', () => {
      // Arrange
      const template = makeTemplate();

      // Act
      const count = templateChannelCount(template);

      // Assert
      expect(count).toBe(3);
    });

    it('returns 0 channel count for empty channels array', () => {
      // Arrange
      const template = makeTemplate({ channels: [] });

      // Act
      const count = templateChannelCount(template);

      // Assert
      expect(count).toBe(0);
    });

    it('returns the correct role count', () => {
      // Arrange
      const template = makeTemplate();

      // Act
      const count = templateRoleCount(template);

      // Assert
      expect(count).toBe(2);
    });

    it('returns 0 role count for empty roles array', () => {
      // Arrange
      const template = makeTemplate({ roles: [] });

      // Act
      const count = templateRoleCount(template);

      // Assert
      expect(count).toBe(0);
    });
  });

  // ---- Template name validation ----

  describe('validateTemplateName', () => {
    it('returns null for a valid name', () => {
      // Act
      const error = validateTemplateName('Community Server');

      // Assert
      expect(error).toBeNull();
    });

    it('returns error for empty name', () => {
      // Act
      const error = validateTemplateName('');

      // Assert
      expect(error).toBe('Template name is required.');
    });

    it('returns error for whitespace-only name', () => {
      // Act
      const error = validateTemplateName('   ');

      // Assert
      expect(error).toBe('Template name is required.');
    });

    it('returns error for name exceeding 100 characters', () => {
      // Arrange
      const longName = 'a'.repeat(101);

      // Act
      const error = validateTemplateName(longName);

      // Assert
      expect(error).toBe('Template name must be 100 characters or fewer.');
    });

    it('accepts a name exactly 100 characters long', () => {
      // Arrange
      const exactName = 'a'.repeat(100);

      // Act
      const error = validateTemplateName(exactName);

      // Assert
      expect(error).toBeNull();
    });
  });

  // ---- Create from template form validation ----

  describe('create from template form validation', () => {
    it('form is invalid when server name is empty', () => {
      expect(isCreateFormValid('')).toBe(false);
    });

    it('form is invalid when server name is whitespace-only', () => {
      expect(isCreateFormValid('   ')).toBe(false);
    });

    it('form is valid when server name is provided', () => {
      expect(isCreateFormValid('My New Server')).toBe(true);
    });
  });

  // ---- API: save template ----

  describe('save template API', () => {
    it('POST to save template hits the correct URL', async () => {
      // Arrange
      const serverId = 'srv-save-test';
      const newTemplate = makeTemplate({ id: 'tpl-new', name: 'Fresh Template' });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => newTemplate,
      });

      // Act
      const result = await api.post<ServerTemplate>(
        `/api/v1/servers/${serverId}/templates`,
        { name: 'Fresh Template', description: 'A fresh start' },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/templates`,
        expect.objectContaining({ method: 'POST' }),
      );
      expect(result.name).toBe('Fresh Template');
    });
  });

  // ---- API: list templates ----

  describe('list templates API', () => {
    it('GET to list templates hits the correct URL', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => [makeTemplate()],
      });

      // Act
      const result = await api.get<ServerTemplate[]>('/api/v1/server-templates');

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/server-templates',
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
    });

    it('returns empty array when no templates exist', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => [],
      });

      // Act
      const result = await api.get<ServerTemplate[]>('/api/v1/server-templates');

      // Assert
      expect(result).toHaveLength(0);
    });
  });

  // ---- API: create server from template ----

  describe('create server from template API', () => {
    it('POST to create server from template hits the correct URL', async () => {
      // Arrange
      const templateId = 'tpl-1';
      const serverName = 'Brand New Server';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 'new-srv-1' }),
      });

      // Act
      const result = await api.post<{ id: string }>('/api/v1/servers/from-template', {
        templateId,
        serverName,
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/from-template',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ templateId, serverName }),
        }),
      );
      expect(result.id).toBe('new-srv-1');
    });
  });

  // ---- Channel types ----

  describe('template channel types', () => {
    it('channel can be Text type', () => {
      const ch = makeChannel({ type: 'Text' });
      expect(ch.type).toBe('Text');
    });

    it('channel can be Voice type', () => {
      const ch = makeChannel({ type: 'Voice' });
      expect(ch.type).toBe('Voice');
    });

    it('channel can be Forum type', () => {
      const ch = makeChannel({ type: 'Forum' });
      expect(ch.type).toBe('Forum');
    });
  });
});
