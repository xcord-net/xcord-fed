import { createSignal, createRoot } from 'solid-js';
import {
  Room,
  RoomEvent,
  Track,
  TrackPublication,
  RemoteTrack,
  RemoteTrackPublication,
  RemoteParticipant,
  Participant,
  ConnectionState,
  VideoPresets,
} from 'livekit-client';
import type { HubConnection } from '@microsoft/signalr';
import type { VoiceParticipant } from '../types/voice';

interface QualityConfig {
  maxAudioBitrateKbps: number;
  maxVideoBitrateKbps: number;
  maxVideoWidth: number;
  maxVideoHeight: number;
  maxVideoFps: number;
  maxScreenShareBitrateKbps: number;
  enableSimulcast: boolean;
}

interface JoinVoiceResponse {
  token: string;
  roomName: string;
  livekitUrl: string;
  qualityConfig: QualityConfig;
}

const store = createRoot(() => {
  const [currentChannelId, setCurrentChannelId] = createSignal<string | null>(null);
  const [participants, setParticipants] = createSignal<Map<string, VoiceParticipant>>(new Map());
  const [isMuted, setIsMuted] = createSignal(false);
  const [isDeafened, setIsDeafened] = createSignal(false);
  const [isConnecting, setIsConnecting] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  return {
    currentChannelId,
    setCurrentChannelId,
    participants,
    setParticipants,
    isMuted,
    setIsMuted,
    isDeafened,
    setIsDeafened,
    isConnecting,
    setIsConnecting,
    error,
    setError,
  };
});

// Module-level LiveKit room instance — one room per browser tab.
let livekitRoom: Room | null = null;
// SignalR connection reference injected by signalr.store after connecting.
let signalrConnection: HubConnection | null = null;
// True when the server-side VoiceState was successfully created. When this is
// set, RoomEvent.Disconnected from a failed LiveKit connection attempt must NOT
// clear currentChannelId — the user is still considered "in voice" on the server.
let serverSideJoined = false;
// True when the user explicitly requested to leave (via leaveVoice or clearVoiceState).
// When set, RoomEvent.Disconnected is allowed to clear the state.
let intentionalLeave = false;

function getRoom(qualityConfig?: QualityConfig): Room {
  if (!livekitRoom) {
    const publishDefaults: Record<string, unknown> = {};

    if (qualityConfig) {
      if (qualityConfig.maxAudioBitrateKbps > 0) {
        publishDefaults.audioBitrate = qualityConfig.maxAudioBitrateKbps * 1000;
      }
      if (qualityConfig.maxVideoBitrateKbps > 0) {
        publishDefaults.videoEncoding = {
          maxBitrate: qualityConfig.maxVideoBitrateKbps * 1000,
          maxFramerate: qualityConfig.maxVideoFps || 30,
        };
      }
      if (qualityConfig.maxScreenShareBitrateKbps > 0) {
        publishDefaults.screenShareEncoding = {
          maxBitrate: qualityConfig.maxScreenShareBitrateKbps * 1000,
          maxFramerate: qualityConfig.maxVideoFps || 30,
        };
      }
      publishDefaults.simulcast = qualityConfig.enableSimulcast;

      if (qualityConfig.maxVideoWidth > 0 && qualityConfig.maxVideoHeight > 0) {
        publishDefaults.videoCodec = 'vp8';
      }
    }

    livekitRoom = new Room({
      adaptiveStream: true,
      dynacast: true,
      publishDefaults: Object.keys(publishDefaults).length > 0 ? publishDefaults : undefined,
    });
    attachRoomEventHandlers(livekitRoom);
  }
  return livekitRoom;
}

function attachRoomEventHandlers(room: Room): void {
  room.on(RoomEvent.Disconnected, () => {
    // Only clear voice state if this was an intentional leave or if the server-side
    // join was never established. A failed LiveKit connection (e.g. LiveKit server
    // unreachable) must not hide the VoicePanel when the server-side VoiceState exists.
    if (intentionalLeave || !serverSideJoined) {
      store.setCurrentChannelId(null);
      store.setParticipants(new Map());
      store.setIsMuted(false);
      store.setIsDeafened(false);
    }
    store.setIsConnecting(false);
    intentionalLeave = false;
  });

  room.on(RoomEvent.ConnectionStateChanged, (state: ConnectionState) => {
    if (state === ConnectionState.Connecting || state === ConnectionState.Reconnecting) {
      store.setIsConnecting(true);
    } else {
      store.setIsConnecting(false);
    }
  });

  // Keep participants map in sync with LiveKit room state.
  room.on(RoomEvent.ParticipantConnected, (participant: RemoteParticipant) => {
    const map = new Map(store.participants());
    map.set(participant.identity, {
      userId: participant.identity,
      isMuted: !participant.isMicrophoneEnabled,
      isDeafened: false,
    });
    store.setParticipants(map);
  });

  room.on(RoomEvent.ParticipantDisconnected, (participant: RemoteParticipant) => {
    const map = new Map(store.participants());
    map.delete(participant.identity);
    store.setParticipants(map);
  });

  room.on(
    RoomEvent.TrackMuted,
    (publication: TrackPublication, participant: Participant) => {
      if (publication.track?.kind === Track.Kind.Audio) {
        const map = new Map(store.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isMuted: true });
          store.setParticipants(map);
        }
      }
    },
  );

  room.on(
    RoomEvent.TrackUnmuted,
    (publication: TrackPublication, participant: Participant) => {
      if (publication.track?.kind === Track.Kind.Audio) {
        const map = new Map(store.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isMuted: false });
          store.setParticipants(map);
        }
      }
    },
  );

  room.on(
    RoomEvent.TrackSubscribed,
    (track: RemoteTrack, _publication: RemoteTrackPublication, participant: RemoteParticipant) => {
      // Attach audio tracks to the DOM so they play automatically.
      if (track.kind === Track.Kind.Audio) {
        track.attach();
        // If deafened, immediately mute newly subscribed audio.
        if (store.isDeafened()) {
          const audioEl = track.attachedElements[0] as HTMLAudioElement | undefined;
          if (audioEl) {
            audioEl.muted = true;
          }
        }
      }
      const map = new Map(store.participants());
      if (!map.has(participant.identity)) {
        map.set(participant.identity, {
          userId: participant.identity,
          isMuted: !participant.isMicrophoneEnabled,
          isDeafened: false,
        });
        store.setParticipants(map);
      }
    },
  );

  room.on(
    RoomEvent.TrackUnsubscribed,
    (track: RemoteTrack) => {
      if (track.kind === Track.Kind.Audio) {
        track.detach();
      }
    },
  );
}

async function applyDeafenToAllRemoteTracks(room: Room, deafened: boolean): Promise<void> {
  for (const participant of room.remoteParticipants.values()) {
    for (const publication of participant.trackPublications.values()) {
      if (publication.track?.kind === Track.Kind.Audio) {
        const attachedEls = publication.track.attachedElements;
        for (const el of attachedEls) {
          (el as HTMLAudioElement).muted = deafened;
        }
      }
    }
  }
}

export function useVoice() {
  return {
    get currentChannelId() { return store.currentChannelId(); },
    get participants() { return store.participants(); },
    get isMuted() { return store.isMuted(); },
    get isDeafened() { return store.isDeafened(); },
    get isConnecting() { return store.isConnecting(); },
    get error() { return store.error(); },

    /**
     * Called by signalr.store after a connection is established so that voice
     * operations can invoke hub methods without a circular import.
     */
    setSignalRConnection(conn: HubConnection | null): void {
      signalrConnection = conn;
    },

    async joinVoice(channelId: string): Promise<void> {
      const conn = signalrConnection;
      if (!conn) {
        console.error('Cannot join voice: SignalR not connected');
        store.setError('Not connected to server');
        return;
      }

      store.setError(null);
      store.setIsConnecting(true);
      serverSideJoined = false;
      intentionalLeave = false;
      // Show VoicePanel immediately while connecting — optimistic update.
      store.setCurrentChannelId(channelId);
      store.setIsMuted(false);
      store.setIsDeafened(false);

      let serverJoinResult: JoinVoiceResponse | null = null;
      try {
        // Ask the backend to provision our slot and issue a LiveKit token.
        serverJoinResult = await conn.invoke<JoinVoiceResponse>('JoinVoiceChannel', channelId);
        // Server-side join succeeded — mark it so RoomEvent.Disconnected does not
        // clear VoicePanel if LiveKit connection subsequently fails.
        serverSideJoined = true;
      } catch (err) {
        console.error('Failed to join voice channel on server:', err);
        store.setError('Failed to connect to voice channel');
        // Server-side join failed — revert the optimistic update.
        store.setCurrentChannelId(null);
        store.setIsConnecting(false);
        return;
      }

      // Server-side join succeeded — VoicePanel stays visible from this point.
      // Attempt to connect to LiveKit; a LiveKit failure is non-fatal.
      try {
        // Recreate the room when quality config is provided so publish defaults
        // reflect the server-supplied tier limits.
        if (livekitRoom) {
          intentionalLeave = true;
          if (livekitRoom.state !== ConnectionState.Disconnected) {
            await livekitRoom.disconnect();
          }
          livekitRoom = null;
          intentionalLeave = false;
        }

        const room = getRoom(serverJoinResult.qualityConfig);

        await room.connect(serverJoinResult.livekitUrl, serverJoinResult.token);

        // Enable the local microphone after connecting.
        await room.localParticipant.setMicrophoneEnabled(true);

        // Seed participants map with anyone already in the room.
        const map = new Map<string, VoiceParticipant>();
        for (const participant of room.remoteParticipants.values()) {
          map.set(participant.identity, {
            userId: participant.identity,
            isMuted: !participant.isMicrophoneEnabled,
            isDeafened: false,
          });
        }
        store.setParticipants(map);
      } catch (err) {
        // LiveKit is unavailable or media access denied — the server-side VoiceState
        // is still active. Show the VoicePanel without audio rather than hiding it.
        console.warn('LiveKit connection failed, voice panel running without audio:', err);
        store.setIsConnecting(false);
      }
    },

    async leaveVoice(): Promise<void> {
      const channelId = store.currentChannelId();
      serverSideJoined = false;

      // Disconnect from LiveKit first so audio stops immediately.
      if (livekitRoom && livekitRoom.state !== ConnectionState.Disconnected) {
        intentionalLeave = true;
        await livekitRoom.disconnect();
        // intentionalLeave is reset inside the Disconnected handler
      }

      // Notify the backend.
      if (channelId && signalrConnection) {
        try {
          await signalrConnection.invoke('LeaveVoiceChannel', channelId);
        } catch (err) {
          console.error('Failed to notify server of voice leave:', err);
        }
      }

      store.setCurrentChannelId(null);
      store.setParticipants(new Map());
      store.setIsMuted(false);
      store.setIsDeafened(false);
    },

    async toggleMute(): Promise<void> {
      const newMutedState = !store.isMuted();
      store.setIsMuted(newMutedState);

      // If deafened, undeafen when unmuting.
      if (store.isDeafened() && !newMutedState) {
        store.setIsDeafened(false);
        if (livekitRoom) {
          await applyDeafenToAllRemoteTracks(livekitRoom, false);
        }
      }

      // Apply to LiveKit local audio track.
      if (livekitRoom && livekitRoom.state === ConnectionState.Connected) {
        try {
          await livekitRoom.localParticipant.setMicrophoneEnabled(!newMutedState);
        } catch (err) {
          console.error('Failed to toggle microphone in LiveKit:', err);
        }
      }

      // Sync mute state with the backend so other participants see the change.
      const channelId = store.currentChannelId();
      if (channelId && signalrConnection) {
        try {
          await signalrConnection.invoke(
            'UpdateVoiceState',
            channelId,
            newMutedState,
            store.isDeafened(),
            null,
          );
        } catch (err) {
          console.error('Failed to sync mute state with server:', err);
        }
      }
    },

    async toggleDeafen(): Promise<void> {
      const newDeafenedState = !store.isDeafened();
      store.setIsDeafened(newDeafenedState);

      // Deafening always mutes.
      if (newDeafenedState && !store.isMuted()) {
        store.setIsMuted(true);
        if (livekitRoom && livekitRoom.state === ConnectionState.Connected) {
          try {
            await livekitRoom.localParticipant.setMicrophoneEnabled(false);
          } catch (err) {
            console.error('Failed to mute microphone in LiveKit:', err);
          }
        }
      }

      // Mute/unmute all remote audio tracks for deafen effect.
      if (livekitRoom) {
        await applyDeafenToAllRemoteTracks(livekitRoom, newDeafenedState);
      }

      // Sync deafen state with the backend.
      const channelId = store.currentChannelId();
      if (channelId && signalrConnection) {
        try {
          await signalrConnection.invoke(
            'UpdateVoiceState',
            channelId,
            store.isMuted(),
            newDeafenedState,
            null,
          );
        } catch (err) {
          console.error('Failed to sync deafen state with server:', err);
        }
      }
    },

    updateVoiceState(userId: string, channelId: string | null, isMuted: boolean, isDeafened: boolean): void {
      if (channelId === store.currentChannelId()) {
        // User joined or updated in current channel
        const map = new Map(store.participants());
        map.set(userId, { userId, isMuted, isDeafened });
        store.setParticipants(map);
      } else {
        // User left current channel
        const map = new Map(store.participants());
        map.delete(userId);
        store.setParticipants(map);
      }
    },

    clearVoiceState(): void {
      serverSideJoined = false;
      intentionalLeave = true;
      if (livekitRoom && livekitRoom.state !== ConnectionState.Disconnected) {
        livekitRoom.disconnect().catch((err) => {
          console.error('Error disconnecting LiveKit on clearVoiceState:', err);
        });
      } else {
        intentionalLeave = false;
      }
      store.setCurrentChannelId(null);
      store.setParticipants(new Map());
      store.setIsMuted(false);
      store.setIsDeafened(false);
    },
  };
}
