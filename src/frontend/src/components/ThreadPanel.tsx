import { For, Show, onMount, createEffect, createSignal } from 'solid-js';
import { useThreads } from '../stores/thread.store';
import { useAuth } from '../stores/auth.store';
import { useMessages } from '../stores/message.store';
import { api } from '../api/client';
import MessageList from './MessageList';

interface ThreadPanelProps {
  channelId: string;
}

export default function ThreadPanel(props: ThreadPanelProps) {
  const threadStore = useThreads();
  const authStore = useAuth();
  const messageStore = useMessages();

  // Edit thread name state
  const [editingName, setEditingName] = createSignal(false);
  const [editNameInput, setEditNameInput] = createSignal('');

  // Track whether the current user is a member of the active thread
  const [isMember, setIsMember] = createSignal(false);
  const [memberCheckDone, setMemberCheckDone] = createSignal(false);

  // Thread compose state
  const [threadMsg, setThreadMsg] = createSignal('');
  const [isSending, setIsSending] = createSignal(false);

  onMount(() => {
    threadStore.loadThreads(props.channelId);
  });

  createEffect(() => {
    // Reload when channel changes
    threadStore.loadThreads(props.channelId);
  });

  const activeThread = () =>
    threadStore.threads.find((t) => t.id === threadStore.activeThreadId);

  // Check membership when active thread changes
  createEffect(() => {
    const thread = activeThread();
    if (!thread) {
      setIsMember(false);
      setMemberCheckDone(false);
      return;
    }

    // Optimistically assume we are a member (creator is auto-joined).
    // A real membership check would require a dedicated API endpoint.
    // For now, assume the user is a member if they can see the thread.
    // The creator is always auto-joined per backend logic.
    setIsMember(true);
    setMemberCheckDone(true);
  });

  const handleJoin = async () => {
    const thread = activeThread();
    if (!thread) return;
    try {
      await threadStore.joinThread(props.channelId, thread.id);
      setIsMember(true);
    } catch (e) {
      console.error('Failed to join thread', e);
    }
  };

  const handleLeave = async () => {
    const thread = activeThread();
    if (!thread) return;
    try {
      await threadStore.leaveThread(props.channelId, thread.id);
      setIsMember(false);
    } catch (e) {
      console.error('Failed to leave thread', e);
    }
  };

  const handleEditName = () => {
    const thread = activeThread();
    if (!thread) return;
    setEditNameInput(thread.name);
    setEditingName(true);
  };

  const handleSaveName = async () => {
    const thread = activeThread();
    if (!thread) return;
    const name = editNameInput().trim();
    if (!name) return;
    try {
      await threadStore.updateThreadName(props.channelId, thread.id, name);
      setEditingName(false);
    } catch (e) {
      console.error('Failed to update thread name', e);
    }
  };

  const handleSendThreadMessage = async () => {
    const thread = activeThread();
    if (!thread || !threadMsg().trim() || isSending()) return;

    setIsSending(true);
    try {
      await messageStore.sendMessage(thread.conversationId, threadMsg().trim());
      setThreadMsg('');
    } catch (e) {
      console.error('Failed to send thread message', e);
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary border-l border-xcord-border w-80">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Threads</h2>
      </div>

      <Show
        when={threadStore.activeThreadId}
        fallback={
          <div class="flex-1 overflow-y-auto">
            <Show when={threadStore.isLoading}>
              <div class="flex items-center justify-center h-32">
                <p class="text-xcord-text-muted">Loading threads...</p>
              </div>
            </Show>

            <Show when={!threadStore.isLoading && threadStore.threads.length === 0}>
              <div class="flex items-center justify-center h-32">
                <p class="text-xcord-text-muted">No active threads</p>
              </div>
            </Show>

            <For each={threadStore.threads}>
              {(thread) => (
                <button
                  class="w-full px-4 py-3 hover:bg-xcord-bg-primary/50 transition border-b border-xcord-border text-left"
                  onClick={() => threadStore.setActiveThread(thread.id)}
                >
                  <div class="flex items-start justify-between">
                    <div class="flex-1 min-w-0">
                      <h3 class="text-white font-medium truncate">{thread.name}</h3>
                      <p class="text-xs text-xcord-text-muted mt-1">
                        {thread.messageCount} {thread.messageCount === 1 ? 'message' : 'messages'}
                        {thread.memberCount > 0 && (
                          <span class="ml-2">{thread.memberCount} {thread.memberCount === 1 ? 'member' : 'members'}</span>
                        )}
                      </p>
                    </div>
                    <Show when={thread.archived}>
                      <span class="text-xs bg-xcord-bg-primary text-xcord-text-muted px-2 py-1 rounded">
                        Archived
                      </span>
                    </Show>
                  </div>
                </button>
              )}
            </For>
          </div>
        }
      >
        <div class="flex-1 flex flex-col min-h-0">
          {/* Thread header */}
          <div class="px-4 py-2 border-b border-xcord-border flex items-center justify-between gap-2">
            <Show
              when={editingName()}
              fallback={
                <div class="flex items-center gap-2 flex-1 min-w-0">
                  <h3 class="text-white font-medium truncate flex-1">
                    {activeThread()?.name}
                  </h3>
                  <button
                    class="text-xcord-text-muted hover:text-white text-xs flex-shrink-0"
                    onClick={handleEditName}
                    aria-label="Edit Thread Name"
                  >
                    Edit Thread Name
                  </button>
                </div>
              }
            >
              <div class="flex items-center gap-1 flex-1 min-w-0">
                <input
                  id="thread-name-edit"
                  type="text"
                  class="flex-1 bg-xcord-bg-primary text-xcord-text-primary text-sm rounded px-2 py-1 border border-xcord-border outline-none focus:border-xcord-brand min-w-0"
                  value={editNameInput()}
                  onInput={(e) => setEditNameInput(e.currentTarget.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleSaveName();
                    if (e.key === 'Escape') setEditingName(false);
                  }}
                />
                <button
                  class="text-xcord-brand hover:text-white text-xs flex-shrink-0 px-2 py-1 rounded border border-xcord-brand hover:bg-xcord-brand transition-colors"
                  onClick={handleSaveName}
                >
                  Save
                </button>
              </div>
            </Show>
            <button
              class="text-xcord-text-muted hover:text-white flex-shrink-0"
              onClick={() => {
                setEditingName(false);
                threadStore.setActiveThread(null);
              }}
              aria-label="Close thread"
            >
              Close
            </button>
          </div>

          {/* Member info and join/leave */}
          <div class="px-4 py-2 border-b border-xcord-border flex items-center justify-between">
            <span class="text-xs text-xcord-text-muted">
              {activeThread()?.memberCount ?? 0}{' '}
              {(activeThread()?.memberCount ?? 0) === 1 ? 'member' : 'members'}
            </span>
            <Show when={memberCheckDone()}>
              <Show
                when={isMember()}
                fallback={
                  <button
                    class="text-xs px-2 py-1 bg-xcord-brand text-white rounded hover:bg-xcord-brand-hover transition-colors"
                    onClick={handleJoin}
                  >
                    Join Thread
                  </button>
                }
              >
                <button
                  class="text-xs px-2 py-1 bg-xcord-bg-primary text-xcord-text-muted rounded hover:text-white hover:bg-xcord-bg-tertiary transition-colors border border-xcord-border"
                  onClick={handleLeave}
                >
                  Leave Thread
                </button>
              </Show>
            </Show>
          </div>

          {/* Thread messages */}
          <div class="flex-1 min-h-0 overflow-hidden">
            <MessageList
              conversationId={
                activeThread()?.conversationId || ''
              }
            />
          </div>

          {/* Thread compose */}
          <div class="px-3 pb-3 pt-1 border-t border-xcord-border">
            <div class="bg-xcord-bg-primary rounded-lg px-3 py-2 flex items-end gap-2">
              <textarea
                class="flex-1 bg-transparent text-xcord-text-primary placeholder-xcord-text-muted resize-none outline-none text-sm"
                placeholder="Reply in thread..."
                rows={1}
                value={threadMsg()}
                onInput={(e) => {
                  const target = e.currentTarget;
                  setThreadMsg(target.value);
                  target.style.height = 'auto';
                  target.style.height = `${Math.min(target.scrollHeight, 120)}px`;
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    handleSendThreadMessage();
                  }
                }}
                disabled={isSending()}
              />
            </div>
          </div>
        </div>
      </Show>
    </div>
  );
}
