import { createSignal, onCleanup } from 'solid-js';
import { Room, RoomEvent, Track, ConnectionState } from 'livekit-client';
import { useSignalR } from '../../stores/signalr.store';

export interface ConnectArgs {
  livekitUrl: string;
  publishToken: string;
  /**
   * The broadcast being hosted. When present the publish token is renewed before
   * it lapses; without it a broadcast simply stops after the token's lifetime.
   */
  broadcastId?: string;
}

/**
 * Publish tokens last 30 minutes. Renew at 25 so a slow round trip, or one
 * retry, still lands inside the window.
 */
const TOKEN_REFRESH_MS = 25 * 60 * 1000;

export function useHostRoom() {
  const [isMuted, setIsMuted] = createSignal(false);
  const [isCameraOff, setIsCameraOff] = createSignal(false);

  const signalr = useSignalR();

  // The freshest publish token for the running broadcast. Read by reconnects.
  const [publishToken, setPublishToken] = createSignal<string | null>(null);

  let previewVideoRef: HTMLVideoElement | undefined;
  let liveRoom: Room | null = null;
  let refreshTimer: ReturnType<typeof setInterval> | undefined;

  const stopTokenRefresh = () => {
    if (refreshTimer !== undefined) {
      clearInterval(refreshTimer);
      refreshTimer = undefined;
    }
  };

  /**
   * Keep a valid publish token on hand for as long as the broadcast runs.
   *
   * An established LiveKit session is not killed when its token expires - the
   * token is checked when joining. What breaks is the next join: after a
   * network blip an hour into a broadcast, reconnecting with the original
   * 30-minute token is refused and the host drops off their own stream. So the
   * newest token is kept here and used by any reconnect.
   */
  const startTokenRefresh = (broadcastId: string) => {
    stopTokenRefresh();
    refreshTimer = setInterval(() => {
      const connection = signalr.connection;
      if (!connection) return;
      connection
        .invoke<{ token: string }>('RefreshBroadcastToken', broadcastId)
        .then((result) => {
          if (result?.token) setPublishToken(result.token);
        })
        .catch((err: unknown) => {
          // Not fatal on its own: the current token is still good for another
          // few minutes and the next tick tries again.
          console.warn('Failed to refresh the broadcast publish token:', err);
        });
    }, TOKEN_REFRESH_MS);
  };

  const setPreviewRef = (el: HTMLVideoElement | undefined) => {
    previewVideoRef = el;
  };

  const attachLocalPreview = (room: Room) => {
    if (!previewVideoRef) return;
    // Attach any already-published camera track.
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
      room.on(RoomEvent.LocalTrackPublished, () => {
        tryAttach();
      });
    }
  };

  const connect = async (args: ConnectArgs): Promise<void> => {
    liveRoom = new Room({ adaptiveStream: true, dynacast: true });
    await liveRoom.connect(args.livekitUrl, args.publishToken);
    await liveRoom.localParticipant.setCameraEnabled(true);
    await liveRoom.localParticipant.setMicrophoneEnabled(true);
    attachLocalPreview(liveRoom);
    setPublishToken(args.publishToken);
    if (args.broadcastId) startTokenRefresh(args.broadcastId);
  };

  const disconnect = async (): Promise<void> => {
    stopTokenRefresh();
    if (liveRoom && liveRoom.state !== ConnectionState.Disconnected) {
      await liveRoom.disconnect();
    }
    liveRoom = null;
  };

  const toggleMute = async () => {
    if (!liveRoom) return;
    const next = !isMuted();
    try {
      await liveRoom.localParticipant.setMicrophoneEnabled(!next);
      setIsMuted(next);
    } catch (err) {
      console.warn('Failed to toggle microphone:', err);
    }
  };

  const toggleCamera = async () => {
    if (!liveRoom) return;
    const next = !isCameraOff();
    try {
      await liveRoom.localParticipant.setCameraEnabled(!next);
      setIsCameraOff(next);
    } catch (err) {
      console.warn('Failed to toggle camera:', err);
    }
  };

  onCleanup(() => {
    stopTokenRefresh();
    if (liveRoom && liveRoom.state !== ConnectionState.Disconnected) {
      liveRoom.disconnect().catch(() => { /* non-fatal */ });
    }
    liveRoom = null;
  });

  return {
    isMuted,
    isCameraOff,
    /** Current publish token, renewed for the life of the broadcast. */
    publishToken,
    setPreviewRef,
    connect,
    disconnect,
    toggleMute,
    toggleCamera,
  };
}
