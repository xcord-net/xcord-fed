import { createSignal, onCleanup } from 'solid-js';
import { Room, RoomEvent, Track, ConnectionState } from 'livekit-client';

export interface ConnectArgs {
  livekitUrl: string;
  publishToken: string;
}

export function useHostRoom() {
  const [isMuted, setIsMuted] = createSignal(false);
  const [isCameraOff, setIsCameraOff] = createSignal(false);

  let previewVideoRef: HTMLVideoElement | undefined;
  let liveRoom: Room | null = null;

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

  const connect = async ({ livekitUrl, publishToken }: ConnectArgs): Promise<void> => {
    liveRoom = new Room({ adaptiveStream: true, dynacast: true });
    await liveRoom.connect(livekitUrl, publishToken);
    await liveRoom.localParticipant.setCameraEnabled(true);
    await liveRoom.localParticipant.setMicrophoneEnabled(true);
    attachLocalPreview(liveRoom);
  };

  const disconnect = async (): Promise<void> => {
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
    if (liveRoom && liveRoom.state !== ConnectionState.Disconnected) {
      liveRoom.disconnect().catch(() => { /* non-fatal */ });
    }
    liveRoom = null;
  });

  return {
    isMuted,
    isCameraOff,
    setPreviewRef,
    connect,
    disconnect,
    toggleMute,
    toggleCamera,
  };
}
