import { onMount, onCleanup, createSignal, Show } from 'solid-js';
import Hls from 'hls.js';
import type { Broadcast } from '../stores/broadcast.store';
import Flexbox from './ui/Flexbox';
import styles from './BroadcastViewer.module.css';

interface Props {
  broadcast: Broadcast;
}

/**
 * HLS playback for non-host, non-guest viewers. Handles segment-not-yet-available
 * errors at broadcast start by retrying the source load after a short delay.
 */
export default function BroadcastViewer(props: Props) {
  let videoRef: HTMLVideoElement | undefined;
  const [error, setError] = createSignal<string | null>(null);
  let hls: Hls | undefined;
  let retryTimer: ReturnType<typeof setTimeout> | undefined;

  onMount(() => {
    if (!videoRef) return;

    if (Hls.isSupported()) {
      hls = new Hls({ liveDurationInfinity: true });
      hls.loadSource(props.broadcast.hlsUrl);
      hls.attachMedia(videoRef);
      hls.on(Hls.Events.MANIFEST_PARSED, () => {
        setError(null);
      });
      hls.on(Hls.Events.ERROR, (_event, data) => {
        if (data.fatal) {
          setError('Waiting for stream...');
          retryTimer = setTimeout(() => {
            if (hls) {
              try {
                hls.loadSource(props.broadcast.hlsUrl);
                hls.startLoad();
              } catch {
                // hls was destroyed - cleanup ran
              }
            }
          }, 3000);
        }
      });
    } else if (videoRef.canPlayType('application/vnd.apple.mpegurl')) {
      // Safari native HLS support.
      videoRef.src = props.broadcast.hlsUrl;
    } else {
      setError('HLS playback is not supported in this browser.');
    }
  });

  onCleanup(() => {
    if (retryTimer) clearTimeout(retryTimer);
    if (hls) {
      hls.destroy();
      hls = undefined;
    }
  });

  return (
    <Flexbox align="center" justify="center" class={styles.container} data-testid="broadcast-viewer">
      <div class={styles.liveBadge}>LIVE</div>
      <video
        ref={videoRef}
        autoplay
        muted
        playsinline
        controls
        class={styles.video}
      />
      <Show when={error()}>
        <div class={styles.error}>{error()}</div>
      </Show>
    </Flexbox>
  );
}
