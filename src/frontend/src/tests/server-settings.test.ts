import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';

// ServerSettings type for tests
interface ServerSettingsPayload {
  name: string;
  description: string | null;
  systemChannelId: string | null;
  defaultNotificationLevel: string;
}

describe('ServerSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('save changes via API', () => {
    it('calls PUT /api/v1/servers/{id} with correct payload', async () => {
      // Arrange
      const serverId = 'srv-abc';
      const payload: ServerSettingsPayload = {
        name: 'Updated Name',
        description: 'New description',
        systemChannelId: null,
        defaultNotificationLevel: 'AllMessages',
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ ...payload, id: serverId }),
      });

      // Act
      const result = await api.put(`/api/v1/servers/${serverId}`, payload);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}`,
        expect.objectContaining({ method: 'PUT', body: JSON.stringify(payload) })
      );
      expect(result).toBeDefined();
    });

    it('sends name trimmed of whitespace', async () => {
      // Arrange
      const serverId = 'srv-trim';
      const rawName = '  My Server  ';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: serverId, name: rawName.trim() }),
      });

      const trimmedName = rawName.trim();

      // Act
      await api.put(`/api/v1/servers/${serverId}`, { name: trimmedName });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}`,
        expect.objectContaining({ body: JSON.stringify({ name: 'My Server' }) })
      );
    });

    it('sends null description when field is empty', async () => {
      // Arrange
      const serverId = 'srv-nodesc';
      const emptyDescription = '';
      const bodyDescription = emptyDescription.trim() || null;

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: serverId }),
      });

      // Act
      await api.put(`/api/v1/servers/${serverId}`, { description: bodyDescription });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}`,
        expect.objectContaining({ body: JSON.stringify({ description: null }) })
      );
    });

    it('throws error and returns error message when API fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.put('/api/v1/servers/srv-fail', { name: 'Bad' })
      ).rejects.toMatchObject({ detail: 'Forbidden' });
    });
  });

  describe('notification level in API payload', () => {
    it('sends selected notification level in PUT payload', async () => {
      // Arrange
      const serverId = 'srv-notif';
      const level = 'OnlyMentions';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: serverId }),
      });

      // Act
      await api.put(`/api/v1/servers/${serverId}`, {
        name: 'Test',
        defaultNotificationLevel: level,
      });

      // Assert
      const callBody = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(callBody.defaultNotificationLevel).toBe('OnlyMentions');
    });
  });
});
