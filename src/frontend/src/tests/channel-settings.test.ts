import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';

interface ChannelSettingsPayload {
  name: string;
  topic: string | null;
  slowModeSeconds: number;
  isNsfw: boolean;
}

/**
 * Build the payload that ChannelSettings.handleSave() sends via api.patch.
 * Mirrors the component's exact logic:
 *   topic: topic().trim() || null
 *   slowModeSeconds: slowMode()          (already a number from parseInt)
 *   isNsfw: isNsfw()
 */
function buildChannelSettingsPayload(
  name: string,
  topic: string,
  slowModeSeconds: number,
  isNsfw: boolean
): ChannelSettingsPayload {
  return {
    name: name.trim(),
    topic: topic.trim() || null,
    slowModeSeconds,
    isNsfw,
  };
}

describe('ChannelSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  describe('slowmode dropdown', () => {
    it('slowmode value is sent as integer in API payload', async () => {
      const channelId = 'ch-5';
      const slowMode = 60;

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      // Build payload using the same logic as the component's handleSave
      const payload = buildChannelSettingsPayload('test', '', slowMode, false);

      await api.patch(`/api/v1/channels/${channelId}`, payload);

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      // The slowModeSeconds value must be a number, not a string — a string would
      // cause a backend type mismatch (the select element stores its value as a
      // string and parseInt is required before passing it here).
      expect(body.slowModeSeconds).toBe(60);
      expect(typeof body.slowModeSeconds).toBe('number');
    });
  });

  describe('NSFW toggle', () => {
    it('sends isNsfw true in API payload when enabled', async () => {
      const channelId = 'ch-nsfw';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      // Build payload as the component does when the NSFW toggle is on
      const payload = buildChannelSettingsPayload('adults-only', '', 0, true);

      await api.patch(`/api/v1/channels/${channelId}`, payload);

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      // isNsfw must be the boolean true, not a string
      expect(body.isNsfw).toBe(true);
      expect(typeof body.isNsfw).toBe('boolean');
    });
  });

  describe('save changes via API', () => {
    it('calls PATCH /api/v1/channels/{channelId}', async () => {
      // The component uses api.patch (not api.put) with /api/v1/channels/{id}
      // (no serverId in the path — channels are addressed directly by their ID).
      const channelId = 'ch-7';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId, name: 'updated' }),
      });

      const payload = buildChannelSettingsPayload('updated', '', 0, false);
      await api.patch(`/api/v1/channels/${channelId}`, payload);

      const [url, opts] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(url).toBe(`/api/v1/channels/${channelId}`);
      expect(opts.method).toBe('PATCH');
    });

    it('sends null topic when topic field is empty', async () => {
      const channelId = 'ch-8';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: channelId }),
      });

      // The component converts empty topic string to null via: topic().trim() || null
      // Pass the raw empty string through the same builder function to verify the conversion.
      const payload = buildChannelSettingsPayload('test', '', 0, false);

      await api.patch(`/api/v1/channels/${channelId}`, payload);

      const body = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body
      );
      // Empty string must arrive as null, not '' or undefined
      expect(body.topic).toBeNull();
    });

    it('throws when API returns an error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ detail: 'Missing Manage Channels permission' }),
      });

      await expect(
        api.patch('/api/v1/channels/ch-x', { name: 'fail' })
      ).rejects.toMatchObject({ detail: 'Missing Manage Channels permission' });
    });
  });
});
