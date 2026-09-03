import { For, Show, createEffect } from 'solid-js';
import { usePins } from '../stores/pin.store';
import { useToasts } from '../stores/toast.store';
import styles from './PinList.module.css';

interface PinListProps {
  conversationId: string;
}

export default function PinList(props: PinListProps) {
  const pinStore = usePins();
  const toasts = useToasts();

  createEffect(() => {
    pinStore.loadPins(props.conversationId);
  });

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div data-testid="pin-list-panel" class={styles.panel}>
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Pinned Messages</h2>
      </div>

      <div class={styles.body}>
        <Show when={pinStore.isLoading}>
          <div class={styles.centeredStatus}>
            <p class={styles.mutedText}>Loading pins...</p>
          </div>
        </Show>

        <Show when={!pinStore.isLoading && pinStore.pinnedMessages.length === 0}>
          <div data-testid="pin-list-empty" class={styles.centeredStatus}>
            <p class={styles.mutedText}>No pinned messages</p>
          </div>
        </Show>

        <For each={pinStore.pinnedMessages}>
          {(message) => (
            <div data-testid="pin-list-item" class={styles.item}>
              <div class={styles.itemRow}>
                <div class={styles.avatar}>
                  {message.authorUsername?.charAt(0).toUpperCase() || 'U'}
                </div>

                <div class={styles.itemContent}>
                  <div class={styles.itemMeta}>
                    <div class={styles.itemAuthorRow}>
                      <span class={styles.authorName}>
                        {message.authorUsername || 'Unknown User'}
                      </span>
                      <span class={styles.timestamp}>{formatTime(message.createdAt)}</span>
                    </div>
                    <button
                      data-testid="pin-list-unpin-button"
                      class={styles.unpinButton}
                      onClick={() => {
                        // Unpinning needs ManageMessages. Without this the
                        // refusal was swallowed and the row simply stayed put,
                        // which reads as a button that does nothing.
                        void pinStore.unpinMessage(props.conversationId, message.id)
                          .catch(() => toasts.error('You do not have permission to unpin messages.'));
                      }}
                      aria-label="Unpin message"
                    >
                      Unpin
                    </button>
                  </div>

                  <p class={styles.messageContent}>{message.content}</p>
                </div>
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
