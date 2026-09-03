import { For, Show, createEffect, createMemo, createSignal, onCleanup, onMount } from 'solid-js';
import { useParams } from '@solidjs/router';
import { useMessages } from '../../stores/message.store';
import { usePins } from '../../stores/pin.store';
import { useAuth } from '../../stores/auth.store';
import { useServers } from '../../stores/server.store';
import { useThreads } from '../../stores/thread.store';
import { useModals } from '../../stores/modal.store';
import { useToasts } from '../../stores/toast.store';
import { api } from '../../api/client';
import type { Message } from '../../types/message';
import MessageRow from './MessageRow';
import DeleteMessageModal from './DeleteMessageModal';
import { MANAGE_MESSAGES_BIT, shouldGroupWithPrevious, startsNewDay, formatDayLabel } from './helpers';
import { buildLanes } from './lanes';
import { createLaneHeat, prefersMotion } from './lane-heat';
import Flexbox from '../ui/Flexbox';
import styles from './MessageList.module.css';
import rowStyles from './MessageRow.module.css';
import EmptyState from '../ui/EmptyState';

/** How often heat is recomputed while a lane is lit. Fast enough to read as a
 *  smooth falloff, slow enough to be invisible in a profile. */
const HEAT_TICK_MS = 100;

/** How many older pages jump-to-message will load before giving up. */
const JUMP_MAX_PAGES = 5;

/** How long the landing flash stays on the jumped-to row - matches messageFlash. */
const JUMP_FLASH_MS = 1200;

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
  const modals = useModals();
  const params = useParams();
  const [createThreadMessageId, setCreateThreadMessageId] = createSignal<string | null>(null);
  const [threadNameInput, setThreadNameInput] = createSignal('');
  let scrollContainer: HTMLDivElement | undefined;
  const [isAtBottom, setIsAtBottom] = createSignal(true);
  const [reactionPickerMessageId, setReactionPickerMessageId] = createSignal<string | null>(null);
  const [deleteConfirmMessageId, setDeleteConfirmMessageId] = createSignal<string | null>(null);
  const [isDeleting, setIsDeleting] = createSignal(false);
  const [myPermissions, setMyPermissions] = createSignal<bigint>(0n);

  // ── Conversation lanes and heat ──
  const lanes = createMemo(() => buildLanes(messageStore.messages));
  const laneHeat = createLaneHeat({ enabled: prefersMotion() });
  // Ticker drives the falloff. It runs only while a lane is lit and stops itself
  // once everything has cooled, so an idle channel costs nothing.
  const [heatNow, setHeatNow] = createSignal(0);
  let heatTimer: ReturnType<typeof setInterval> | undefined;
  let lastSeenMessageId: string | null = null;

  const stopHeatTicker = () => {
    if (heatTimer === undefined) return;
    clearInterval(heatTimer);
    heatTimer = undefined;
  };

  const startHeatTicker = () => {
    if (heatTimer !== undefined) return;
    heatTimer = setInterval(() => {
      const now = Date.now();
      setHeatNow(now);
      if (!laneHeat.hasHotLanes(now)) stopHeatTicker();
    }, HEAT_TICK_MS);
  };

  onCleanup(stopHeatTicker);

  // Light a lane whenever a reply lands in it. Covers all three arrival routes -
  // own optimistic send, the POST echo, and Chat_MessageCreated over SignalR -
  // because each ends up appending to the same store.
  createEffect(() => {
    const list = messageStore.messages;
    const laneMap = lanes();
    if (list.length === 0) {
      lastSeenMessageId = null;
      return;
    }

    const newest = list[list.length - 1];
    if (newest.id === lastSeenMessageId) return;

    const isFirstPass = lastSeenMessageId === null;
    const previousIndex = lastSeenMessageId
      ? list.findIndex((m) => m.id === lastSeenMessageId)
      : -1;
    lastSeenMessageId = newest.id;

    // Heat marks live activity, so the backlog never lights up on channel open -
    // an hour-old reply is still a lane, just not a hot one.
    if (isFirstPass) return;

    // The last message seen can vanish from the list: an optimistic "pending-" id
    // is swapped for the real one once the POST returns. Without a reference point
    // there is no safe way to tell what is new, so only consider the newest row
    // rather than lighting every lane in the window.
    const firstUnseen = previousIndex >= 0 ? previousIndex + 1 : list.length - 1;

    const now = Date.now();
    let lit = false;
    for (let i = firstUnseen; i < list.length; i++) {
      const lane = laneMap.get(list[i].id);
      if (!lane || !list[i].replyToId) continue;
      laneHeat.noteReply(lane.laneId, now);
      lit = true;
    }

    if (lit) {
      setHeatNow(now);
      startHeatTicker();
    }
  });

  const heatForRow = (laneId: string | undefined) =>
    laneId ? laneHeat.heatFor(laneId, heatNow()) : 0;

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

  /** Scrolls to a message and flashes it. Pages backwards when the target has not
   *  been loaded yet, bounded so a reply to something ancient cannot spin forever. */
  const jumpToMessage = async (messageId: string) => {
    const focusRow = () => {
      const row = scrollContainer?.querySelector<HTMLElement>(
        `[data-message-id="${CSS.escape(messageId)}"]`,
      );
      if (!row) return false;

      row.scrollIntoView({ block: 'center', behavior: prefersMotion() ? 'smooth' : 'auto' });
      row.classList.remove(rowStyles.messageRowFlash);
      // Force a reflow so re-adding the class restarts the animation when the same
      // message is jumped to twice in a row.
      void row.offsetWidth;
      row.classList.add(rowStyles.messageRowFlash);
      setTimeout(() => row.classList.remove(rowStyles.messageRowFlash), JUMP_FLASH_MS);
      return true;
    };

    if (focusRow()) return;

    for (let page = 0; page < JUMP_MAX_PAGES; page++) {
      const cursor = messageStore.nextCursor;
      if (!cursor || !messageStore.hasMore) break;
      await messageStore.loadMessages(props.conversationId, cursor);
      // Let the newly prepended rows render before looking for the target.
      await new Promise<void>((resolve) => queueMicrotask(resolve));
      if (focusRow()) return;
    }

    toasts.error('That message is too far back to jump to.');
  };

  const handleSelectReaction = async (messageId: string, emoji: string) => {
    setReactionPickerMessageId(null);
    try {
      await api.put(
        `/api/v1/conversations/${props.conversationId}/messages/${messageId}/reactions/${encodeURIComponent(emoji)}`,
      );
      // Patch just this message so the user's scroll position is preserved.
      // The server announces the new reaction set to the conversation, so there
      // is nothing to fetch here - and fetching raced that announcement.
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
      const thread = await threadStore.createThread(channelId, parentMessageId, name);
      setCreateThreadMessageId(null);
      setThreadNameInput('');
      // Land in the thread that was just made. Without this the form simply
      // vanished: the thread existed, but nothing opened it and nothing said it
      // had been created, so naming one looked like a button that did nothing.
      threadStore.setActiveThread(thread.id);
      modals.openThreads();
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
        <EmptyState
          title="This is the start of the conversation"
          body="Send a message below and it lands here."
          data-testid="messages-empty"
        />
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
              const newDay = () =>
                startsNewDay(messageStore.messages, message, index());
              const lane = () => lanes().get(message.id);

              return (
                <>
                <Show when={newDay()}>
                  <div class={styles.dayDivider} data-testid="message-day-divider">
                    <span class={styles.dayLabel}>{formatDayLabel(message.createdAt)}</span>
                  </div>
                </Show>
                <MessageRow
                  message={message}
                  conversationId={props.conversationId}
                  serverId={params.serverId}
                  channelId={params.channelId}
                  grouped={grouped()}
                  lane={lane()}
                  laneHeat={heatForRow(lane()?.laneId)}
                  onJumpTo={(id) => void jumpToMessage(id)}
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
                </>
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
