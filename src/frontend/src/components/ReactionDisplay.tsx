import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';
import { useMessages } from '../stores/message.store';
import { useAuth } from '../stores/auth.store';
import EmojiPicker from './EmojiPicker';
import type { MessageReaction } from '../types/message';

interface ReactionDisplayProps {
  reactions: MessageReaction[];
  messageId: string;
  conversationId: string;
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
    <div class="flex flex-wrap items-center gap-1 mt-1">
      <For each={props.reactions}>
        {(reaction) => (
          <button
            class={`flex items-center gap-1 rounded-full px-2 py-0.5 text-sm border transition-colors ${
              hasUserReacted(reaction)
                ? 'bg-xcord-brand/10 border-xcord-brand text-xcord-text-primary'
                : 'bg-xcord-bg-tertiary hover:bg-xcord-bg-secondary border-xcord-border text-xcord-text-primary'
            }`}
            onClick={() => toggleReaction(reaction)}
            title={`${reaction.count} reaction${reaction.count !== 1 ? 's' : ''}`}
          >
            <span>{reaction.emoji}</span>
            <span class="text-xcord-text-muted text-xs">{reaction.count}</span>
          </button>
        )}
      </For>

      {/* Add reaction button */}
      <div class="relative">
        <button
          class="flex items-center justify-center w-7 h-6 rounded-full bg-xcord-bg-tertiary hover:bg-xcord-bg-secondary border border-xcord-border text-xcord-text-muted hover:text-xcord-text-primary transition-colors text-sm"
          onClick={() => setShowPicker(!showPicker())}
          title="Add reaction"
        >
          +
        </button>

        <Show when={showPicker()}>
          <div
            class="absolute bottom-full left-0 mb-1 z-50"
            onMouseLeave={() => setShowPicker(false)}
          >
            <EmojiPicker onSelect={handlePickerSelect} />
          </div>
        </Show>
      </div>
    </div>
  );
}
