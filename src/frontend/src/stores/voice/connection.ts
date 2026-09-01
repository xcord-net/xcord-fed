import { Room, RoomEvent, ConnectionState } from 'livekit-client';
import { voiceState, voiceRefs, type QualityConfig, type JoinVoiceResponse } from './state';
import { attachParticipantHandlers, seedParticipantsFromRoom } from './participants';

/**
 * LiveKit connection lifecycle: Room creation, join, leave, disconnect-state handling.
 * Audio control side-effects (mute/deafen/screen share) live in audio-controls.ts.
 */

function buildPublishDefaults(qualityConfig?: QualityConfig): Record<string, unknown> | undefined {
  if (!qualityConfig) return undefined;
  const publishDefaults: Record<string, unknown> = {};

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

  return Object.keys(publishDefaults).length > 0 ? publishDefaults : undefined;
}

/** Get or lazily create the module-level LiveKit Room. */
export function getRoom(qualityConfig?: QualityConfig): Room {
  if (!voiceRefs.room) {
    voiceRefs.room = new Room({
      adaptiveStream: true,
      dynacast: true,
      publishDefaults: buildPublishDefaults(qualityConfig),
    });
    attachRoomEventHandlers(voiceRefs.room);
    attachParticipantHandlers(voiceRefs.room);
  }
  return voiceRefs.room;
}

function attachRoomEventHandlers(room: Room): void {
  room.on(RoomEvent.Disconnected, () => {
    // Only clear voice state if this was an intentional leave or if the server-side
    // join was never established. A failed LiveKit connection (e.g. LiveKit server
    // unreachable) must not hide the VoicePanel when the server-side VoiceState exists.
    if (voiceRefs.intentionalLeave || !voiceRefs.serverSideJoined) {
      voiceState.setCurrentChannelId(null);
      voiceState.setParticipants(new Map());
      voiceState.setIsMuted(false);
      voiceState.setIsDeafened(false);
      voiceState.setIsScreenSharing(false);
      voiceState.setScreenShareParticipantId(null);
      voiceState.setIsSpeaking(false);
    }
    voiceState.setIsConnecting(false);
    voiceRefs.intentionalLeave = false;
  });

  room.on(RoomEvent.ConnectionStateChanged, (state: ConnectionState) => {
    if (state === ConnectionState.Connecting || state === ConnectionState.Reconnecting) {
      voiceState.setIsConnecting(true);
    } else {
      voiceState.setIsConnecting(false);
    }
  });
}

export function setSignalRConnection(conn: import('@microsoft/signalr').HubConnection | null): void {
  voiceRefs.signalrConnection = conn;
}

/** Join a voice channel: ask the backend for a token, then connect to LiveKit. */
export async function joinVoice(channelId: string): Promise<void> {
  const conn = voiceRefs.signalrConnection;
  if (!conn) {
    console.error('Cannot join voice: SignalR not connected');
    voiceState.setError('Not connected to server');
    return;
  }

  voiceState.setError(null);
  voiceState.setIsConnecting(true);
  voiceRefs.serverSideJoined = false;
  voiceRefs.intentionalLeave = false;
  // Show VoicePanel immediately while connecting - optimistic update.
  voiceState.setCurrentChannelId(channelId);
  voiceState.setIsMuted(false);
  voiceState.setIsDeafened(false);

  let serverJoinResult: JoinVoiceResponse | null = null;
  try {
    // Ask the backend to provision our slot and issue a LiveKit token.
    serverJoinResult = await conn.invoke<JoinVoiceResponse>('JoinVoiceChannel', channelId);
    // Server-side join succeeded - mark it so RoomEvent.Disconnected does not
    // clear VoicePanel if LiveKit connection subsequently fails.
    voiceRefs.serverSideJoined = true;
  } catch (err) {
    console.error('Failed to join voice channel on server:', err);
    voiceState.setError('Failed to connect to voice channel');
    // Server-side join failed - revert the optimistic update.
    voiceState.setCurrentChannelId(null);
    voiceState.setIsConnecting(false);
    return;
  }

  // Server-side join succeeded - VoicePanel stays visible from this point.
  // Attempt to connect to LiveKit; a LiveKit failure is non-fatal.
  try {
    // Recreate the room when quality config is provided so publish defaults
    // reflect the server-supplied tier limits.
    if (voiceRefs.room) {
      voiceRefs.intentionalLeave = true;
      if (voiceRefs.room.state !== ConnectionState.Disconnected) {
        await voiceRefs.room.disconnect();
      }
      voiceRefs.room = null;
      voiceRefs.intentionalLeave = false;
    }

    const room = getRoom(serverJoinResult.qualityConfig);

    await room.connect(serverJoinResult.livekitUrl, serverJoinResult.token);

    // Enable the local microphone after connecting.
    await room.localParticipant.setMicrophoneEnabled(true);

    // Seed participants map with anyone already in the room.
    seedParticipantsFromRoom(room);
  } catch (err) {
    // LiveKit is unavailable or media access denied - the server-side VoiceState
    // is still active. Show the VoicePanel without audio rather than hiding it.
    console.warn('LiveKit connection failed, voice panel running without audio:', err);
    voiceState.setIsConnecting(false);
  }
}

/** Leave the current voice channel: disconnect LiveKit + notify backend. */
export async function leaveVoice(): Promise<void> {
  const channelId = voiceState.currentChannelId();
  voiceRefs.serverSideJoined = false;

  // Disconnect from LiveKit first so audio stops immediately.
  if (voiceRefs.room && voiceRefs.room.state !== ConnectionState.Disconnected) {
    voiceRefs.intentionalLeave = true;
    await voiceRefs.room.disconnect();
    // intentionalLeave is reset inside the Disconnected handler
  }

  // Notify the backend.
  if (channelId && voiceRefs.signalrConnection) {
    try {
      await voiceRefs.signalrConnection.invoke('LeaveVoiceChannel', channelId);
    } catch (err) {
      console.error('Failed to notify server of voice leave:', err);
    }
  }

  voiceState.setCurrentChannelId(null);
  voiceState.setParticipants(new Map());
  voiceState.setIsMuted(false);
  voiceState.setIsDeafened(false);
  voiceState.setIsScreenSharing(false);
  voiceState.setScreenShareParticipantId(null);
  voiceState.setIsSpeaking(false);
}

/**
 * Clear all voice state without notifying the server. Used when SignalR
 * reconnect causes the server-side state to be lost.
 */
export function clearVoiceState(): void {
  voiceRefs.serverSideJoined = false;
  voiceRefs.intentionalLeave = true;
  if (voiceRefs.room && voiceRefs.room.state !== ConnectionState.Disconnected) {
    voiceRefs.room.disconnect().catch((err) => {
      console.error('Error disconnecting LiveKit on clearVoiceState:', err);
    });
  } else {
    voiceRefs.intentionalLeave = false;
  }
  voiceState.setCurrentChannelId(null);
  voiceState.setParticipants(new Map());
  voiceState.setIsMuted(false);
  voiceState.setIsDeafened(false);
  voiceState.setIsScreenSharing(false);
  voiceState.setScreenShareParticipantId(null);
  voiceState.setIsSpeaking(false);
}

/** Hard reset for tests / sign-out. */
export function resetVoice(): void {
  clearVoiceState();
  voiceState.setIsConnecting(false);
  voiceState.setError(null);
  voiceRefs.room = null;
  voiceRefs.signalrConnection = null;
  voiceRefs.serverSideJoined = false;
  voiceRefs.intentionalLeave = false;
}
