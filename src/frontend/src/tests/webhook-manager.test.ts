import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  validateWebhookUrl,
  validateWebhookName,
  toggleEvent,
  type OutgoingWebhook,
  type WebhookEvent,
  ALL_WEBHOOK_EVENTS,
  WEBHOOK_EVENT_LABELS,
} from '../components/WebhookManager';

// ---- Test data ----

const makeWebhook = (overrides?: Partial<OutgoingWebhook>): OutgoingWebhook => ({
  id: 'wh-1',
  serverId: 'server-abc',
  name: 'Deploy Notifier',
  targetUrl: 'https://example.com/webhook',
  events: ['message.created', 'member.joined'],
  secret: 'wh-secret-abc123',
  enabled: true,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
});

// ---- Tests ----

describe('WebhookManager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- URL validation ----

  describe('validateWebhookUrl', () => {
    it('returns null for a valid https URL', () => {
      // Act
      const result = validateWebhookUrl('https://example.com/hook');

      // Assert
      expect(result).toBeNull();
    });

    it('returns null for a valid http URL', () => {
      // Act
      const result = validateWebhookUrl('http://localhost:3000/hook');

      // Assert
      expect(result).toBeNull();
    });

    it('returns an error for an empty URL', () => {
      // Act
      const result = validateWebhookUrl('');

      // Assert
      expect(typeof result).toBe('string');
      expect(result).toContain('required');
    });

    it('returns an error for a non-URL string', () => {
      // Act
      const result = validateWebhookUrl('not-a-url');

      // Assert
      expect(typeof result).toBe('string');
      expect((result as string).length).toBeGreaterThan(0);
    });

    it('returns an error for ftp:// URLs', () => {
      // Act
      const result = validateWebhookUrl('ftp://example.com/hook');

      // Assert
      expect(typeof result).toBe('string');
      expect((result as string).length).toBeGreaterThan(0);
    });
  });

  // ---- Name validation ----

  describe('validateWebhookName', () => {
    it('returns null for a valid name', () => {
      // Act
      const result = validateWebhookName('My Webhook');

      // Assert
      expect(result).toBeNull();
    });

    it('returns an error for an empty name', () => {
      // Act
      const result = validateWebhookName('');

      // Assert
      expect(typeof result).toBe('string');
      expect(result).toContain('required');
    });

    it('returns an error for a name longer than 64 characters', () => {
      // Act
      const result = validateWebhookName('a'.repeat(65));

      // Assert
      expect(typeof result).toBe('string');
      expect((result as string).length).toBeGreaterThan(0);
    });

    it('accepts a name exactly 64 characters long', () => {
      // Act
      const result = validateWebhookName('a'.repeat(64));

      // Assert
      expect(result).toBeNull();
    });
  });

  // ---- Event toggle ----

  describe('toggleEvent', () => {
    it('adds an event when not already in the list', () => {
      // Arrange
      const events: WebhookEvent[] = ['message.created'];

      // Act
      const result = toggleEvent(events, 'member.joined');

      // Assert
      expect(result).toContain('member.joined');
      expect(result).toHaveLength(2);
    });

    it('removes an event when already in the list', () => {
      // Arrange
      const events: WebhookEvent[] = ['message.created', 'member.joined'];

      // Act
      const result = toggleEvent(events, 'message.created');

      // Assert
      expect(result).not.toContain('message.created');
      expect(result).toHaveLength(1);
    });

    it('does not mutate the original array', () => {
      // Arrange
      const events: WebhookEvent[] = ['message.created'];

      // Act
      toggleEvent(events, 'member.joined');

      // Assert
      expect(events).toHaveLength(1);
    });
  });

  // ---- Event catalog ----

  describe('ALL_WEBHOOK_EVENTS and labels', () => {
    it('ALL_WEBHOOK_EVENTS contains the expected events', () => {
      // Assert - exact catalog so accidentally removing any event fails the test
      expect(ALL_WEBHOOK_EVENTS).toEqual([
        'message.created',
        'message.deleted',
        'member.joined',
        'member.left',
        'channel.created',
        'channel.deleted',
        'group.updated',
      ]);
    });

    it('every event in ALL_WEBHOOK_EVENTS has a non-empty label', () => {
      // Assert
      for (const evt of ALL_WEBHOOK_EVENTS) {
        expect(WEBHOOK_EVENT_LABELS[evt]).toEqual(expect.any(String));
        expect(WEBHOOK_EVENT_LABELS[evt].length).toBeGreaterThan(0);
      }
    });
  });

  // ---- API calls ----

  describe('fetching webhooks', () => {
    it('calls GET /api/v1/servers/{id}/webhooks/outgoing', async () => {
      // Arrange
      const serverId = 'server-abc';
      const mockWebhooks: OutgoingWebhook[] = [makeWebhook()];

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => mockWebhooks,
      });

      // Act
      const result = await api.get<OutgoingWebhook[]>(
        `/api/v1/servers/${serverId}/webhooks/outgoing`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/webhooks/outgoing`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Deploy Notifier');
    });

    it('throws when API returns an error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Forbidden' }),
      });

      // Act & Assert
      await expect(
        api.get('/api/v1/servers/srv/webhooks/outgoing'),
      ).rejects.toMatchObject({ error: 'Forbidden' });
    });
  });

  describe('creating a webhook', () => {
    it('calls POST /api/v1/servers/{id}/webhooks/outgoing with correct body', async () => {
      // Arrange
      const serverId = 'server-abc';
      const newWebhook = makeWebhook({ id: 'wh-new', name: 'New Hook' });

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => newWebhook,
      });

      // Act
      const result = await api.post<OutgoingWebhook>(
        `/api/v1/servers/${serverId}/webhooks/outgoing`,
        { name: 'New Hook', targetUrl: 'https://example.com/hook', events: ['message.created'] },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/webhooks/outgoing`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            name: 'New Hook',
            targetUrl: 'https://example.com/hook',
            events: ['message.created'],
          }),
        }),
      );
      expect(result.id).toBe('wh-new');
    });

  });

  describe('deleting a webhook', () => {
    it('calls DELETE /api/v1/servers/{id}/webhooks/outgoing/{webhookId}', async () => {
      // Arrange
      const serverId = 'server-abc';
      const webhookId = 'wh-1';

      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.delete(`/api/v1/servers/${serverId}/webhooks/outgoing/${webhookId}`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/webhooks/outgoing/${webhookId}`,
        expect.objectContaining({ method: 'DELETE' }),
      );
    });

  });
});
