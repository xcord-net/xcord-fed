import { For, Show, createEffect, createSignal, onMount } from 'solid-js';
import { useParams } from '@solidjs/router';
import { useMessages } from '../../stores/message.store';
import { usePins } from '../../stores/pin.store';
import { useAuth } from '../../stores/auth.store';
import { useServers } from '../../stores/server.store';
import { useThreads } from '../../stores/thread.store';
import { useToasts } from '../../stores/toast.store';
import { api } from '../../api/client';
import type { Message } from '../../types/message';
import MessageRow from './MessageRow';
import DeleteMessageModal from './DeleteMessageModal';
import { MANAGE_MESSAGES_BIT, shouldGroupWithPrevious } from './helpers';
import Flexbox from '../ui/Flexbox';
import styles from './MessageList.module.css';

interface MessageListProps {
  conversationId: string;
}

export default function MessageList(props: MessageListProps) {
  const messageStore = useMessages();
  const pinStore = usePins();
  const authStore = useAuth();
  const serverStore = useServers();
  const threadStore = useThreads();
  const toasts = useToasts();
  const params = useParams();
  const [createThreadMessageId, setCreateThreadMessageId] = createSignal<string | null>(null);
  const [threadNameInput, setThreadNameInput] = createSignal('');
  let scrollContainer: HTMLDivElement | undefined;
  const [isAtBottom, setIsAtBottom] = createSignal(true);
  const [reactionPickerMessageId, setReactionPickerMessageId] = createSignal<string | null>(null);
  const [deleteConfirmMessageId, setDeleteConfirmMessageId] = createSignal<string | null>(null);
  const [isDeleting, setIsDeleting] = createSignal(false);
  const [myPermissions, setMyPermissions] = createSignal<bigint>(0n);

  const currentUserId = () => authStore.user?.id;

  const isServerOwner = () => {
    const serverId = params.serverId;
    if (!serverId) return false;
    const server = serverStore.servers.find((s) => s.id === serverId);
    return server?.ownerId === currentUserId();
  };

  const hasManageMessages = () => {
    return (myPermissions() & MANAGE_MESSAGES_BIT) !== 0n;
  };

  const canDeleteMessage = (msg: Message) => {
    return msg.authorId === currentUserId() || isServerOwner() || hasManageMessages();
  };

  // Fetch my effective permissions for the current channel whenever channelId changes.
  // Permissions is a numeric bitmask (LongAsNumberConverter). BigInt handles both
  // number and string inputs safely in case of format changes.
  createEffect(() => {
    const channelId = params.channelId;
    if (!channelId) {
      setMyPermissions(0n);
      return;
    }
    api.get<{ permissions: string | number }>(`/api/v1/channels/${channelId}/my-permissions`)
      .then((data) => {
        try {
          setMyPermissions(BigInt(data.permissions));
        } catch {
          setMyPermissions(0n);
        }
      })
      .catch(() => setMyPermissions(0n));
  });

  // Re-fetch messages whenever the conversation changes
  createEffect(() => {
    const id = props.conversationId;
    messageStore.clearMessages();
    messageStore.loadMessages(id);
  });

  // Scroll to bottom when new messages arrive and user is at bottom
  createEffect(() => {
    const count = messageStore.messages.length;
    if (count === 0) return;
    if (isAtBottom()) {
      // Use a microtask to let the DOM update before scrolling
      queueMicrotask(() => {
        if (scrollContainer) {
          scrollContainer.scrollTop = scrollContainer.scrollHeight;
        }
      });
    }
  });

  // Scroll to bottom on initial mount once messages load
  onMount(() => {
    if (scrollContainer) {
      scrollContainer.scrollTop = scrollContainer.scrollHeight;
    }
  });

  const handleScroll = () => {
    if (!scrollContainer) return;

    const { scrollTop, scrollHeight, clientHeight } = scrollContainer;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
    setIsAtBottom(distanceFromBottom < 50);

    // Load more messages when scrolled near top
    if (scrollTop < 100 && messageStore.hasMore && !messageStore.isLoading) {
      const oldHeight = scrollContainer.scrollHeight;
      const oldScrollTop = scrollContainer.scrollTop;

      const olderCursor = messageStore.nextCursor;
      if (!olderCursor) return;
      messageStore.loadMessages(props.conversationId, olderCursor).then(() => {
        // Maintain scroll position after prepending messages
        if (scrollContainer) {
          scrollContainer.scrollTop = scrollContainer.scrollHeight - oldHeight + oldScrollTop;
        }
      });
    }
  };

  const handleSelectReaction = async (messageId: string, emoji: string) => {
    setReactionPickerMessageId(null);
    try {
      await api.put(
        `/api/v1/conversations/${props.conversationId}/messages/${messageId}/reactions/${encodeURIComponent(emoji)}`,
      );
      // Patch just this message so the user's scroll position is preserved.
      await messageStore.refreshMessage(props.conversationId, messageId);
    } catch (e) {
      console.error('Failed to add reaction', e);
      toasts.error('Could not add reaction. Please try again.');
    }
  };

  const handleSubmitThreadCreate = async (parentMessageId: string) => {
    const name = threadNameInput().trim();
    if (!name) return;
    const channelId = params.channelId;
    if (!channelId) return;
    try {
      await threadStore.createThread(channelId, parentMessageId, name);
      setCreateThreadMessageId(null);
      setThreadNameInput('');
    } catch (e) {
      console.error('Failed to create thread', e);
      toasts.error('Could not create the thread. Please try again.');
    }
  };

  const handleTogglePin = async (msg: Message) => {
    try {
      if (msg.isPinned) {
        await pinStore.unpinMessage(props.conversationId, msg.id);
      } else {
        await pinStore.pinMessage(props.conversationId, msg.id);
      }
    } catch (e) {
      console.error('Failed to toggle pin', e);
      toasts.error(msg.isPinned ? 'Could not unpin the message.' : 'Could not pin the message.');
    }
  };

  return (
    <div
      ref={scrollContainer}
      class={styles.scrollContainer}
      onScroll={handleScroll}
    >
      {/* Loading indicator while fetching initial messages */}
      <Show when={messageStore.isLoading && messageStore.messages.length === 0}>
        <Flexbox direction="vertical" align="center" justify="center" gap={0.75} class={styles.loadingCenter}>
          <Flexbox gap={0.25} class={styles.bounceDots}>
            <div class={styles.bounceDot} />
            <div class={styles.bounceDot} />
            <div class={styles.bounceDot} />
          </Flexbox>
          <p data-testid="messages-loading" class={styles.loadingText}>Loading messages...</p>
        </Flexbox>
      </Show>

      {/* Load-more spinner shown at top when fetching older messages */}
      <Show when={messageStore.isLoading && messageStore.messages.length > 0}>
        <Flexbox align="center" justify="center" class={styles.loadMoreSpinner}>
          <div class={styles.spinner} />
        </Flexbox>
      </Show>

      {/* Empty channel: give the start of the conversation a clear marker instead
          of a blank void. */}
      <Show when={!messageStore.isLoading && messageStore.messages.length === 0}>
        <Flexbox direction="vertical" align="center" justify="center" gap={0.5} class={styles.emptyState}>
          <p data-testid="messages-empty" class={styles.emptyTitle}>This is the start of the conversation</p>
          <p class={styles.emptyBody}>Send a message below to get things going.</p>
        </Flexbox>
      </Show>

      {/* Message list. Rendered in normal document flow (no windowing) so each
          row takes its natural, content-dependent height - wrapped messages
          can never overlap their neighbours. The store caps how many messages
          are loaded and paginates on scroll-up, bounding the node count. */}
      <Show when={messageStore.messages.length > 0}>
        <div class={styles.messageFlow}>
          <For each={messageStore.messages}>
            {(message, index) => {
              const grouped = () =>
                shouldGroupWithPrevious(messageStore.messages, message, index());

              return (
                <MessageRow
                  message={message}
                  conversationId={props.conversationId}
                  serverId={params.serverId}
                  channelId={params.channelId}
                  grouped={grouped()}
                  isAuthor={message.authorId === currentUserId()}
                  canDelete={canDeleteMessage(message)}
                  isReactionPickerOpen={reactionPickerMessageId() === message.id}
                  isThreadCreateOpen={createThreadMessageId() === message.id}
                  threadNameValue={threadNameInput()}
                  onReply={() => messageStore.startReply({
                    id: message.id,
                    authorUsername: message.authorUsername || 'Unknown User',
                    content: (message.content || '').slice(0, 80),
                  })}
                  onEdit={() => messageStore.startEditing(message.id, message.content)}
                  onDelete={() => setDeleteConfirmMessageId(message.id)}
                  onToggleReactionPicker={() =>
                    setReactionPickerMessageId(
                      reactionPickerMessageId() === message.id ? null : message.id,
                    )
                  }
                  onCloseReactionPicker={() => setReactionPickerMessageId(null)}
                  onSelectReaction={(emoji) => handleSelectReaction(message.id, emoji)}
                  onTogglePin={() => handleTogglePin(message)}
                  onStartThread={() => {
                    setCreateThreadMessageId(message.id);
                    setThreadNameInput('');
                  }}
                  onThreadNameInput={(v) => setThreadNameInput(v)}
                  onThreadCreateSubmit={() => handleSubmitThreadCreate(message.id)}
                  onThreadCreateCancel={() => setCreateThreadMessageId(null)}
                />
              );
            }}
          </For>
        </div>
      </Show>

      <DeleteMessageModal
        open={deleteConfirmMessageId() !== null}
        pending={isDeleting()}
        onClose={() => { if (!isDeleting()) setDeleteConfirmMessageId(null); }}
        onConfirm={async () => {
          const id = deleteConfirmMessageId();
          if (!id) return;
          setIsDeleting(true);
          try {
            await messageStore.deleteMessage(props.conversationId, id);
            setDeleteConfirmMessageId(null);
          } catch (e) {
            console.error('Failed to delete message', e);
            toasts.error('Could not delete the message. Please try again.');
          } finally {
            setIsDeleting(false);
          }
        }}
      />
    </div>
  );
}
