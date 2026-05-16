import { createSignal, createRoot } from 'solid-js';
import { Room } from 'livekit-client';
import type { HubConnection } from '@microsoft/signalr';
import type { VoiceParticipant } from '../../types/voice';

/**
 * Reactive voice state shared by every voice module.
 *
 * The voice store is intentionally split into:
 *  - state.ts (this file)        - signals + module-level refs
 *  - connection.ts               - LiveKit Room lifecycle
 *  - participants.ts             - participant Map sync from LiveKit events
 *  - audio-controls.ts           - mute/deafen/screen-share controls
 *
 * `voice.store.ts` re-exports a single `useVoice()` API by composing them.
 */

export interface QualityConfig {
  maxAudioBitrateKbps: number;
  maxVideoBitrateKbps: number;
  maxVideoWidth: number;
  maxVideoHeight: number;
  maxVideoFps: number;
  maxScreenShareBitrateKbps: number;
  enableSimulcast: boolean;
}

export interface JoinVoiceResponse {
  token: string;
  roomName: string;
  livekitUrl: string;
  qualityConfig: QualityConfig;
}

export const voiceState = createRoot(() => {
  const [currentChannelId, setCurrentChannelId] = createSignal<string | null>(null);
  const [participants, setParticipants] = createSignal<Map<string, VoiceParticipant>>(new Map());
  const [isMuted, setIsMuted] = createSignal(false);
  const [isDeafened, setIsDeafened] = createSignal(false);
  const [isConnecting, setIsConnecting] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [isScreenSharing, setIsScreenSharing] = createSignal(false);
  // Identity of the participant currently screen sharing (null if nobody is).
  const [screenShareParticipantId, setScreenShareParticipantId] = createSignal<string | null>(null);

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
    isScreenSharing,
    setIsScreenSharing,
    screenShareParticipantId,
    setScreenShareParticipantId,
  };
});

/**
 * Module-level mutable refs. Held in a single object so submodules can mutate
 * shared state without each maintaining their own copy.
 *
 * - room: the LiveKit Room (one per browser tab; recreated on quality changes)
 * - signalrConnection: injected by signalr.store after connecting; used to invoke hub methods
 * - serverSideJoined: true once the server-side VoiceState exists. While set,
 *   RoomEvent.Disconnected from a failed LiveKit connection must NOT clear
 *   currentChannelId - the user is still considered "in voice" on the server.
 * - intentionalLeave: true when the user explicitly requested to leave (via
 *   leaveVoice or clearVoiceState). RoomEvent.Disconnected uses this to know
 *   whether it is allowed to clear state.
 */
export interface VoiceRefs {
  room: Room | null;
  signalrConnection: HubConnection | null;
  serverSideJoined: boolean;
  intentionalLeave: boolean;
}

export const voiceRefs: VoiceRefs = {
  room: null,
  signalrConnection: null,
  serverSideJoined: false,
  intentionalLeave: false,
};

/**
 * Returns the module-level LiveKit Room instance, or null if not yet created.
 * Used by ScreenShareViewer to attach video tracks to DOM elements.
 */
export function getLivekitRoom(): Room | null {
  return voiceRefs.room;
}
