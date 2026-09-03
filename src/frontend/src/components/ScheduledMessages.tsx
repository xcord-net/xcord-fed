import { For, Show, createSignal, createEffect, onCleanup } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';
import styles from './ScheduledMessages.module.css';
import EmptyState from './ui/EmptyState';
import { Clock } from 'lucide-solid';

interface ScheduledMessage {
  id: string;
  conversationId: string;
  authorId: string;
  content: string;
  scheduledAt: string;
  sentAt: string | null;
}

interface ScheduledMessagesProps {
  channelId: string;
}

export default function ScheduledMessages(props: ScheduledMessagesProps) {
  const [messages, setMessages] = createSignal<ScheduledMessage[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal('');
  const [cancellingId, setCancellingId] = createSignal<string | null>(null);
  const [confirmCancelId, setConfirmCancelId] = createSignal<string | null>(null);

  const loadMessages = async () => {
    setIsLoading(true);
    setError('');
    try {
      const result = await api.get<ScheduledMessage[]>(
        `/api/v1/channels/${props.channelId}/scheduled-messages`,
      );
      setMessages(result);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load scheduled messages'));
    } finally {
      setIsLoading(false);
    }
  };

  // Reload when channelId changes
  createEffect(() => {
    const _channelId = props.channelId;
    void _channelId;
    loadMessages();
  });

  // Auto-refresh every 30 seconds to keep countdown times accurate.
  // Pause polling when the tab is hidden to avoid burning battery/bandwidth
  // in backgrounded tabs; resume (with an immediate refresh) on visibility.
  // See kanban #162.
  let refreshInterval: ReturnType<typeof setInterval> | null = null;

  const tickIfIdle = () => {
    if (!isLoading()) {
      loadMessages();
    }
  };

  const startInterval = () => {
    if (refreshInterval === null) {
      refreshInterval = setInterval(tickIfIdle, 30_000);
    }
  };

  const stopInterval = () => {
    if (refreshInterval !== null) {
      clearInterval(refreshInterval);
      refreshInterval = null;
    }
  };

  const handleVisibilityChange = () => {
    if (document.hidden) {
      stopInterval();
    } else {
      // Fire an immediate refresh on resume so countdowns aren't stale.
      tickIfIdle();
      startInterval();
    }
  };

  if (!document.hidden) {
    startInterval();
  }
  document.addEventListener('visibilitychange', handleVisibilityChange);

  onCleanup(() => {
    stopInterval();
    document.removeEventListener('visibilitychange', handleVisibilityChange);
  });

  const handleCancel = async (messageId: string) => {
    setCancellingId(messageId);
    setConfirmCancelId(null);
    try {
      await api.delete(`/api/v1/channels/${props.channelId}/scheduled-messages/${messageId}`);
      setMessages((prev) => prev.filter((m) => m.id !== messageId));
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to cancel scheduled message'));
    } finally {
      setCancellingId(null);
    }
  };

  const formatScheduledTime = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = date.getTime() - now.getTime();

    // Show relative time if within 24 hours
    if (diffMs > 0 && diffMs < 86_400_000) {
      const hours = Math.floor(diffMs / 3_600_000);
      const minutes = Math.floor((diffMs % 3_600_000) / 60_000);
      if (hours > 0) {
        return `in ${hours}h ${minutes}m`;
      }
      return `in ${minutes}m`;
    }

    // Otherwise show full date/time in user's timezone
    return date.toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  return (
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Scheduled Messages</h2>
        <button
          class={styles.refreshBtn}
          onClick={loadMessages}
          disabled={isLoading()}
          aria-label="Refresh scheduled messages"
          title="Refresh"
        >
          &#8635;
        </button>
      </div>

      <div class={styles.scrollArea}>
        <Show when={isLoading() && messages().length === 0}>
          <div class={styles.loadingCenter}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <Show when={error()}>
          <div role="alert" class={styles.errorAlert}>
            {error()}
          </div>
        </Show>

        <Show when={!isLoading() && messages().length === 0 && !error()}>
          <EmptyState
            icon={Clock}
            title="Nothing scheduled"
            body="Use the clock in the composer to send a message at a set time."
            data-testid="scheduled-messages-empty"
          />
        </Show>

        <For each={messages()}>
          {(message) => (
            <div class={styles.messageItem}>
              <div class={styles.messageInner}>
                <div class={styles.messageBody}>
                  <div class={styles.messageStatus}>
                    <span
                      class={styles.pendingDot}
                      aria-label="Pending"
                      title="Pending"
                    />
                    <span class={styles.scheduledTime}>
                      {formatScheduledTime(message.scheduledAt)}
                    </span>
                  </div>
                  <p class={styles.messageContent}>
                    {message.content}
                  </p>
                  <p class={styles.messageTimestamp}>
                    {new Date(message.scheduledAt).toLocaleString(undefined, {
                      weekday: 'short',
                      month: 'short',
                      day: 'numeric',
                      hour: 'numeric',
                      minute: '2-digit',
                      timeZoneName: 'short',
                    })}
                  </p>
                </div>
                <button
                  class={styles.cancelBtn}
                  onClick={() => setConfirmCancelId(message.id)}
                  disabled={cancellingId() === message.id}
                  aria-label="Cancel scheduled message"
                >
                  {cancellingId() === message.id ? '...' : 'Cancel'}
                </button>
              </div>
            </div>
          )}
        </For>
      </div>

      {/* Cancel confirmation dialog */}
      <Modal
        open={confirmCancelId() !== null}
        onClose={() => setConfirmCancelId(null)}
        title="Cancel Scheduled Message"
        size="sm"
        role="alertdialog"
      >
        <div class={styles.modalBody}>
          <p class={styles.modalText}>
            Are you sure you want to cancel this scheduled message? It will not be sent.
          </p>
          <div class={styles.modalButtons}>
            <button
              class={styles.modalKeepBtn}
              onClick={() => setConfirmCancelId(null)}
            >
              Keep
            </button>
            <button
              class={styles.modalCancelBtn}
              disabled={cancellingId() !== null}
              onClick={() => {
                const id = confirmCancelId();
                if (id) handleCancel(id);
              }}
            >
              Cancel Message
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
