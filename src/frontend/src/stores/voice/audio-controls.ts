import { ConnectionState } from 'livekit-client';
import { voiceState, voiceRefs } from './state';
import { applyDeafenToAllRemoteTracks } from './participants';

/**
 * User-facing audio controls: mute, deafen, screen share.
 * All controls update local state, push to LiveKit, and sync with the backend.
 */

export async function toggleMute(): Promise<void> {
  const newMutedState = !voiceState.isMuted();
  voiceState.setIsMuted(newMutedState);

  // If deafened, undeafen when unmuting.
  if (voiceState.isDeafened() && !newMutedState) {
    voiceState.setIsDeafened(false);
    if (voiceRefs.room) {
      await applyDeafenToAllRemoteTracks(voiceRefs.room, false);
    }
  }

  // Apply to LiveKit local audio track.
  if (voiceRefs.room && voiceRefs.room.state === ConnectionState.Connected) {
    try {
      await voiceRefs.room.localParticipant.setMicrophoneEnabled(!newMutedState);
    } catch (err) {
      console.error('Failed to toggle microphone in LiveKit:', err);
    }
  }

  // Sync mute state with the backend so other participants see the change.
  const channelId = voiceState.currentChannelId();
  if (channelId && voiceRefs.signalrConnection) {
    try {
      await voiceRefs.signalrConnection.invoke(
        'UpdateVoiceState',
        channelId,
        newMutedState,
        voiceState.isDeafened(),
        null,
      );
    } catch (err) {
      console.error('Failed to sync mute state with server:', err);
    }
  }
}

export async function toggleDeafen(): Promise<void> {
  const newDeafenedState = !voiceState.isDeafened();
  voiceState.setIsDeafened(newDeafenedState);

  // Deafening always mutes.
  if (newDeafenedState && !voiceState.isMuted()) {
    voiceState.setIsMuted(true);
    if (voiceRefs.room && voiceRefs.room.state === ConnectionState.Connected) {
      try {
        await voiceRefs.room.localParticipant.setMicrophoneEnabled(false);
      } catch (err) {
        console.error('Failed to mute microphone in LiveKit:', err);
      }
    }
  }

  // Mute/unmute all remote audio tracks for deafen effect.
  if (voiceRefs.room) {
    await applyDeafenToAllRemoteTracks(voiceRefs.room, newDeafenedState);
  }

  // Sync deafen state with the backend.
  const channelId = voiceState.currentChannelId();
  if (channelId && voiceRefs.signalrConnection) {
    try {
      await voiceRefs.signalrConnection.invoke(
        'UpdateVoiceState',
        channelId,
        voiceState.isMuted(),
        newDeafenedState,
        null,
      );
    } catch (err) {
      console.error('Failed to sync deafen state with server:', err);
    }
  }
}

export async function toggleScreenShare(): Promise<void> {
  if (voiceState.isScreenSharing()) {
    await stopScreenShare();
    return;
  }

  if (!voiceRefs.room || voiceRefs.room.state !== ConnectionState.Connected) {
    console.error('Cannot screen share: not connected to LiveKit');
    return;
  }

  try {
    await voiceRefs.room.localParticipant.setScreenShareEnabled(true);
    voiceState.setIsScreenSharing(true);
    voiceState.setScreenShareParticipantId(voiceRefs.room.localParticipant.identity);
  } catch (err) {
    // User denied the browser prompt or an error occurred - not fatal.
    console.warn('Screen share failed:', err);
    voiceState.setIsScreenSharing(false);
  }
}

export async function stopScreenShare(): Promise<void> {
  if (!voiceState.isScreenSharing()) return;

  if (voiceRefs.room && voiceRefs.room.state === ConnectionState.Connected) {
    try {
      await voiceRefs.room.localParticipant.setScreenShareEnabled(false);
    } catch (err) {
      console.error('Failed to stop screen share in LiveKit:', err);
    }
  }

  voiceState.setIsScreenSharing(false);
  // Only clear the screen share participant if it was us.
  if (voiceRefs.room && voiceState.screenShareParticipantId() === voiceRefs.room.localParticipant.identity) {
    voiceState.setScreenShareParticipantId(null);
  }
}
