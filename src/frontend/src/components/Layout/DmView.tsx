import { Show } from 'solid-js';
import DmList from '../DmList';
import FriendList from '../FriendList';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import TypingIndicator from '../TypingIndicator';
import Flexbox from '../ui/Flexbox';
import styles from './DmView.module.css';

interface DmViewProps {
  channelId: string | undefined;
  dmConversationId: string | undefined;
}

export default function DmView(props: DmViewProps) {
  return (
    <Show
      when={props.channelId && props.dmConversationId}
      fallback={
        <Flexbox direction="vertical" class={styles.channelView}>
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
