import { onMount, Show, createMemo } from 'solid-js';
import { useBroadcast } from '../stores/broadcast.store';
import { useAuth } from '../stores/auth.store';
import BroadcastHostPanel from './BroadcastHostPanel';
import BroadcastGuestPanel from './BroadcastGuestPanel';
import BroadcastViewer from './BroadcastViewer';
import BroadcastGreenRoom from './BroadcastGreenRoom';
import Flexbox from './ui/Flexbox';
import styles from './BroadcastChannel.module.css';

interface Props {
  channelId: string;
  canManageBroadcasts: boolean;
}

/**
 * Top-level view for channels with the Streaming capability. Dispatches to the
 * host, guest, or viewer sub-view based on the current user's relationship to
 * the active broadcast (if any).
 */
export default function BroadcastChannel(props: Props) {
  const broadcast = useBroadcast();
  const auth = useAuth();

  onMount(() => {
    broadcast.loadActiveBroadcast(props.channelId).catch(() => {
      // Non-fatal; the panel will show the idle state.
    });
  });

  const active = createMemo(() => broadcast.getActiveBroadcast(props.channelId));
  const userRole = createMemo<'host' | 'guest' | 'viewer' | 'none'>(() => {
    const b = active();
    if (!b) return 'none';
    if (b.hostUserId === auth.user?.id) return 'host';
    if (broadcast.guestCredentials?.broadcastId === b.id) return 'guest';
    return 'viewer';
  });

  return (
    <Flexbox direction="vertical" class={styles.container} data-testid="broadcast-channel">
      <Show
        when={active()}
        fallback={
          <Show
            when={props.canManageBroadcasts}
            fallback={
              <Flexbox align="center" justify="center" class={styles.idle} data-testid="broadcast-idle">
                No broadcast is live.
              </Flexbox>
            }
          >
            <BroadcastHostPanel channelId={props.channelId} mode="idle" />
          </Show>
        }
      >
        {(broadcastData) => (
          <>
            <Show when={userRole() === 'host'}>
              <BroadcastHostPanel
                channelId={props.channelId}
                mode="live"
                broadcast={broadcastData()}
              />
            </Show>
            <Show when={userRole() === 'guest'}>
              <BroadcastGuestPanel broadcast={broadcastData()} />
            </Show>
            <Show when={userRole() === 'viewer'}>
              <BroadcastViewer broadcast={broadcastData()} />
            </Show>
            <Show when={props.canManageBroadcasts && userRole() === 'host'}>
              <BroadcastGreenRoom broadcast={broadcastData()} />
            </Show>
          </>
        )}
      </Show>
    </Flexbox>
  );
}
