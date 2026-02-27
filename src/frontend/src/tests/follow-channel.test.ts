import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import {
  fetchFollows,
  fetchAvailableChannels,
  followChannel,
  unfollowChannel,
} from '../components/FollowChannel';
import type { Follow } from '../components/FollowChannel';

describe('follow-channel', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.setAuthenticated(false);
  });

  describe('channel selector dropdown', () => {
    it('should load available channels excluding source channel', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [
          { id: 'ch-1', name: 'general', type: 'Text' },
          { id: 'ch-2', name: 'announcements', type: 'Announcement' },
          { id: 'ch-source', name: 'news', type: 'Text' },
        ],
      });

      const result = await fetchAvailableChannels('server-1', 'ch-source');

      expect(result.error).toBe('');
      // Source channel and non-text channels are excluded
      expect(result.channels.find((c) => c.id === 'ch-source')).toBeUndefined();
      expect(result.channels.find((c) => c.id === 'ch-2')).toBeUndefined();
      expect(result.channels.find((c) => c.id === 'ch-1')).toBeDefined();
    });

    it('should return error when channel loading fails', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ error: 'Not a member' }),
      });

      const result = await fetchAvailableChannels('server-1', 'ch-source');

      expect(result.error).toBe('Not a member');
      expect(result.channels).toHaveLength(0);
    });
  });

  describe('current follows list', () => {
    it('should load current follows from API', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [
          {
            id: 'sub-1',
            targetChannelId: 'ch-1',
            targetServerId: 'srv-1',
            targetChannelName: 'general',
            createdAt: '2026-01-01T00:00:00Z',
          },
        ],
      });

      const result = await fetchFollows('server-1', 'channel-1');

      expect(result.error).toBe('');
      expect(result.follows).toHaveLength(1);
      expect(result.follows[0].targetChannelName).toBe('general');
    });

    it('should return empty list when no follows exist', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [],
      });

      const result = await fetchFollows('server-1', 'channel-1');

      expect(result.error).toBe('');
      expect(result.follows).toHaveLength(0);
    });
  });

  describe('follow API call', () => {
    it('should call followers endpoint with target channel', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 201,
        json: async () => ({
          id: 'sub-1',
          targetChannelId: 'ch-1',
          targetServerId: 'srv-1',
          targetChannelName: 'general',
          createdAt: '2026-01-01T00:00:00Z',
        }),
      });

      const result = await followChannel('server-1', 'channel-1', 'ch-1');

      expect(result.error).toBe('');
      expect(result.follow?.id).toBe('sub-1');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/server-1/channels/channel-1/followers',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ targetChannelId: 'ch-1' }),
        })
      );
    });

    it('should return error when no channel is selected', async () => {
      globalThis.fetch = vi.fn();

      const result = await followChannel('server-1', 'channel-1', '');

      expect(result.error).toBe('Please select a channel');
      expect(result.follow).toBeNull();
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should return API error on duplicate follow', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Already following this channel' }),
      });

      const result = await followChannel('server-1', 'channel-1', 'ch-1');

      expect(result.error).toBe('Already following this channel');
      expect(result.follow).toBeNull();
    });
  });

  describe('unfollow button', () => {
    it('should call DELETE endpoint to unfollow', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      const result = await unfollowChannel('server-1', 'channel-1', 'sub-1');

      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/server-1/channels/channel-1/followers/sub-1',
        expect.objectContaining({ method: 'DELETE' })
      );
    });

    it('should return error when unfollow fails', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Follow subscription not found' }),
      });

      const result = await unfollowChannel('server-1', 'channel-1', 'sub-999');

      expect(result.success).toBe(false);
      expect(result.error).toBe('Follow subscription not found');
    });

    it('should return generic error when unfollow API returns no message', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({}),
      });

      const result = await unfollowChannel('server-1', 'channel-1', 'sub-1');

      expect(result.success).toBe(false);
      expect(result.error).toBe('Failed to unfollow channel');
    });
  });
});
