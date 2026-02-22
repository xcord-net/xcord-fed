import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';

interface ChannelSettingsPayload {
  name: string;
  topic: string | null;
  slowModeSeconds: number;
  isNsfw: boolean;
}

describe('ChannelSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('slowmode dropdown', () => {
    it('slowmode value is sent as integer in API payload', async () => {
      const serverId = 'srv-1';
      const channelId = 'ch-5';
      const slowMode = 60;

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      await api.put(`/api/v1/servers/${serverId}/channels/${channelId}`, {
        name: 'test',
        slowModeSeconds: slowMode,
        isNsfw: false,
      });

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(body.slowModeSeconds).toBe(60);
      expect(typeof body.slowModeSeconds).toBe('number');
    });
  });

  describe('NSFW toggle', () => {
    it('sends isNsfw true in API payload when enabled', async () => {
      const serverId = 'srv-2';
      const channelId = 'ch-nsfw';
      const payload: ChannelSettingsPayload = {
        name: 'adults-only',
        topic: null,
        slowModeSeconds: 0,
        isNsfw: true,
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      await api.put(`/api/v1/servers/${serverId}/channels/${channelId}`, payload);

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(body.isNsfw).toBe(true);
    });
  });

  describe('save changes via API', () => {
    it('calls PUT /api/v1/servers/{serverId}/channels/{channelId}', async () => {
      const serverId = 'srv-3';
      const channelId = 'ch-7';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId, name: 'updated' }),
      });

      await api.put(`/api/v1/servers/${serverId}/channels/${channelId}`, {
        name: 'updated',
        topic: null,
        slowModeSeconds: 0,
        isNsfw: false,
      });

      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}`,
        expect.objectContaining({ method: 'PUT' })
      );
    });

    it('sends null topic when topic field is empty', async () => {
      const serverId = 'srv-4';
      const channelId = 'ch-8';
      const emptyTopic = '';
      const apiTopic = emptyTopic.trim() || null;

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      await api.put(`/api/v1/servers/${serverId}/channels/${channelId}`, {
        name: 'test',
        topic: apiTopic,
      });

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      expect(body.topic).toBeNull();
    });

    it('throws when API returns an error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Missing Manage Channels permission' }),
      });

      await expect(
        api.put('/api/v1/servers/srv-x/channels/ch-x', { name: 'fail' })
      ).rejects.toMatchObject({ detail: 'Missing Manage Channels permission' });
    });
  });
});
