import { For, Show, createEffect } from 'solid-js';
import { usePins } from '../stores/pin.store';

interface PinListProps {
  conversationId: string;
}

export default function PinList(props: PinListProps) {
  const pinStore = usePins();

  createEffect(() => {
    pinStore.loadPins(props.conversationId);
  });

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary border-l border-xcord-border w-80">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Pinned Messages</h2>
      </div>

      <div class="flex-1 overflow-y-auto">
        <Show when={pinStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading pins...</p>
          </div>
        </Show>

        <Show when={!pinStore.isLoading && pinStore.pinnedMessages.length === 0}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">No pinned messages</p>
          </div>
        </Show>

        <For each={pinStore.pinnedMessages}>
          {(message) => (
            <div class="px-4 py-3 border-b border-xcord-border hover:bg-xcord-bg-primary/30">
              <div class="flex items-start space-x-3">
                <div class="w-8 h-8 rounded-full bg-xcord-brand flex items-center justify-center text-white text-sm font-semibold flex-shrink-0">
                  {message.authorUsername?.charAt(0).toUpperCase() || 'U'}
                </div>

                <div class="flex-1 min-w-0">
                  <div class="flex items-baseline justify-between">
                    <div class="flex items-baseline space-x-2">
                      <span class="font-semibold text-white text-sm">
                        {message.authorUsername || 'Unknown User'}
                      </span>
                      <span class="text-xs text-xcord-text-muted">{formatTime(message.createdAt)}</span>
                    </div>
                    <button
                      class="text-xcord-text-muted hover:text-white text-xs"
                      onClick={() => pinStore.unpinMessage(props.conversationId, message.id)}
                    >
                      Unpin
                    </button>
                  </div>

                  <p class="text-sm text-xcord-text-primary mt-1 break-words">{message.content}</p>
                </div>
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
