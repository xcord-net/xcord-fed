import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  formatElapsedTime,
  getActivityLabel,
  buildPresencePayload,
  activityVerb,
} from '../components/RichPresence';
import type { Activity, ActivityType, RichPresenceData } from '../components/RichPresence';

// ---- Test data helpers ----

const makeActivity = (overrides: Partial<Activity> = {}): Activity => ({
  type: 'Playing',
  name: 'Minecraft',
  details: 'Survival Mode',
  startedAt: new Date(Date.now() - 90 * 60 * 1000).toISOString(), // 90 min ago
  ...overrides,
});

const makePresence = (overrides: Partial<RichPresenceData> = {}): RichPresenceData => ({
  status: 'online',
  activity: makeActivity(),
  ...overrides,
});

// ---- Tests ----

describe('rich-presence', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Activity type labels ----

  describe('activityVerb map', () => {
    it('Playing maps to "Playing"', () => {
      expect(activityVerb['Playing']).toBe('Playing');
    });

    it('Listening maps to "Listening to"', () => {
      expect(activityVerb['Listening']).toBe('Listening to');
    });

    it('Watching maps to "Watching"', () => {
      expect(activityVerb['Watching']).toBe('Watching');
    });

    it('Streaming maps to "Streaming"', () => {
      expect(activityVerb['Streaming']).toBe('Streaming');
    });

    it('Custom maps to empty string', () => {
      expect(activityVerb['Custom']).toBe('');
    });
  });

  // ---- getActivityLabel ----

  describe('getActivityLabel', () => {
    it('returns "Playing <name>" for Playing type', () => {
      const act = makeActivity({ type: 'Playing', name: 'Chess' });
      expect(getActivityLabel(act)).toBe('Playing Chess');
    });

    it('returns "Listening to <name>" for Listening type', () => {
      const act = makeActivity({ type: 'Listening', name: 'Chill Beats' });
      expect(getActivityLabel(act)).toBe('Listening to Chill Beats');
    });

    it('returns "Watching <name>" for Watching type', () => {
      const act = makeActivity({ type: 'Watching', name: 'YouTube' });
      expect(getActivityLabel(act)).toBe('Watching YouTube');
    });

    it('returns "Streaming <name>" for Streaming type', () => {
      const act = makeActivity({ type: 'Streaming', name: 'Live Coding' });
      expect(getActivityLabel(act)).toBe('Streaming Live Coding');
    });

    it('returns emoji + name for Custom type with emoji', () => {
      const act = makeActivity({ type: 'Custom', name: 'Feeling great', emoji: ':wave:' });
      expect(getActivityLabel(act)).toBe(':wave: Feeling great');
    });

    it('returns just name for Custom type without emoji', () => {
      const act = makeActivity({ type: 'Custom', name: 'Working from home', emoji: undefined });
      expect(getActivityLabel(act)).toBe('Working from home');
    });
  });

  // ---- formatElapsedTime ----

  describe('formatElapsedTime', () => {
    it('returns hours and minutes when elapsed >= 1 hour', () => {
      const start = new Date(Date.now() - 90 * 60 * 1000).toISOString();
      const result = formatElapsedTime(start);
      expect(result).toBe('1h 30m');
    });

    it('returns minutes only when elapsed < 1 hour', () => {
      const start = new Date(Date.now() - 45 * 60 * 1000).toISOString();
      const result = formatElapsedTime(start);
      expect(result).toBe('45 min');
    });

    it('returns "0 min" for future timestamps', () => {
      const start = new Date(Date.now() + 60_000).toISOString();
      const result = formatElapsedTime(start);
      expect(result).toBe('0 min');
    });

    it('returns "2h 0m" for exactly 2 hours elapsed', () => {
      const start = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
      const result = formatElapsedTime(start);
      expect(result).toBe('2h 0m');
    });
  });

  // ---- buildPresencePayload ----

  describe('buildPresencePayload', () => {
    it('includes status and activity when activity is provided', () => {
      const act = makeActivity();
      const payload = buildPresencePayload('online', act);
      expect(payload.status).toBe('online');
      expect(payload.activity).toEqual(act);
    });

    it('sets activity to null when no activity provided', () => {
      const payload = buildPresencePayload('idle');
      expect(payload.status).toBe('idle');
      expect(payload.activity).toBeNull();
    });

    it('builds payload for dnd status', () => {
      const payload = buildPresencePayload('dnd', makeActivity({ type: 'Custom', name: 'Busy' }));
      expect(payload.status).toBe('dnd');
      expect((payload.activity as Activity).type).toBe('Custom');
    });
  });

  // ---- API calls ----

  describe('PUT /api/v1/users/@me/presence', () => {
    it('calls the presence endpoint with status and activity', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.put('/api/v1/users/@me/presence', {
        status: 'online',
        activity: { type: 'Playing', name: 'Minecraft' },
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/presence',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ status: 'online', activity: { type: 'Playing', name: 'Minecraft' } }),
        }),
      );
    });

    it('sends null activity to clear presence activity', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.put('/api/v1/users/@me/presence', { status: 'online', activity: null });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/presence',
        expect.objectContaining({
          body: JSON.stringify({ status: 'online', activity: null }),
        }),
      );
    });

    it('throws when API returns error', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Unauthorized' }),
      });

      // Act & Assert
      await expect(
        api.put('/api/v1/users/@me/presence', { status: 'online' }),
      ).rejects.toMatchObject({ error: 'Unauthorized' });
    });
  });

  // ---- RichPresenceData shape ----

  describe('RichPresenceData shape', () => {
    it('presence with activity has required fields', () => {
      const presence = makePresence();
      expect(presence.status).toBeDefined();
      expect(presence.activity).toBeDefined();
      expect(presence.activity!.type).toBeDefined();
      expect(presence.activity!.name).toBeDefined();
    });

    it('presence without activity is valid', () => {
      const presence = makePresence({ activity: undefined });
      expect(presence.status).toBe('online');
      expect(presence.activity).toBeUndefined();
    });

    it('all five activity types are valid ActivityType values', () => {
      const types: ActivityType[] = ['Playing', 'Listening', 'Watching', 'Streaming', 'Custom'];
      expect(types).toHaveLength(5);
      for (const t of types) {
        expect(activityVerb).toHaveProperty(t);
      }
    });
  });
});
