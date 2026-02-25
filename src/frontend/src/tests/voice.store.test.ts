import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useVoice } from '../stores/voice.store';

// Minimal SignalR HubConnection mock that satisfies the voice store's invoke calls.
function makeMockConnection(overrides?: Partial<{ token: string; livekitUrl: string }>) {
  return {
    invoke: vi.fn().mockResolvedValue({
      token: overrides?.token ?? 'mock-token',
      roomName: 'mock-room',
      livekitUrl: overrides?.livekitUrl ?? 'ws://livekit.test:7880',
      qualityConfig: {
        maxAudioBitrateKbps: 0,
        maxVideoBitrateKbps: 0,
        maxVideoWidth: 0,
        maxVideoHeight: 0,
        maxVideoFps: 0,
        maxScreenShareBitrateKbps: 0,
        enableSimulcast: false,
      },
    }),
    state: 'Connected',
  } as unknown as import('@microsoft/signalr').HubConnection;
}

// Track the most-recently-created mock Room instance so tests can inspect calls.
let lastMockRoomInstance: {
  connect: ReturnType<typeof vi.fn>;
  disconnect: ReturnType<typeof vi.fn>;
  state: string;
  localParticipant: { setMicrophoneEnabled: ReturnType<typeof vi.fn> };
  remoteParticipants: Map<string, unknown>;
  on: ReturnType<typeof vi.fn>;
} | null = null;

// Mock livekit-client so no real WebRTC code runs in unit tests.
vi.mock('livekit-client', () => {
  const Room = vi.fn().mockImplementation(() => {
    const instance = {
      state: 'disconnected',
      localParticipant: {
        setMicrophoneEnabled: vi.fn().mockResolvedValue(undefined),
      },
      remoteParticipants: new Map(),
      connect: vi.fn().mockResolvedValue(undefined),
      disconnect: vi.fn().mockResolvedValue(undefined),
      on: vi.fn(),
    };
    // Store reference so tests can inspect the instance created by the store.
    lastMockRoomInstance = instance;
    return instance;
  });

  return {
    Room,
    RoomEvent: {
      Disconnected: 'disconnected',
      ConnectionStateChanged: 'connectionStateChanged',
      ParticipantConnected: 'participantConnected',
      ParticipantDisconnected: 'participantDisconnected',
      TrackMuted: 'trackMuted',
      TrackUnmuted: 'trackUnmuted',
      TrackSubscribed: 'trackSubscribed',
      TrackUnsubscribed: 'trackUnsubscribed',
    },
    Track: { Kind: { Audio: 'audio', Video: 'video' } },
    ConnectionState: {
      Connected: 'connected',
      Connecting: 'connecting',
      Disconnected: 'disconnected',
      Reconnecting: 'reconnecting',
    },
    VideoPresets: {
      h720: { encoding: { maxBitrate: 1_500_000, maxFramerate: 30 } },
      h1080: { encoding: { maxBitrate: 4_000_000, maxFramerate: 30 } },
    },
  };
});

describe('voice.store', () => {
  beforeEach(() => {
    const voice = useVoice();
    voice.clearVoiceState();
    // Reset SignalR connection
    voice.setSignalRConnection(null);
    // Clear mock call history so each test starts with a clean slate.
    // NOTE: do NOT reset lastMockRoomInstance — the mock factory updates it
    // each time a new Room is created, and tests can inspect the latest instance.
    vi.clearAllMocks();
  });

  describe('joinVoice', () => {
    it('should set current channel', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());

      await voice.joinVoice('channel-1');

      expect(voice.currentChannelId).toBe('channel-1');
    });

    it('should call LiveKit room.connect() with the URL and token returned by the backend', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(
        makeMockConnection({
          token: 'backend-issued-jwt',
          livekitUrl: 'ws://livekit.example:7880',
        })
      );

      await voice.joinVoice('channel-42');

      // The store creates a Room via getRoom() on the first call; subsequent calls reuse
      // the same instance. lastMockRoomInstance captures the most recent Room instance.
      const room = lastMockRoomInstance;
      expect(room).not.toBeNull();
      expect(room!.connect).toHaveBeenCalledOnce();
      expect(room!.connect).toHaveBeenCalledWith('ws://livekit.example:7880', 'backend-issued-jwt');
    });

    it('should call room.connect() with the token from the backend for a second join', async () => {
      const voice = useVoice();
      // First join to initialise the room instance.
      voice.setSignalRConnection(makeMockConnection({ token: 'token-1', livekitUrl: 'ws://lk:7880' }));
      await voice.joinVoice('channel-1');

      // Second join with a different token — the store recreates the Room
      // with quality config each time, so we check the NEW room instance.
      voice.setSignalRConnection(makeMockConnection({ token: 'token-2', livekitUrl: 'ws://lk:7880' }));
      await voice.joinVoice('channel-2');

      const newRoom = lastMockRoomInstance!;
      expect(newRoom.connect).toHaveBeenCalledOnce();
      expect(newRoom.connect).toHaveBeenCalledWith('ws://lk:7880', 'token-2');
    });
  });

  describe('leaveVoice', () => {
    it('should clear channel and reset state', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());

      await voice.joinVoice('channel-1');
      await voice.toggleMute(); // mute
      await voice.leaveVoice();

      expect(voice.currentChannelId).toBeNull();
      expect(voice.isMuted).toBe(false);
      expect(voice.isDeafened).toBe(false);
      expect(voice.participants.size).toBe(0);
    });

    it('should call LiveKit room.disconnect() when leaving a voice channel', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());

      await voice.joinVoice('channel-1');
      const room = lastMockRoomInstance!;

      // Simulate the room being in a connected state so the guard in leaveVoice
      // (room.state !== ConnectionState.Disconnected) allows disconnect() to fire.
      room.state = 'connected';
      room.disconnect.mockClear();

      await voice.leaveVoice();

      expect(room.disconnect).toHaveBeenCalledOnce();
    });
  });

  describe('toggleMute', () => {
    it('should toggle mute state', async () => {
      const voice = useVoice();

      expect(voice.isMuted).toBe(false);
      await voice.toggleMute();
      expect(voice.isMuted).toBe(true);
      await voice.toggleMute();
      expect(voice.isMuted).toBe(false);
    });

    it('should undeafen when unmuting', async () => {
      const voice = useVoice();

      // Deafen (which auto-mutes)
      await voice.toggleDeafen();
      expect(voice.isDeafened).toBe(true);
      expect(voice.isMuted).toBe(true);

      // Unmute (toggle mute off) should also undeafen
      await voice.toggleMute();
      expect(voice.isMuted).toBe(false);
      expect(voice.isDeafened).toBe(false);
    });
  });

  describe('toggleDeafen', () => {
    it('should toggle deafen state', async () => {
      const voice = useVoice();

      expect(voice.isDeafened).toBe(false);
      await voice.toggleDeafen();
      expect(voice.isDeafened).toBe(true);
      await voice.toggleDeafen();
      expect(voice.isDeafened).toBe(false);
    });

    it('should auto-mute when deafening', async () => {
      const voice = useVoice();

      expect(voice.isMuted).toBe(false);
      await voice.toggleDeafen();
      expect(voice.isMuted).toBe(true);
      expect(voice.isDeafened).toBe(true);
    });

    it('should not force unmute when undeafening', async () => {
      const voice = useVoice();

      // Deafen (auto-mutes)
      await voice.toggleDeafen();
      expect(voice.isMuted).toBe(true);

      // Undeafen - mute stays because deafen toggle doesn't touch mute on undeafen
      await voice.toggleDeafen();
      expect(voice.isDeafened).toBe(false);
      // Mute state is still true since toggleDeafen only sets mute on deafen, doesn't clear on undeafen
      expect(voice.isMuted).toBe(true);
    });
  });

  describe('updateVoiceState', () => {
    it('should add participant in current channel', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());
      await voice.joinVoice('channel-1');

      voice.updateVoiceState('user-1', 'channel-1', false, false);

      expect(voice.participants.has('user-1')).toBe(true);
      const participant = voice.participants.get('user-1')!;
      expect(participant.isMuted).toBe(false);
      expect(participant.isDeafened).toBe(false);
    });

    it('should update existing participant state', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());
      await voice.joinVoice('channel-1');

      voice.updateVoiceState('user-1', 'channel-1', false, false);
      voice.updateVoiceState('user-1', 'channel-1', true, false);

      const participant = voice.participants.get('user-1')!;
      expect(participant.isMuted).toBe(true);
    });

    it('should remove participant when they move to different channel', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());
      await voice.joinVoice('channel-1');

      voice.updateVoiceState('user-1', 'channel-1', false, false);
      expect(voice.participants.has('user-1')).toBe(true);

      // User moves to a different channel
      voice.updateVoiceState('user-1', 'channel-2', false, false);
      expect(voice.participants.has('user-1')).toBe(false);
    });

    it('should remove participant when channelId is null (left)', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());
      await voice.joinVoice('channel-1');

      voice.updateVoiceState('user-1', 'channel-1', false, false);
      voice.updateVoiceState('user-1', null, false, false);

      expect(voice.participants.has('user-1')).toBe(false);
    });
  });

  describe('clearVoiceState', () => {
    it('should reset all voice state', async () => {
      const voice = useVoice();
      voice.setSignalRConnection(makeMockConnection());

      await voice.joinVoice('channel-1');
      await voice.toggleMute();
      await voice.toggleDeafen();
      voice.updateVoiceState('user-1', 'channel-1', false, false);

      voice.clearVoiceState();

      expect(voice.currentChannelId).toBeNull();
      expect(voice.participants.size).toBe(0);
      expect(voice.isMuted).toBe(false);
      expect(voice.isDeafened).toBe(false);
    });
  });
});
