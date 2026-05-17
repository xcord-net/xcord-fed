import { Show, createMemo, createSignal, onCleanup } from 'solid-js';
import { Room, RoomEvent, Track, ConnectionState } from 'livekit-client';
import {
  useBroadcast,
  type Broadcast,
} from '../stores/broadcast.store';
import { useAuth } from '../stores/auth.store';
import Flexbox from './ui/Flexbox';
import styles from './BroadcastGuestPanel.module.css';

interface Props {
  broadcast: Broadcast;
}

export default function BroadcastGuestPanel(props: Props) {
  const broadcast = useBroadcast();
  const auth = useAuth();

  const [connecting, setConnecting] = createSignal(false);
  const [connected, setConnected] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [isMuted, setIsMuted] = createSignal(false);
  const [isCameraOff, setIsCameraOff] = createSignal(false);

  let previewVideoRef: HTMLVideoElement | undefined;
  let guestRoom: Room | null = null;

  const onStage = createMemo(() => {
    const uid = auth.user?.id;
    if (!uid) return false;
    return props.broadcast.stageSlots.some(s => s.userId === uid);
  });

  onCleanup(() => {
    if (guestRoom && guestRoom.state !== ConnectionState.Disconnected) {
      guestRoom.disconnect().catch(() => { /* non-fatal */ });
    }
    guestRoom = null;
  });

  const attachLocalPreview = (room: Room) => {
    if (!previewVideoRef) return;
    const tryAttach = () => {
      const pub = room.localParticipant
        .getTrackPublications()
        .find(p => p.source === Track.Source.Camera);
      const track = pub?.track;
      if (track && previewVideoRef) {
        track.attach(previewVideoRef);
        return true;
      }
      return false;
    };
    if (!tryAttach()) {
      room.on(RoomEvent.LocalTrackPublished, () => tryAttach());
    }
  };

  const handleJoin = async () => {
    setError(null);
    setConnecting(true);
    try {
      const res = await broadcast.joinAsGuest(props.broadcast.id);
      try {
        guestRoom = new Room({ adaptiveStream: true, dynacast: true });
        await guestRoom.connect(res.livekitUrl, res.token);
        await guestRoom.localParticipant.setCameraEnabled(true);
        await guestRoom.localParticipant.setMicrophoneEnabled(true);
        attachLocalPreview(guestRoom);
        setConnected(true);
      } catch (err) {
        console.warn('Guest LiveKit connection failed:', err);
        setError('Unable to publish camera/microphone.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to join as guest.');
    } finally {
      setConnecting(false);
    }
  };

  const handleLeave = async () => {
    if (guestRoom && guestRoom.state !== ConnectionState.Disconnected) {
      try {
        await guestRoom.disconnect();
      } catch {
        // non-fatal
      }
    }
    guestRoom = null;
    broadcast.clearGuestCredentials();
    setConnected(false);
    setIsMuted(false);
    setIsCameraOff(false);
  };

  const toggleMute = async () => {
    if (!guestRoom) return;
    const next = !isMuted();
    try {
      await guestRoom.localParticipant.setMicrophoneEnabled(!next);
      setIsMuted(next);
    } catch (err) {
      console.warn('Failed to toggle microphone:', err);
    }
  };

  const toggleCamera = async () => {
    if (!guestRoom) return;
    const next = !isCameraOff();
    try {
      await guestRoom.localParticipant.setCameraEnabled(!next);
      setIsCameraOff(next);
    } catch (err) {
      console.warn('Failed to toggle camera:', err);
    }
  };

  return (
    <Flexbox direction="vertical" gap={1} class={styles.panel} data-testid="broadcast-guest-panel">
      <Flexbox align="center" justify="between" gap={0.75} class={styles.header}>
        <h2 class={styles.title}>Guest Booth</h2>
        <Show when={connected()}>
          <span class={`${styles.stageIndicator} ${onStage() ? styles.stageIndicatorOn : styles.stageIndicatorOff}`}>
            {onStage() ? 'On Stage' : 'Off Stage'}
          </span>
        </Show>
      </Flexbox>

      <div class={styles.previewWrap}>
        <video
          ref={previewVideoRef}
          autoplay
          muted
          playsinline
          class={styles.previewVideo}
        />
        <Show when={!connected()}>
          <Flexbox align="center" justify="center" class={styles.previewOverlay}>
            <p class={styles.previewHint}>
              Join as a guest to publish your camera and mic.
            </p>
          </Flexbox>
        </Show>
      </div>

      <Show when={error()}>
        <div role="alert" class={styles.errorMsg}>{error()}</div>
      </Show>

      <Flexbox gap={0.5} justify="center" class={styles.actions}>
        <Show
          when={connected()}
          fallback={
            <button
              type="button"
              class={styles.joinButton}
              onClick={handleJoin}
              disabled={connecting()}
              data-testid="broadcast-guest-join-button"
            >
              {connecting() ? 'Joining...' : 'Join as Guest'}
            </button>
          }
        >
          <button
            type="button"
            class={`${styles.controlButton} ${isMuted() ? styles.controlButtonActive : ''}`}
            onClick={toggleMute}
            data-testid="broadcast-guest-mute-button"
          >
            {isMuted() ? 'Unmute' : 'Mute'}
          </button>
          <button
            type="button"
            class={`${styles.controlButton} ${isCameraOff() ? styles.controlButtonActive : ''}`}
            onClick={toggleCamera}
            data-testid="broadcast-guest-camera-button"
          >
            {isCameraOff() ? 'Camera On' : 'Camera Off'}
          </button>
          <button
            type="button"
            class={styles.leaveButton}
            onClick={handleLeave}
            data-testid="broadcast-guest-leave-button"
          >
            Leave
          </button>
        </Show>
      </Flexbox>
    </Flexbox>
  );
}
