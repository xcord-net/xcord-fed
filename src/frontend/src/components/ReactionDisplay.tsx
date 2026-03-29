import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';
import { useMessages } from '../stores/message.store';
import { useAuth } from '../stores/auth.store';
import EmojiPicker from './EmojiPicker';
import type { MessageReaction } from '../types/message';
import styles from './ReactionDisplay.module.css';

interface ReactionDisplayProps {
  reactions: MessageReaction[];
  messageId: string;
  conversationId: string;
  serverId?: string;
}

export default function ReactionDisplay(props: ReactionDisplayProps) {
  const authStore = useAuth();
  const messageStore = useMessages();
  const [showPicker, setShowPicker] = createSignal(false);

  const currentUserId = () => authStore.user?.id ?? '';

  const hasUserReacted = (reaction: MessageReaction) => {
    return reaction.userIds.includes(currentUserId());
  };

  const addReaction = async (emoji: string) => {
    try {
      await api.put(
        `/api/v1/conversations/${props.conversationId}/messages/${props.messageId}/reactions/${encodeURIComponent(emoji)}`
      );
    } catch (e) {
      console.error('Failed to add reaction', e);
    }
  };

  const removeReaction = async (emoji: string) => {
    try {
      await api.delete(
        `/api/v1/conversations/${props.conversationId}/messages/${props.messageId}/reactions/${encodeURIComponent(emoji)}`
      );
    } catch (e) {
      console.error('Failed to remove reaction', e);
    }
  };

  const reloadMessages = () => {
    messageStore.clearMessages();
    messageStore.loadMessages(props.conversationId);
  };

  const toggleReaction = async (reaction: MessageReaction) => {
    if (hasUserReacted(reaction)) {
      await removeReaction(reaction.emoji);
    } else {
      await addReaction(reaction.emoji);
    }
    reloadMessages();
  };

  const handlePickerSelect = async (emoji: string) => {
    setShowPicker(false);
    await addReaction(emoji);
    reloadMessages();
  };

  return (
    <div class={styles.reactionList}>
      <For each={props.reactions}>
        {(reaction) => (
          <button
            data-testid={`reaction-badge-${reaction.emoji}`}
            classList={{
              [styles.reactionBadge]: true,
              [styles.reactionBadgeActive]: hasUserReacted(reaction),
              [styles.reactionBadgeDefault]: !hasUserReacted(reaction),
            }}
            onClick={() => toggleReaction(reaction)}
            title={`${reaction.count} reaction${reaction.count !== 1 ? 's' : ''}`}
          >
            <span>{reaction.emoji}</span>
            <span data-testid={`reaction-count-${reaction.emoji}`} class={styles.reactionCount}>{reaction.count}</span>
          </button>
        )}
      </For>

      {/* Add reaction button */}
      <div class={styles.addReactionWrapper}>
        <button
          data-testid="reaction-add-button"
          class={styles.addReactionButton}
          onClick={() => setShowPicker(!showPicker())}
          title="Add reaction"
          aria-label="Add reaction"
        >
          +
        </button>

        <Show when={showPicker()}>
          <div class={styles.pickerBackdrop} aria-hidden="true" onClick={() => setShowPicker(false)} />
          <div class={styles.pickerPopover}>
            <EmojiPicker serverId={props.serverId} onSelect={handlePickerSelect} onClose={() => setShowPicker(false)} />
          </div>
        </Show>
      </div>
    </div>
  );
}
