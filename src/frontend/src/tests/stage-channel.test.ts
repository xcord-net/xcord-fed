import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { StageParticipant, StageChannelData } from '../components/StageChannel';
import {
  getParticipantRole,
  canHostInvite,
  buildSpeakRequestUrl,
  sortSpeakersFirst,
  moveSpeakerToAudience,
  moveAudienceToSpeaker,
} from '../components/StageChannel';

// ---- Test data factories ----

const makeParticipant = (overrides: Partial<StageParticipant> = {}): StageParticipant => ({
  userId: 'user-1',
  displayName: 'Alice',
  role: 'audience',
  isMuted: false,
  ...overrides,
});

const makeStage = (overrides: Partial<StageChannelData> = {}): StageChannelData => ({
  channelId: 'ch-1',
  channelName: 'Main Stage',
  serverId: 'srv-1',
  topic: 'Weekly AMA',
  speakers: [
    makeParticipant({ userId: 'user-host', displayName: 'Bob', role: 'host', isMuted: false }),
    makeParticipant({ userId: 'user-2', displayName: 'Carol', role: 'speaker', isMuted: false }),
  ],
  audience: [
    makeParticipant({ userId: 'user-3', displayName: 'Dave', role: 'audience', isMuted: false }),
    makeParticipant({ userId: 'user-4', displayName: 'Eve', role: 'audience', isMuted: false }),
  ],
  myUserId: 'user-3',
  myRole: 'audience',
  hasPendingSpeakRequest: false,
  ...overrides,
});

// ---- Tests ----

describe('StageChannel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Data shape ----

  describe('stage channel data shape', () => {
    it('has correct channelName', () => {
      const stage = makeStage({ channelName: 'AMA Stage' });
      expect(stage.channelName).toBe('AMA Stage');
    });

    it('has speakers and audience lists', () => {
      const stage = makeStage();
      expect(stage.speakers).toHaveLength(2);
      expect(stage.audience).toHaveLength(2);
    });

    it('topic is optional on stage data', () => {
      const stage = makeStage({ topic: undefined });
      expect(stage.topic).toBeUndefined();
    });

    it('hasPendingSpeakRequest defaults to false', () => {
      const stage = makeStage();
      expect(stage.hasPendingSpeakRequest).toBe(false);
    });
  });

  // ---- getParticipantRole helper ----

  describe('getParticipantRole', () => {
    it('returns "Host" for host role', () => {
      const p = makeParticipant({ role: 'host' });
      expect(getParticipantRole(p)).toBe('Host');
    });

    it('returns "Speaker" for speaker role', () => {
      const p = makeParticipant({ role: 'speaker' });
      expect(getParticipantRole(p)).toBe('Speaker');
    });

    it('returns "Audience" for audience role', () => {
      const p = makeParticipant({ role: 'audience' });
      expect(getParticipantRole(p)).toBe('Audience');
    });
  });

  // ---- canHostInvite helper ----

  describe('canHostInvite', () => {
    it('returns true when myRole is host', () => {
      expect(canHostInvite('host')).toBe(true);
    });

    it('returns false when myRole is speaker', () => {
      expect(canHostInvite('speaker')).toBe(false);
    });

    it('returns false when myRole is audience', () => {
      expect(canHostInvite('audience')).toBe(false);
    });
  });

  // ---- buildSpeakRequestUrl helper ----

  describe('buildSpeakRequestUrl', () => {
    it('builds the correct request-to-speak URL', () => {
      const url = buildSpeakRequestUrl('srv-abc', 'ch-xyz');
      expect(url).toBe('/api/v1/servers/srv-abc/channels/ch-xyz/stage/request-to-speak');
    });
  });

  // ---- Participant role transitions ----

  describe('participant role transitions', () => {
    it('moveSpeakerToAudience removes from speakers list', () => {
      const stage = makeStage();
      const updated = moveSpeakerToAudience(stage, 'user-2');
      expect(updated.speakers.find((p) => p.userId === 'user-2')).toBeUndefined();
    });

    it('moveSpeakerToAudience adds participant to audience list', () => {
      const stage = makeStage();
      const updated = moveSpeakerToAudience(stage, 'user-2');
      expect(updated.audience.find((p) => p.userId === 'user-2')).toBeDefined();
    });

    it('moveSpeakerToAudience sets role to audience', () => {
      const stage = makeStage();
      const updated = moveSpeakerToAudience(stage, 'user-2');
      const moved = updated.audience.find((p) => p.userId === 'user-2');
      expect(moved?.role).toBe('audience');
    });

    it('moveAudienceToSpeaker removes from audience list', () => {
      const stage = makeStage();
      const updated = moveAudienceToSpeaker(stage, 'user-3');
      expect(updated.audience.find((p) => p.userId === 'user-3')).toBeUndefined();
    });

    it('moveAudienceToSpeaker adds participant to speakers list', () => {
      const stage = makeStage();
      const updated = moveAudienceToSpeaker(stage, 'user-3');
      expect(updated.speakers.find((p) => p.userId === 'user-3')).toBeDefined();
    });

    it('role transitions do not mutate original stage', () => {
      const stage = makeStage();
      moveSpeakerToAudience(stage, 'user-2');
      // Original unchanged
      expect(stage.speakers).toHaveLength(2);
    });

    it('sortSpeakersFirst puts hosts before speakers before audience', () => {
      const participants: StageParticipant[] = [
        makeParticipant({ userId: 'u1', role: 'audience' }),
        makeParticipant({ userId: 'u2', role: 'speaker' }),
        makeParticipant({ userId: 'u3', role: 'host' }),
      ];
      const sorted = sortSpeakersFirst(participants);
      expect(sorted[0].role).toBe('host');
      expect(sorted[1].role).toBe('speaker');
      expect(sorted[2].role).toBe('audience');
    });
  });

  // ---- Request-to-speak API ----

  describe('request-to-speak API', () => {
    it('POST request-to-speak hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await api.post(buildSpeakRequestUrl(serverId, channelId), {});

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage/request-to-speak`,
        expect.objectContaining({ method: 'POST' }),
      );
    });
  });

  // ---- Invite to speak API ----

  describe('invite-to-speak API (host controls)', () => {
    it('POST invite-to-speak hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';
      const userId = 'user-3';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await api.post(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage/invite-to-speak`,
        { userId },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage/invite-to-speak`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ userId }),
        }),
      );
    });

    it('POST move-to-audience hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';
      const userId = 'user-2';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await api.post(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage/move-to-audience`,
        { userId },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage/move-to-audience`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ userId }),
        }),
      );
    });
  });

  // ---- GET stage channel API ----

  describe('load stage channel API', () => {
    it('GET stage channel hits correct URL', async () => {
      // Arrange
      const serverId = 'srv-1';
      const channelId = 'ch-1';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => makeStage(),
      });

      // Act
      const result = await api.get<StageChannelData>(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.channelName).toBe('Main Stage');
    });

    it('throws on API error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Not found' }),
      });

      await expect(
        api.get('/api/v1/servers/srv-1/channels/ch-missing/stage'),
      ).rejects.toMatchObject({ error: 'Not found' });
    });
  });
});
