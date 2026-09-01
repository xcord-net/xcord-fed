import {
  Room,
  RoomEvent,
  Track,
  TrackPublication,
  LocalTrackPublication,
  RemoteTrack,
  RemoteTrackPublication,
  RemoteParticipant,
  Participant,
} from 'livekit-client';
import type { VoiceParticipant } from '../../types/voice';
import { voiceState, voiceRefs } from './state';

/**
 * Subscribes a LiveKit Room to participant/track events and keeps the
 * participants signal Map in sync. Also attaches/detaches audio tracks to
 * the DOM so they play automatically.
 *
 * This is called once per Room instance from connection.ts.
 */
export function attachParticipantHandlers(room: Room): void {
  // Keep participants map in sync with LiveKit room state.
  room.on(RoomEvent.ParticipantConnected, (participant: RemoteParticipant) => {
    const map = new Map(voiceState.participants());
    map.set(participant.identity, {
      userId: participant.identity,
      isMuted: !participant.isMicrophoneEnabled,
      isDeafened: false,
    });
    voiceState.setParticipants(map);
  });

  room.on(RoomEvent.ParticipantDisconnected, (participant: RemoteParticipant) => {
    const map = new Map(voiceState.participants());
    map.delete(participant.identity);
    voiceState.setParticipants(map);
  });

  room.on(
    RoomEvent.TrackMuted,
    (publication: TrackPublication, participant: Participant) => {
      if (publication.track?.kind === Track.Kind.Audio) {
        const map = new Map(voiceState.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isMuted: true });
          voiceState.setParticipants(map);
        }
      }
    },
  );

  room.on(
    RoomEvent.TrackUnmuted,
    (publication: TrackPublication, participant: Participant) => {
      if (publication.track?.kind === Track.Kind.Audio) {
        const map = new Map(voiceState.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isMuted: false });
          voiceState.setParticipants(map);
        }
      }
    },
  );

  // LiveKit reports the full set of active speakers on every change, so the
  // handler sets the flag on everyone in that set and clears it on everyone
  // else. The local participant is not in the participants map, so its
  // speaking state lands on its own signal.
  room.on(RoomEvent.ActiveSpeakersChanged, (speakers: Participant[]) => {
    const speaking = new Set(speakers.map((s) => s.identity));
    voiceState.setIsSpeaking(speaking.has(room.localParticipant.identity));

    const map = new Map(voiceState.participants());
    let changed = false;
    for (const [identity, participant] of map) {
      const next = speaking.has(identity);
      if (participant.isSpeaking !== next) {
        map.set(identity, { ...participant, isSpeaking: next });
        changed = true;
      }
    }
    if (changed) {
      voiceState.setParticipants(map);
    }
  });

  room.on(
    RoomEvent.TrackSubscribed,
    (track: RemoteTrack, publication: RemoteTrackPublication, participant: RemoteParticipant) => {
      // Attach audio tracks to the DOM so they play automatically.
      if (track.kind === Track.Kind.Audio) {
        track.attach();
        // If deafened, immediately mute newly subscribed audio.
        if (voiceState.isDeafened()) {
          const audioEl = track.attachedElements[0] as HTMLAudioElement | undefined;
          if (audioEl) {
            audioEl.muted = true;
          }
        }
      }

      // Track screen share publications from remote participants.
      if (track.kind === Track.Kind.Video && publication.source === Track.Source.ScreenShare) {
        voiceState.setScreenShareParticipantId(participant.identity);
        const map = new Map(voiceState.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isScreenSharing: true });
        } else {
          map.set(participant.identity, {
            userId: participant.identity,
            isMuted: !participant.isMicrophoneEnabled,
            isDeafened: false,
            isScreenSharing: true,
          });
        }
        voiceState.setParticipants(map);
        return;
      }

      const map = new Map(voiceState.participants());
      if (!map.has(participant.identity)) {
        map.set(participant.identity, {
          userId: participant.identity,
          isMuted: !participant.isMicrophoneEnabled,
          isDeafened: false,
        });
        voiceState.setParticipants(map);
      }
    },
  );

  room.on(
    RoomEvent.TrackUnsubscribed,
    (track: RemoteTrack, publication: RemoteTrackPublication, participant: RemoteParticipant) => {
      if (track.kind === Track.Kind.Audio) {
        track.detach();
      }

      // Clear screen share state when the track is removed.
      if (track.kind === Track.Kind.Video && publication.source === Track.Source.ScreenShare) {
        if (voiceState.screenShareParticipantId() === participant.identity) {
          voiceState.setScreenShareParticipantId(null);
        }
        const map = new Map(voiceState.participants());
        const existing = map.get(participant.identity);
        if (existing) {
          map.set(participant.identity, { ...existing, isScreenSharing: false });
          voiceState.setParticipants(map);
        }
      }
    },
  );

  // Detect when the local user stops screen sharing via the browser's
  // native "Stop sharing" button (which unpublishes the track automatically).
  room.on(
    RoomEvent.LocalTrackUnpublished,
    (publication: LocalTrackPublication) => {
      if (publication.source === Track.Source.ScreenShare) {
        voiceState.setIsScreenSharing(false);
        if (voiceState.screenShareParticipantId() === room.localParticipant.identity) {
          voiceState.setScreenShareParticipantId(null);
        }
      }
    },
  );
}

/**
 * Seeds the participants map from a freshly-connected Room. Called by
 * connection.ts after the LiveKit connection is established.
 */
export function seedParticipantsFromRoom(room: Room): void {
  const map = new Map<string, VoiceParticipant>();
  for (const participant of room.remoteParticipants.values()) {
    const hasScreenShare = participant.isScreenShareEnabled;
    map.set(participant.identity, {
      userId: participant.identity,
      isMuted: !participant.isMicrophoneEnabled,
      isDeafened: false,
      isScreenSharing: hasScreenShare,
    });
    if (hasScreenShare) {
      voiceState.setScreenShareParticipantId(participant.identity);
    }
  }
  voiceState.setParticipants(map);
}

/**
 * Called from SignalR when a server-side voice state event arrives.
 * Mirrors the server's view into the local participants map.
 */
export function applyServerVoiceStateUpdate(
  userId: string,
  channelId: string | null,
  isMuted: boolean,
  isDeafened: boolean,
): void {
  if (channelId === voiceState.currentChannelId()) {
    // User joined or updated in current channel
    const map = new Map(voiceState.participants());
    map.set(userId, { userId, isMuted, isDeafened });
    voiceState.setParticipants(map);
  } else {
    // User left current channel
    const map = new Map(voiceState.participants());
    map.delete(userId);
    voiceState.setParticipants(map);
  }
}

/** Mute/unmute every attached remote audio element. Used by deafen toggles. */
export async function applyDeafenToAllRemoteTracks(room: Room, deafened: boolean): Promise<void> {
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

// Re-export voiceRefs so connection.ts can mutate intentionalLeave/serverSideJoined
// without participants.ts having to import them separately.
export { voiceRefs };
