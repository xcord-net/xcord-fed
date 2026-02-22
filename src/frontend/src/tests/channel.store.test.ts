import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useChannels } from '../stores/channel.store';

describe('channel.store', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('fetchChannels', () => {
    it('should load channels for a server', async () => {
      // Arrange
      const serverId = 'server-1';
      const mockResponse = {
        channels: [
          { id: 'ch-1', name: 'general', type: 'Text', serverId },
          { id: 'ch-2', name: 'voice', type: 'Voice', serverId },
        ],
        categories: [],
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockResponse,
      });

      const channels = useChannels();

      // Act
      await channels.fetchChannels(serverId);

      // Assert
      expect(channels.channels.length).toBe(2);
      expect(channels.channels[0].name).toBe('general');
      expect(channels.channels[1].name).toBe('voice');
    });

    it('should handle fetch errors', async () => {
      // Arrange
      const serverId = 'server-1';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Server not found' }),
      });

      const channels = useChannels();

      // Act & Assert
      await expect(channels.fetchChannels(serverId)).rejects.toThrow();
    });
  });

  describe('selectChannel', () => {
    it('should update selected channel ID', () => {
      // Arrange
      const channels = useChannels();

      // Act
      channels.selectChannel('ch-123');

      // Assert
      expect(channels.selectedChannelId).toBe('ch-123');
    });

    it('should allow deselecting channel', () => {
      // Arrange
      const channels = useChannels();
      channels.selectChannel('ch-123');

      // Act
      channels.selectChannel(null);

      // Assert
      expect(channels.selectedChannelId).toBeNull();
    });
  });
});
