import { Show } from 'solid-js';
import EmojiPicker from '../EmojiPicker';
import type { Message } from '../../types/message';
import styles from './MessageRow.module.css';

interface MessageActionBarProps {
  message: Message;
  isReactionPickerOpen: boolean;
  canEdit: boolean;
  canDelete: boolean;
  showThreadButton: boolean;
  serverId?: string;
  onReply?: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onToggleReactionPicker: () => void;
  onCloseReactionPicker: () => void;
  onSelectReaction: (emoji: string) => void;
  onTogglePin: () => void;
  onStartThread: () => void;
}

/** Hover action bar shown on a message row: reply, edit, delete, react, pin, thread. */
export default function MessageActionBar(props: MessageActionBarProps) {
  return (
    <div
      role="toolbar"
      aria-label="Message actions"
      classList={{
        [styles.actionBar]: true,
        [styles.actionBarVisible]: props.isReactionPickerOpen,
      }}
    >
      <button
        data-testid="message-action-reply"
        title="Reply"
        aria-label="Reply"
        class={styles.actionButton}
      >
        &#8617;
      </button>
      <Show when={props.canEdit}>
        <button
          data-testid="message-action-edit"
          title="Edit"
          aria-label="Edit"
          class={styles.actionButton}
          onClick={() => props.onEdit()}
        >
          &#9999;&#65039;
        </button>
      </Show>
      <Show when={props.canDelete}>
        <button
          data-testid="message-action-delete"
          title="Delete"
          aria-label="Delete"
          class={`${styles.actionButton} ${styles.actionButtonDelete}`}
          onClick={() => props.onDelete()}
        >
          &#128465;
        </button>
      </Show>
      <div class={styles.reactionButtonWrap}>
        <button
          data-testid="message-action-react"
          title="Add reaction"
          aria-label="Add reaction"
          class={styles.actionButton}
          onClick={() => props.onToggleReactionPicker()}
        >
          &#128578;
        </button>
        <Show when={props.isReactionPickerOpen}>
          <div
            class={styles.reactionPickerOverlay}
            aria-hidden="true"
            onClick={() => props.onCloseReactionPicker()}
          />
          <div class={styles.reactionPickerPopover}>
            <EmojiPicker
              serverId={props.serverId}
              onSelect={(emoji) => props.onSelectReaction(emoji)}
              onClose={() => props.onCloseReactionPicker()}
            />
          </div>
        </Show>
      </div>
      <button
        data-testid="message-action-pin"
        title="Pin"
        aria-label="Pin message"
        class={`${styles.actionButton} ${styles.actionButtonPin}`}
        onClick={() => props.onTogglePin()}
      >
        &#128204;
      </button>
      <Show when={props.showThreadButton}>
        <button
          data-testid="message-action-thread"
          title="Create Thread"
          aria-label="Start thread"
          class={styles.actionButton}
          onClick={() => props.onStartThread()}
        >
          &#35;&#43;
        </button>
      </Show>
    </div>
  );
}
