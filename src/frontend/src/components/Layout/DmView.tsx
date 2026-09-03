import { Show } from 'solid-js';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import TypingIndicator from '../TypingIndicator';
import Flexbox from '../ui/Flexbox';
import styles from './DmView.module.css';

interface DmViewProps {
  channelId: string | undefined;
  dmConversationId: string | undefined;
}

/**
 * One direct-message conversation.
 *
 * It used to double as the DM home, falling back to a friends-and-DMs panel
 * whenever no conversation was selected. Under the Deck that fallback was
 * unreachable in one direction and stale in the other: `/channels/me` shows
 * Home, so the panel only appeared when you arrived carrying a leftover active
 * tab. The friends list and the DM list moved to the People tab in account
 * settings; the welcome panel moved onto Home.
 */
export default function DmView(props: DmViewProps) {
  return (
    <Show when={props.channelId && props.dmConversationId}>
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
