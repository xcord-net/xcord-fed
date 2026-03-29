import { Show, onCleanup, createEffect } from 'solid-js';
import { Track } from 'livekit-client';
import { useVoice, getLivekitRoom } from '../stores/voice.store';
import styles from './ScreenShareViewer.module.css';

/**
 * Renders the screen share video when a participant in the current voice
 * channel is sharing their screen. The component gets the LiveKit screen share
 * track from the room and attaches it to a <video> element.
 *
 * This is intentionally kept out of the voice store -- the store manages state
 * while this component owns the DOM attachment lifecycle.
 */
export default function ScreenShareViewer() {
  const voice = useVoice();

  let videoRef!: HTMLVideoElement;

  // Re-attach whenever the screen share participant changes.
  createEffect(() => {
    const participantId = voice.screenShareParticipantId;
    if (!participantId || !videoRef) return;

    const room = getLivekitRoom();
    if (!room) return;

    // Find the screen share track -- it could be from a remote participant or the local one.
    let screenTrack: Track | undefined;

    if (room.localParticipant.identity === participantId) {
      for (const pub of room.localParticipant.trackPublications.values()) {
        if (pub.source === Track.Source.ScreenShare && pub.track) {
          screenTrack = pub.track;
          break;
        }
      }
    } else {
      const remote = room.remoteParticipants.get(participantId);
      if (remote) {
        for (const pub of remote.trackPublications.values()) {
          if (pub.source === Track.Source.ScreenShare && pub.track) {
            screenTrack = pub.track;
            break;
          }
        }
      }
    }

    if (screenTrack) {
      screenTrack.attach(videoRef);
    }

    onCleanup(() => {
      if (screenTrack) {
        screenTrack.detach(videoRef);
      }
    });
  });

  return (
    <Show when={voice.screenShareParticipantId}>
      <div class={styles.container} data-testid="screen-share-viewer">
        <div class={styles.videoWrapper}>
          <video
            ref={videoRef!}
            autoplay
            playsinline
            class={styles.video}
          />
          <div class={styles.badge}>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.badgeIcon} aria-hidden="true">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
            {voice.isScreenSharing ? 'You are sharing' : 'Screen Share'}
          </div>
          <Show when={voice.isScreenSharing}>
            <button
              class={styles.stopButton}
              onClick={() => voice.stopScreenShare()}
            >
              Stop Sharing
            </button>
          </Show>
        </div>
      </div>
    </Show>
  );
}
