import { For, Show, createSignal, createEffect, onCleanup } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';

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

  // Auto-refresh every 30 seconds to keep countdown times accurate
  const refreshInterval = setInterval(() => {
    if (!isLoading()) {
      loadMessages();
    }
  }, 30_000);

  onCleanup(() => clearInterval(refreshInterval));

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
    <div class="flex flex-col h-full">
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">Scheduled Messages</h2>
        <button
          class="text-xcord-text-muted hover:text-white text-xs transition-colors"
          onClick={loadMessages}
          disabled={isLoading()}
          aria-label="Refresh scheduled messages"
          title="Refresh"
        >
          &#8635;
        </button>
      </div>

      <div class="flex-1 overflow-y-auto">
        <Show when={isLoading() && messages().length === 0}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={error()}>
          <div role="alert" class="mx-4 mt-3 px-3 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-xs">
            {error()}
          </div>
        </Show>

        <Show when={!isLoading() && messages().length === 0 && !error()}>
          <div class="flex flex-col items-center justify-center h-32 px-4">
            <p class="text-xcord-text-muted text-sm">No scheduled messages</p>
            <p class="text-xcord-text-muted text-xs mt-1">
              Use the clock button in the compose area to schedule a message.
            </p>
          </div>
        </Show>

        <For each={messages()}>
          {(message) => (
            <div class="px-4 py-3 border-b border-xcord-border hover:bg-xcord-bg-primary/30">
              <div class="flex items-start justify-between gap-2">
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 mb-1">
                    <span
                      class="inline-block w-2 h-2 rounded-full bg-yellow-400 flex-shrink-0"
                      aria-label="Pending"
                      title="Pending"
                    />
                    <span class="text-xs text-yellow-400 font-medium">
                      {formatScheduledTime(message.scheduledAt)}
                    </span>
                  </div>
                  <p class="text-sm text-xcord-text-primary break-words line-clamp-3">
                    {message.content}
                  </p>
                  <p class="text-xs text-xcord-text-muted mt-1">
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
                  class="flex-shrink-0 text-xcord-text-muted hover:text-red-400 text-xs px-2 py-1 rounded border border-xcord-border hover:border-red-400/40 transition-colors disabled:opacity-50"
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
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-4">
            Are you sure you want to cancel this scheduled message? It will not be sent.
          </p>
          <div class="flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm text-xcord-text-primary bg-xcord-bg-primary hover:bg-xcord-bg-tertiary rounded transition-colors"
              onClick={() => setConfirmCancelId(null)}
            >
              Keep
            </button>
            <button
              class="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded transition-colors disabled:opacity-50"
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
