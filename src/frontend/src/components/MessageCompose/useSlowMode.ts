import { createSignal, createEffect, onCleanup } from 'solid-js';
import { useChannels } from '../../stores/channel.store';

interface UseSlowModeArgs {
  channelId: () => string | undefined;
}

export function useSlowMode(args: UseSlowModeArgs) {
  const channelStore = useChannels();
  const [slowModeCountdown, setSlowModeCountdown] = createSignal(0);
  let slowModeTimer: ReturnType<typeof setInterval> | undefined;

  // Reset slow mode countdown when channel changes
  createEffect(() => {
    const _channelId = args.channelId();
    void _channelId;
    setSlowModeCountdown(0);
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
      slowModeTimer = undefined;
    }
  });

  onCleanup(() => {
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
    }
  });

  const slowModeInterval = () => {
    const id = args.channelId();
    if (!id) return 0;
    const channel = channelStore.channels.find((c) => c.id === id);
    return channel?.slowModeSeconds ?? 0;
  };

  const isSlowModeActive = () => slowModeCountdown() > 0;

  const startSlowModeCountdown = () => {
    const interval = slowModeInterval();
    if (interval <= 0) return;
    setSlowModeCountdown(interval);
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
    }
    slowModeTimer = setInterval(() => {
      setSlowModeCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(slowModeTimer);
          slowModeTimer = undefined;
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  };

  return {
    slowModeCountdown,
    isSlowModeActive,
    startSlowModeCountdown,
  };
}
