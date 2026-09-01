import { Show } from 'solid-js';
import type { ReplyTo } from '../../types/message';
import styles from './MessageRow.module.css';

interface ReplyReferenceProps {
  replyTo: ReplyTo;
  onJumpTo: () => void;
}

/** The quote line above a reply: who was replied to, what they said, and a way back
 *  to it. A deleted parent renders as inert text - there is nothing to jump to. */
export default function ReplyReference(props: ReplyReferenceProps) {
  return (
    <Show
      when={!props.replyTo.isDeleted}
      fallback={
        <div
          class={`${styles.replyIndicator} ${styles.replyIndicatorInert}`}
          data-testid="reply-reference-deleted"
        >
          <span class={styles.replyDeleted}>Original message deleted</span>
        </div>
      }
    >
      <button
        type="button"
        class={styles.replyIndicator}
        data-testid="reply-reference"
        aria-label={`Jump to the message from ${props.replyTo.authorUsername || 'Unknown User'}`}
        onClick={() => props.onJumpTo()}
      >
        <span
          class={styles.replyAuthor}
          style={{ color: props.replyTo.authorGroupColor ?? undefined }}
        >
          {props.replyTo.authorUsername || 'Unknown User'}
        </span>
        <span class={styles.replyPreview}>{props.replyTo.preview}</span>
      </button>
    </Show>
  );
}
