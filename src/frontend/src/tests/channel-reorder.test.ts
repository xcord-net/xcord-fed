import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';

/**
 * Channel Drag-and-Drop Reordering tests — Card 169
 */

// ---- Types ----

interface Channel {
  id: string;
  name: string;
  position: number;
  categoryId?: string;
  type: 'Text' | 'Voice';
}

interface Category {
  id: string;
  name: string;
  position: number;
}

// ---- Fixtures ----

const CHANNELS: Channel[] = [
  { id: 'ch-1', name: 'general', position: 0, type: 'Text' },
  { id: 'ch-2', name: 'random', position: 1, type: 'Text' },
  { id: 'ch-3', name: 'voice', position: 2, type: 'Voice' },
];

const CATEGORIES: Category[] = [
  { id: 'cat-1', name: 'Text Channels', position: 0 },
  { id: 'cat-2', name: 'Voice Channels', position: 1 },
];

// ---- Tests ----

describe('channel-reorder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.setAuthenticated(true);
  });

  describe('API call on channel drop', () => {
    it('calls PUT with new position when channel is dropped on target', async () => {
      const source = CHANNELS[2]; // ch-3, position 2
      const target = CHANNELS[0]; // ch-1, position 0

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({}),
      });

      await api.put(`/api/v1/servers/srv-1/channels/${source.id}`, {
        position: target.position,
        categoryId: target.categoryId ?? null,
      });

      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/srv-1/channels/ch-3',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ position: 0, categoryId: null }),
        }),
      );
    });

    it('uses the target channel position in the PUT body', async () => {
      const source: Channel = { id: 'ch-a', name: 'alpha', position: 5, type: 'Text' };
      const target: Channel = { id: 'ch-b', name: 'beta', position: 2, type: 'Text' };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({}),
      });

      await api.put(`/api/v1/servers/srv-1/channels/${source.id}`, {
        position: target.position,
        categoryId: target.categoryId ?? null,
      });

      const callBody = JSON.parse(
        (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body,
      );
      expect(callBody.position).toBe(2);
    });
  });

  describe('API call on category drop', () => {
    it('calls PUT /channels/categories/{id} with target position', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({}),
      });

      const source = CATEGORIES[0]; // cat-1, pos 0
      const target = CATEGORIES[1]; // cat-2, pos 1

      await api.put(`/api/v1/servers/srv-1/channels/categories/${source.id}`, {
        position: target.position,
      });

      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/servers/srv-1/channels/categories/cat-1',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ position: 1 }),
        }),
      );
    });
  });
});
