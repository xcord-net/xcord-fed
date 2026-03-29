import { For, Show, onMount, createEffect, createSignal } from 'solid-js';
import { useThreads } from '../stores/thread.store';
import { useAuth } from '../stores/auth.store';
import { useMessages } from '../stores/message.store';
import { api } from '../api/client';
import MessageList from './MessageList';
import styles from './ThreadPanel.module.css';

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
    <div data-testid="thread-panel" class={styles.panel}>
      <div class={styles.panelHeader}>
        <h2 class={styles.panelTitle}>Threads</h2>
      </div>

      <Show
        when={threadStore.activeThreadId}
        fallback={
          <div data-testid="thread-list" class={styles.threadList}>
            <Show when={threadStore.isLoading}>
              <div class={styles.centeredStatus}>
                <p class={styles.mutedText}>Loading threads...</p>
              </div>
            </Show>

            <Show when={!threadStore.isLoading && threadStore.threads.length === 0}>
              <div class={styles.centeredStatus}>
                <p data-testid="thread-list-empty" class={styles.mutedText}>No active threads</p>
              </div>
            </Show>

            <For each={threadStore.threads}>
              {(thread) => (
                <button
                  data-testid="thread-list-item"
                  class={styles.threadItem}
                  onClick={() => threadStore.setActiveThread(thread.id)}
                >
                  <div class={styles.threadItemInner}>
                    <div class={styles.threadItemBody}>
                      <h3 class={styles.threadName}>{thread.name}</h3>
                      <p class={styles.threadMeta}>
                        {thread.messageCount} {thread.messageCount === 1 ? 'message' : 'messages'}
                        {thread.memberCount > 0 && (
                          <span class={styles.memberCount}>{thread.memberCount} {thread.memberCount === 1 ? 'member' : 'members'}</span>
                        )}
                      </p>
                    </div>
                    <Show when={thread.archived}>
                      <span class={styles.archivedBadge}>
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
        <div class={styles.activeThreadContainer}>
          {/* Thread header */}
          <div class={styles.threadHeader}>
            <Show
              when={editingName()}
              fallback={
                <div class={styles.threadNameRow}>
                  <h3 class={styles.activeThreadName}>
                    {activeThread()?.name}
                  </h3>
                  <button
                    class={styles.editNameButton}
                    onClick={handleEditName}
                    aria-label="Edit Thread Name"
                  >
                    Edit Thread Name
                  </button>
                </div>
              }
            >
              <div class={styles.editNameRow}>
                <input
                  id="thread-name-edit"
                  type="text"
                  class={styles.editNameInput}
                  value={editNameInput()}
                  onInput={(e) => setEditNameInput(e.currentTarget.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleSaveName();
                    if (e.key === 'Escape') setEditingName(false);
                  }}
                />
                <button
                  class={styles.saveNameButton}
                  onClick={handleSaveName}
                >
                  Save
                </button>
              </div>
            </Show>
            <button
              class={styles.closeButton}
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
          <div class={styles.memberBar}>
            <span class={styles.memberBarCount}>
              {activeThread()?.memberCount ?? 0}{' '}
              {(activeThread()?.memberCount ?? 0) === 1 ? 'member' : 'members'}
            </span>
            <Show when={memberCheckDone()}>
              <Show
                when={isMember()}
                fallback={
                  <button
                    class={styles.joinButton}
                    onClick={handleJoin}
                  >
                    Join Thread
                  </button>
                }
              >
                <button
                  class={styles.leaveButton}
                  onClick={handleLeave}
                >
                  Leave Thread
                </button>
              </Show>
            </Show>
          </div>

          {/* Thread messages */}
          <div data-testid="thread-message-list" class={styles.messageListWrapper}>
            <MessageList
              conversationId={
                activeThread()?.conversationId || ''
              }
            />
          </div>

          {/* Thread compose */}
          <div class={styles.composeArea}>
            <div class={styles.composeBox}>
              <textarea
                data-testid="thread-compose-input"
                class={styles.composeInput}
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
