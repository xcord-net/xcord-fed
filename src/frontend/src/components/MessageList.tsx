import { For, Show, createEffect, createSignal, onMount } from 'solid-js';
import { useParams } from '@solidjs/router';
import { createVirtualizer } from '@tanstack/solid-virtual';
import { useMessages } from '../stores/message.store';
import { usePins } from '../stores/pin.store';
import { useAuth } from '../stores/auth.store';
import { useServers } from '../stores/server.store';
import { useThreads } from '../stores/thread.store';
import { api } from '../api/client';
import MarkdownRenderer from './MarkdownRenderer';
import EmbedDisplay from './EmbedDisplay';
import ReactionDisplay from './ReactionDisplay';
import EmojiPicker from './EmojiPicker';
import PollDisplay from './PollDisplay';
import type { Poll } from './PollDisplay';
import Modal from './ui/Modal';
import type { Message, MessageAttachment } from '../types/message';

/** Renders the attachment list for a message, showing image thumbnails and file links. */
function AttachmentList(props: { attachments: MessageAttachment[] }) {
  return (
    <div class="mt-1 flex flex-wrap gap-2">
      <For each={props.attachments}>
        {(attachment) => (
          <Show
            when={attachment.thumbnailUrl}
            fallback={
              <a
                href={attachment.downloadUrl}
                target="_blank"
                rel="noopener noreferrer"
                class="text-xs text-xcord-brand underline"
                aria-label={`Download ${attachment.fileName}`}
              >
                {attachment.fileName}
              </a>
            }
          >
            <a
              href={attachment.downloadUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={`View ${attachment.fileName}`}
            >
              <img
                src={attachment.thumbnailUrl}
                alt={attachment.fileName}
                class="max-h-48 max-w-xs rounded object-contain"
                data-attachment-thumbnail
              />
            </a>
          </Show>
        )}
      </For>
    </div>
  );
}

/** Fetches a poll from the backend and renders PollDisplay once loaded. */
function PollContainer(props: { pollId: string; isAuthor: boolean }) {
  const [poll, setPoll] = createSignal<Poll | null>(null);

  onMount(async () => {
    try {
      const data = await api.get<{
        id: string;
        question: string;
        allowMultipleAnswers: boolean;
        isClosed: boolean;
        expiresAt?: string;
        options: Array<{ id: string; text: string; voteCount: number }>;
        userVotes?: string[];
      }>(`/api/v1/polls/${props.pollId}`);

      const totalVotes = data.options.reduce((sum, o) => sum + o.voteCount, 0);
      setPoll({
        question: data.question,
        options: data.options.map((o) => ({
          id: String(o.id),
          text: o.text,
          voteCount: o.voteCount,
        })),
        allowMultiSelect: data.allowMultipleAnswers,
        totalVotes,
        userVotedOptionIds: (data.userVotes ?? []).map(String),
        expiresAt: data.expiresAt,
        isClosed: data.isClosed,
      });
    } catch {
      // Poll failed to load - render nothing
    }
  });

  return (
    <Show when={poll()}>
      {(p) => (
        <PollDisplay
          pollId={props.pollId}
          poll={p()}
          canEnd={props.isAuthor}
          onVoted={(updated) => setPoll(updated)}
        />
      )}
    </Show>
  );
}

interface MessageListProps {
  conversationId: string;
}

// ManageMessages permission bit (must match backend Permission.ManageMessages = 1L << 7)
const MANAGE_MESSAGES_BIT = 128n;

export default function MessageList(props: MessageListProps) {
  const messageStore = useMessages();
  const pinStore = usePins();
  const authStore = useAuth();
  const serverStore = useServers();
  const threadStore = useThreads();
  const params = useParams();
  const [createThreadMessageId, setCreateThreadMessageId] = createSignal<string | null>(null);
  const [threadNameInput, setThreadNameInput] = createSignal('');
  let scrollContainer: HTMLDivElement | undefined;
  const [isAtBottom, setIsAtBottom] = createSignal(true);
  const [reactionPickerMessageId, setReactionPickerMessageId] = createSignal<string | null>(null);
  const [deleteConfirmMessageId, setDeleteConfirmMessageId] = createSignal<string | null>(null);
  const [myPermissions, setMyPermissions] = createSignal<bigint>(0n);

  const currentUserId = () => authStore.user?.id;

  const isServerOwner = () => {
    const serverId = params.serverId;
    if (!serverId) return false;
    const server = serverStore.servers.find(s => s.id === serverId);
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

  const virtualizer = createVirtualizer<HTMLDivElement, HTMLDivElement>({
    get count() {
      return messageStore.messages.length;
    },
    getScrollElement: () => scrollContainer ?? null,
    estimateSize: (index) => {
      // Grouped messages (no avatar/username header) are shorter
      const messages = messageStore.messages;
      if (index === 0) return 72;
      const prev = messages[index - 1];
      const curr = messages[index];
      if (!prev || !curr) return 72;
      if (prev.authorId !== curr.authorId) return 72;
      const timeDiff =
        new Date(curr.createdAt).getTime() - new Date(prev.createdAt).getTime();
      return timeDiff < 5 * 60 * 1000 ? 36 : 72;
    },
    overscan: 10,
    // Messages list grows downward - reverse scroll (newest at bottom)
    getItemKey: (index) => messageStore.messages[index]?.id ?? index,
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

      messageStore.loadMessages(props.conversationId, messageStore.messages[0]?.id).then(() => {
        // Maintain scroll position after prepending messages
        if (scrollContainer) {
          scrollContainer.scrollTop = scrollContainer.scrollHeight - oldHeight + oldScrollTop;
        }
      });
    }
  };

  const shouldGroupWithPrevious = (message: Message, index: number): boolean => {
    if (index === 0) return false;
    const previousMessage = messageStore.messages[index - 1];
    if (previousMessage.authorId !== message.authorId) return false;

    const timeDiff =
      new Date(message.createdAt).getTime() - new Date(previousMessage.createdAt).getTime();
    return timeDiff < 5 * 60 * 1000; // 5 minutes
  };

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div
      ref={scrollContainer}
      class="flex-1 overflow-y-auto px-4 py-4"
      onScroll={handleScroll}
    >
      {/* Loading indicator while fetching initial messages */}
      <Show when={messageStore.isLoading && messageStore.messages.length === 0}>
        <div class="flex flex-col items-center justify-center h-full space-y-3">
          <div class="flex space-x-1">
            <div class="w-2 h-2 bg-xcord-text-muted rounded-full animate-bounce [animation-delay:-0.3s]" />
            <div class="w-2 h-2 bg-xcord-text-muted rounded-full animate-bounce [animation-delay:-0.15s]" />
            <div class="w-2 h-2 bg-xcord-text-muted rounded-full animate-bounce" />
          </div>
          <p class="text-xcord-text-muted text-sm">Loading messages...</p>
        </div>
      </Show>

      {/* Load-more spinner shown at top when fetching older messages */}
      <Show when={messageStore.isLoading && messageStore.messages.length > 0}>
        <div class="flex items-center justify-center py-3">
          <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      {/* Virtual scroll container */}
      <Show when={messageStore.messages.length > 0}>
        <div
          style={{
            height: `${virtualizer.getTotalSize()}px`,
            position: 'relative',
          }}
        >
          <For each={virtualizer.getVirtualItems()}>
            {(virtualRow) => {
              const message = () => messageStore.messages[virtualRow.index];
              const grouped = () => shouldGroupWithPrevious(message(), virtualRow.index);

              return (
                <div
                  data-index={virtualRow.index}
                  ref={(el) => virtualizer.measureElement(el)}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    transform: `translateY(${virtualRow.start}px)`,
                  }}
                >
                  <Show when={message()}>
                    <div
                      class={`group/msg relative ${grouped() ? 'pl-14 hover:bg-xcord-bg-primary/30' : 'hover:bg-xcord-bg-primary/30'}`}
                    >
                      {/* Hover action bar - uses CSS group-hover for visibility to survive virtualizer DOM re-creation */}
                      <div role="toolbar" aria-label="Message actions" class={`absolute right-2 top-0 bg-xcord-bg-tertiary rounded shadow-lg border border-xcord-border z-30 ${
                        messageStore.editingMessageId
                          ? 'hidden'
                          : reactionPickerMessageId() === message().id
                            ? 'flex'
                            : 'hidden group-hover/msg:flex group-focus-within/msg:flex'
                      }`}>
                          <button
                            title="Reply"
                            aria-label="Reply"
                            class="px-2 py-1 text-xs text-xcord-text-muted hover:text-white hover:bg-xcord-bg-primary rounded"
                          >
                            &#8617;
                          </button>
                          <Show when={message().authorId === currentUserId()}>
                            <button
                              title="Edit"
                              aria-label="Edit"
                              class="px-2 py-1 text-xs text-xcord-text-muted hover:text-white hover:bg-xcord-bg-primary rounded"
                              onClick={() => messageStore.startEditing(message().id, message().content)}
                            >
                              &#9999;&#65039;
                            </button>
                          </Show>
                          <Show when={canDeleteMessage(message())}>
                            <button
                              title="Delete"
                              aria-label="Delete"
                              class="px-2 py-1 text-xs text-xcord-text-muted hover:text-red-400 hover:bg-xcord-bg-primary rounded"
                              onClick={() => setDeleteConfirmMessageId(message().id)}
                            >
                              &#128465;
                            </button>
                          </Show>
                          <div class="relative">
                            <button
                              title="Add reaction"
                              aria-label="Add reaction"
                              class="px-2 py-1 text-xs text-xcord-text-muted hover:text-white hover:bg-xcord-bg-primary rounded"
                              onClick={() => setReactionPickerMessageId(
                                reactionPickerMessageId() === message().id ? null : message().id
                              )}
                            >
                              &#128578;
                            </button>
                            <Show when={reactionPickerMessageId() === message().id}>
                              <div class="fixed inset-0 z-40" aria-hidden="true" onClick={() => setReactionPickerMessageId(null)} />
                              <div class="absolute right-0 top-full mt-1 z-50">
                                <EmojiPicker
                                  serverId={params.serverId}
                                  onSelect={async (emoji) => {
                                    setReactionPickerMessageId(null);
                                    try {
                                      await api.put(
                                        `/api/v1/conversations/${props.conversationId}/messages/${message().id}/reactions/${encodeURIComponent(emoji)}`
                                      );
                                      // Reload messages to show the new reaction
                                      messageStore.clearMessages();
                                      messageStore.loadMessages(props.conversationId);
                                    } catch (e) {
                                      console.error('Failed to add reaction', e);
                                    }
                                  }}
                                  onClose={() => setReactionPickerMessageId(null)}
                                />
                              </div>
                            </Show>
                          </div>
                          <button
                            title="Pin"
                            aria-label="Pin message"
                            class="px-2 py-1 text-xs text-xcord-text-muted hover:text-yellow-400 hover:bg-xcord-bg-primary rounded"
                            onClick={() => {
                              if (message().isPinned) {
                                pinStore.unpinMessage(props.conversationId, message().id);
                              } else {
                                pinStore.pinMessage(props.conversationId, message().id);
                              }
                            }}
                          >
                            &#128204;
                          </button>
                          <Show when={params.channelId}>
                            <button
                              title="Create Thread"
                              aria-label="Start thread"
                              class="px-2 py-1 text-xs text-xcord-text-muted hover:text-white hover:bg-xcord-bg-primary rounded"
                              onClick={() => {
                                setCreateThreadMessageId(message().id);
                                setThreadNameInput('');
                              }}
                            >
                              &#35;&#43;
                            </button>
                          </Show>
                        </div>

                      {/* Thread creation form - shown inline below the action bar when triggered */}
                        <Show when={createThreadMessageId() === message().id}>
                          <div class="mt-1 ml-14 flex items-center gap-2 p-2 bg-xcord-bg-tertiary rounded border border-xcord-border">
                            <input
                              id="thread-name"
                              type="text"
                              placeholder="Thread name"
                              class="flex-1 bg-xcord-bg-primary text-xcord-text-primary text-sm rounded px-2 py-1 border border-xcord-border outline-none focus:border-xcord-brand"
                              value={threadNameInput()}
                              onInput={(e) => setThreadNameInput((e.target as HTMLInputElement).value)}
                              onKeyDown={(e) => {
                                if (e.key === 'Escape') setCreateThreadMessageId(null);
                              }}
                            />
                            <button
                              class="px-3 py-1 text-xs bg-xcord-brand text-white rounded hover:bg-xcord-brand-hover"
                              onClick={async () => {
                                const name = threadNameInput().trim();
                                if (!name) return;
                                const channelId = params.channelId;
                                if (!channelId) return;
                                await threadStore.createThread(channelId, message().id, name);
                                setCreateThreadMessageId(null);
                                setThreadNameInput('');
                              }}
                            >
                              Create Thread
                            </button>
                            <button
                              class="px-2 py-1 text-xs text-xcord-text-muted hover:text-white"
                              onClick={() => setCreateThreadMessageId(null)}
                            >
                              Cancel
                            </button>
                          </div>
                        </Show>

                      <Show
                        when={!grouped()}
                        fallback={
                          <div class="py-0.5">
                            <span class="text-sm text-xcord-text-primary">
                              <MarkdownRenderer content={message().content} />
                            </span>
                            <Show when={message().editedAt}>
                              <span class="text-xs text-xcord-text-muted ml-1">(edited)</span>
                            </Show>
                            {/* Attachments */}
                            <Show when={(message().attachments?.length ?? 0) > 0}>
                              <AttachmentList attachments={message().attachments!} />
                            </Show>
                            {/* Embeds */}
                            <Show when={(message().embeds?.length ?? 0) > 0}>
                              <For each={message().embeds}>
                                {(embed) => <EmbedDisplay embed={embed} />}
                              </For>
                            </Show>
                            {/* Poll */}
                            <Show when={message().type === 'PollCreated' && message().pollId}>
                              <PollContainer
                                pollId={message().pollId!}
                                isAuthor={message().authorId === currentUserId()}
                              />
                            </Show>
                            {/* Reactions */}
                            <Show when={(message().reactions?.length ?? 0) > 0}>
                              <ReactionDisplay
                                reactions={message().reactions!}
                                messageId={message().id}
                                conversationId={props.conversationId}
                                serverId={params.serverId}
                              />
                            </Show>
                          </div>
                        }
                      >
                        <div class="flex space-x-3 py-1">
                          {/* Avatar */}
                          <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold flex-shrink-0">
                            <Show when={message().authorAvatarUrl} fallback={message().authorUsername?.charAt(0).toUpperCase() || 'U'}>
                              <img
                                src={message().authorAvatarUrl}
                                alt={message().authorUsername}
                                class="w-full h-full rounded-full object-cover"
                              />
                            </Show>
                          </div>

                          {/* Message content */}
                          <div class="flex-1 min-w-0">
                            <div class="flex items-baseline space-x-2">
                              {/* Card 170: role color applied inline to username */}
                              <span
                                class="font-semibold"
                                style={{ color: message().authorRoleColor ?? 'white' }}
                              >
                                {message().authorUsername || 'Unknown User'}
                              </span>
                              <span class="text-xs text-xcord-text-muted">{formatTime(message().createdAt)}</span>
                            </div>

                            <Show when={message().replyToId}>
                              <div class="text-xs text-xcord-text-muted mb-1">
                                Replying to a message
                              </div>
                            </Show>

                            <div class="text-sm text-xcord-text-primary break-words">
                              <MarkdownRenderer content={message().content} />
                            </div>
                            <Show when={message().editedAt}>
                              <span class="text-xs text-xcord-text-muted">(edited)</span>
                            </Show>
                            {/* Attachments */}
                            <Show when={(message().attachments?.length ?? 0) > 0}>
                              <AttachmentList attachments={message().attachments!} />
                            </Show>
                            {/* Embeds */}
                            <Show when={(message().embeds?.length ?? 0) > 0}>
                              <For each={message().embeds}>
                                {(embed) => <EmbedDisplay embed={embed} />}
                              </For>
                            </Show>
                            {/* Poll */}
                            <Show when={message().type === 'PollCreated' && message().pollId}>
                              <PollContainer
                                pollId={message().pollId!}
                                isAuthor={message().authorId === currentUserId()}
                              />
                            </Show>
                            {/* Reactions */}
                            <Show when={(message().reactions?.length ?? 0) > 0}>
                              <ReactionDisplay
                                reactions={message().reactions!}
                                messageId={message().id}
                                conversationId={props.conversationId}
                                serverId={params.serverId}
                              />
                            </Show>
                          </div>
                        </div>
                      </Show>
                    </div>
                  </Show>
                </div>
              );
            }}
          </For>
        </div>
      </Show>

      <Modal
        open={deleteConfirmMessageId() !== null}
        onClose={() => setDeleteConfirmMessageId(null)}
        title="Delete Message"
        size="sm"
        role="alertdialog"
      >
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-4">Are you sure you want to delete this message? This cannot be undone.</p>
          <div class="flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm text-xcord-text-primary bg-xcord-bg-primary hover:bg-xcord-bg-tertiary rounded transition-colors"
              onClick={() => setDeleteConfirmMessageId(null)}
            >
              Cancel
            </button>
            <button
              class="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded transition-colors"
              onClick={() => {
                const id = deleteConfirmMessageId();
                if (id) messageStore.deleteMessage(props.conversationId, id);
                setDeleteConfirmMessageId(null);
              }}
            >
              Delete
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
