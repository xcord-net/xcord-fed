import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { Message } from '../types/message';

const store = createRoot(() => {
  const [pinnedMessages, setPinnedMessages] = createSignal<Message[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    pinnedMessages,
    setPinnedMessages,
    isLoading,
    setIsLoading,
  };
});

export function usePins() {
  return {
    get pinnedMessages() { return store.pinnedMessages(); },
    get isLoading() { return store.isLoading(); },

    async loadPins(conversationId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        // Backend returns { messages: MessageDto[] }
        const response = await api.get<{ messages: Message[] }>(`/api/v1/conversations/${conversationId}/pins`);
        store.setPinnedMessages(response.messages ?? []);
      } finally {
        store.setIsLoading(false);
      }
    },

    async pinMessage(conversationId: string, messageId: string): Promise<void> {
      const pinned = await api.post<Message>(
        `/api/v1/conversations/${conversationId}/messages/${messageId}/pin`,
        {}
      );
      // Idempotent: a refetch of the list can land either side of this, and
      // appending blind then showed the same message pinned twice.
      if (!store.pinnedMessages().some((m) => m.id === pinned.id)) {
        store.setPinnedMessages([...store.pinnedMessages(), pinned]);
      }
    },

    async unpinMessage(conversationId: string, messageId: string): Promise<void> {
      await api.delete(`/api/v1/conversations/${conversationId}/messages/${messageId}/pin`);
      store.setPinnedMessages(store.pinnedMessages().filter((m) => m.id !== messageId));
    },

    clearPins(): void {
      store.setPinnedMessages([]);
    },

    reset(): void {
      store.setPinnedMessages([]);
      store.setIsLoading(false);
    },
  };
}
