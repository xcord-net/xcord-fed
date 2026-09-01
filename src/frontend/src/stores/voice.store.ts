/**
 * Voice store entry point. The implementation is split across the `voice/`
 * folder by responsibility:
 *
 *  - voice/state.ts             - SolidJS signals + module-level refs
 *  - voice/connection.ts        - LiveKit Room create/connect/disconnect
 *  - voice/participants.ts      - LiveKit event -> participants Map sync
 *  - voice/audio-controls.ts    - mute / deafen / screen-share controls
 *
 * This file composes those modules into the public `useVoice()` API used by
 * components and other stores. Keeping `voice.store.ts` as the import path
 * means consumers (signalr.store, VoicePanel, ScreenShareViewer, etc.) do
 * not have to change when internals are reshuffled.
 */

import type { HubConnection } from '@microsoft/signalr';
import { voiceState } from './voice/state';
import {
  joinVoice,
  leaveVoice,
  clearVoiceState,
  resetVoice,
  setSignalRConnection,
} from './voice/connection';
import { applyServerVoiceStateUpdate } from './voice/participants';
import {
  toggleMute,
  toggleDeafen,
  toggleScreenShare,
  stopScreenShare,
} from './voice/audio-controls';

export { getLivekitRoom } from './voice/state';

export function useVoice() {
  return {
    get currentChannelId() { return voiceState.currentChannelId(); },
    get participants() { return voiceState.participants(); },
    get isMuted() { return voiceState.isMuted(); },
    get isDeafened() { return voiceState.isDeafened(); },
    get isConnecting() { return voiceState.isConnecting(); },
    get error() { return voiceState.error(); },
    get isScreenSharing() { return voiceState.isScreenSharing(); },
    get screenShareParticipantId() { return voiceState.screenShareParticipantId(); },
    /** Whether the local user is speaking. Remote speakers carry `isSpeaking`
     *  on their entry in `participants`. */
    get isSpeaking() { return voiceState.isSpeaking(); },

    /**
     * Called by signalr.store after a connection is established so that voice
     * operations can invoke hub methods without a circular import.
     */
    setSignalRConnection(conn: HubConnection | null): void {
      setSignalRConnection(conn);
    },

    joinVoice,
    leaveVoice,
    toggleMute,
    toggleDeafen,
    toggleScreenShare,
    stopScreenShare,

    updateVoiceState(userId: string, channelId: string | null, isMuted: boolean, isDeafened: boolean): void {
      applyServerVoiceStateUpdate(userId, channelId, isMuted, isDeafened);
    },

    clearVoiceState,

    reset(): void {
      resetVoice();
    },
  };
}
