import { Show } from 'solid-js';
import DmList from '../DmList';
import FriendList from '../FriendList';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import TypingIndicator from '../TypingIndicator';
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
        <div class={styles.channelView}>
          <FriendList />
          <DmList />
        </div>
      }
    >
      {/* DM conversation view */}
      <div class={styles.channelView}>
        <div class={styles.messagesArea}>
          <MessageList conversationId={props.dmConversationId!} />
          <TypingIndicator conversationId={props.dmConversationId!} />
          <MessageCompose conversationId={props.dmConversationId!} channelId={props.channelId} />
        </div>
      </div>
    </Show>
  );
}
