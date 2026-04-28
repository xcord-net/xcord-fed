import { For, Show } from 'solid-js';
import EmbedDisplay from '../EmbedDisplay';
import ReactionDisplay from '../ReactionDisplay';
import { useThreads } from '../../stores/thread.store';
import type { Message } from '../../types/message';
import AttachmentList from './AttachmentList';
import PollContainer from './PollContainer';
import styles from './MessageRow.module.css';

interface MessageContentProps {
  message: Message;
  conversationId: string;
  serverId?: string;
  isAuthor: boolean;
}

/** Shared trailer rendered after the message text in both grouped and full layouts:
 *  edited badge, attachments, embeds, poll, reactions, thread indicator. */
export default function MessageContent(props: MessageContentProps) {
  const threadStore = useThreads();

  return (
    <>
      <Show when={props.message.editedAt}>
        <span data-testid="message-edited-indicator" class={styles.editedBadge}>(edited)</span>
      </Show>
      {/* Attachments */}
      <Show when={(props.message.attachments?.length ?? 0) > 0}>
        <AttachmentList attachments={props.message.attachments!} />
      </Show>
      {/* Embeds */}
      <Show when={(props.message.embeds?.length ?? 0) > 0}>
        <For each={props.message.embeds}>
          {(embed) => <EmbedDisplay embed={embed} />}
        </For>
      </Show>
      {/* Poll */}
      <Show when={props.message.type === 'PollCreated' && props.message.pollId}>
        <PollContainer
          pollId={props.message.pollId!}
          isAuthor={props.isAuthor}
        />
      </Show>
      {/* Reactions */}
      <Show when={(props.message.reactions?.length ?? 0) > 0}>
        <ReactionDisplay
          reactions={props.message.reactions!}
          messageId={props.message.id}
          conversationId={props.conversationId}
          serverId={props.serverId}
        />
      </Show>
      {/* Thread indicator */}
      <Show when={threadStore.threads.find((t) => t.parentMessageId === props.message.id)}>
        {(thread) => (
          <button
            data-testid="message-thread-indicator"
            class={styles.threadIndicator}
            onClick={() => threadStore.setActiveThread(thread().id)}
          >
            &#35; {thread().name} &middot; {thread().messageCount}{' '}
            {thread().messageCount === 1 ? 'reply' : 'replies'}
          </button>
        )}
      </Show>
    </>
  );
}
