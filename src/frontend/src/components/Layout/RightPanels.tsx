import { Show } from 'solid-js';
import PinList from '../PinList';
import ScheduledEvents from '../ScheduledEvents';
import ScheduledMessages from '../ScheduledMessages';
import SearchPanel from '../SearchPanel';
import ThreadPanel from '../ThreadPanel';
import { useChannels } from '../../stores/channel.store';
import { useModals } from '../../stores/modal.store';
import { useServers } from '../../stores/server.store';
import styles from './RightPanels.module.css';

interface RightPanelsProps {
  conversationId: string | undefined;
  channelId: string | undefined;
}

export default function RightPanels(props: RightPanelsProps) {
  const modals = useModals();
  const serverStore = useServers();
  const channelStore = useChannels();

  return (
    <>
      <Show when={modals.showSearch}>
        <div class={styles.rightPanel}>
          <SearchPanel />
        </div>
      </Show>
      <Show when={modals.showPins && props.conversationId}>
        <div class={styles.rightPanel}>
          <PinList conversationId={props.conversationId!} />
        </div>
      </Show>
      <Show when={modals.showThreads}>
        <div class={styles.rightPanel}>
          <ThreadPanel channelId={props.channelId || ''} />
        </div>
      </Show>
      <Show when={modals.showEvents && serverStore.selectedServerId}>
        <div class={styles.rightPanel}>
          <ScheduledEvents serverId={serverStore.selectedServerId!} />
        </div>
      </Show>
      <Show when={modals.showScheduledMessages && channelStore.selectedChannelId}>
        <div class={styles.rightPanel}>
          <ScheduledMessages channelId={channelStore.selectedChannelId!} />
        </div>
      </Show>
    </>
  );
}
