import { Show } from 'solid-js';
import DmList from '../DmList';
import FriendList from '../FriendList';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import TypingIndicator from '../TypingIndicator';
import WelcomePanel from './WelcomePanel';
import Flexbox from '../ui/Flexbox';
import { useServers } from '../../stores/server.store';
import styles from './DmView.module.css';

interface DmViewProps {
  channelId: string | undefined;
  dmConversationId: string | undefined;
}

export default function DmView(props: DmViewProps) {
  const serverStore = useServers();

  // A user who hasn't created or joined any server gets a guidance banner above
  // the friends/DM view (not instead of it - they still need it to start DMs).
  const hasNoServers = () => serverStore.servers.length === 0;

  return (
    <Show
      when={props.channelId && props.dmConversationId}
      fallback={
        <Flexbox direction="vertical" class={styles.channelView}>
          <Show when={hasNoServers()}>
            <WelcomePanel />
          </Show>
          <FriendList />
          <DmList />
        </Flexbox>
      }
    >
      {/* DM conversation view */}
      <Flexbox direction="vertical" class={styles.channelView}>
        <Flexbox direction="vertical" class={styles.messagesArea}>
          <MessageList conversationId={props.dmConversationId!} />
          <TypingIndicator conversationId={props.dmConversationId!} />
          <MessageCompose conversationId={props.dmConversationId!} channelId={props.channelId} />
        </Flexbox>
      </Flexbox>
    </Show>
  );
}
