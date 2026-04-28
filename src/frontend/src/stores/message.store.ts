import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Message } from '../types/message';

function normalizeMessage(m: Message): Message {
  const base = normalizeIds(
    m as unknown as Record<string, unknown>,
    'id', 'conversationId',
  ) as unknown as Message;
  return {
    ...base,
    authorId: m.authorId ? String(m.authorId) : '',
    // Guard against null/undefined content from incomplete SignalR payloads
    // (system messages dispatched by backend handlers like BanMemberHandler).
    content: base.content ?? '',
    replyToId: m.replyToId ? String(m.replyToId) : undefined,
    pollId: m.pollId ? String(m.pollId) : undefined,
  };
}

const store = createRoot(() => {
  const [messages, setMessages] = createSignal<Message[]>([]);
  const [hasMore, setHasMore] = createSignal(true);
  const [nextCursor, setNextCursor] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [pendingNonces, setPendingNonces] = createSignal<Map<string, string>>(new Map());
  const [editingMessageId, setEditingMessageId] = createSignal<string | null>(null);
  const [editContent, setEditContent] = createSignal('');

  return {
    messages, setMessages,
    hasMore, setHasMore,
    nextCursor, setNextCursor,
    isLoading, setIsLoading,
    pendingNonces, setPendingNonces,
    editingMessageId, setEditingMessageId,
    editContent, setEditContent,
  };
});

function generateNonce(): string {
  return `${Date.now()}-${Math.random().toString(36).substring(2, 15)}`;
}

export function useMessages() {
  return {
    get messages() { return store.messages(); },
    get hasMore() { return store.hasMore(); },
    get nextCursor() { return store.nextCursor(); },
    get isLoading() { return store.isLoading(); },
    get editingMessageId() { return store.editingMessageId(); },
    get editContent() { return store.editContent(); },

    startEditing(messageId: string, content: string): void {
      store.setEditingMessageId(messageId);
      store.setEditContent(content);
    },

    cancelEditing(): void {
      store.setEditingMessageId(null);
      store.setEditContent('');
    },

    setEditContent(content: string): void {
      store.setEditContent(content);
    },

    async loadMessages(conversationId: string, cursor?: string): Promise<void> {
      store.setIsLoading(true);
      try {
        const query = cursor
          ? `?cursor=${encodeURIComponent(cursor)}&limit=50`
          : '?limit=50';
        const response = await api.get<{ messages: Message[]; nextCursor?: string | null }>(
          `/api/v1/conversations/${conversationId}/messages${query}`,
        );
        const newMessages = (response.messages ?? []).map(normalizeMessage);

        // The server omits nextCursor (null/undefined) when there is no further page
        const next = response.nextCursor ?? null;
        store.setNextCursor(next);
        store.setHasMore(next !== null);

        if (cursor) {
          // Prepend older messages
          store.setMessages([...newMessages, ...store.messages()]);
        } else {
          // Initial load. Preserve any optimistic messages currently in the store
          // (id starts with "pending-") so an in-flight send is not wiped by the
          // initial fetch returning before the POST response.
          const pending = store.messages().filter((m) => m.id.startsWith('pending-'));
          store.setMessages([...newMessages, ...pending]);
        }
      } finally {
        store.setIsLoading(false);
      }
    },

    async sendMessage(conversationId: string, content: string, replyToId?: string, attachmentIds?: string[]): Promise<void> {
      const nonce = generateNonce();
      const optimisticMessage: Message = {
        id: `pending-${nonce}`,
        conversationId,
        authorId: 'me', // Will be replaced with real author data
        type: 'Default',
        content: content || '[attachment]',
        replyToId,
        isPinned: false,
        createdAt: new Date().toISOString(),
        nonce,
      };

      // Track pending nonce
      const nonces = new Map(store.pendingNonces());
      nonces.set(nonce, optimisticMessage.id);
      store.setPendingNonces(nonces);

      // Optimistically add message
      store.setMessages([...store.messages(), optimisticMessage]);

      try {
        const rawMessage = await api.post<Message>(`/api/v1/conversations/${conversationId}/messages`, {
          content,
          replyToId,
          attachmentIds,
          nonce,
        });
        const realMessage = normalizeMessage(rawMessage);

        // Replace optimistic message with real one (match by nonce if server echoed
        // it, otherwise by temp ID). SignalR may have delivered this same message
        // already, so dedupe by id afterward.
        const replaced = store.messages().map((m) => {
          if (m.nonce && m.nonce === nonce) return realMessage;
          if (m.id === optimisticMessage.id) return realMessage;
          return m;
        });
        const seen = new Set<string>();
        store.setMessages(replaced.filter((m) => {
          if (seen.has(m.id)) return false;
          seen.add(m.id);
          return true;
        }));

        // Remove from pending nonces
        const updatedNonces = new Map(store.pendingNonces());
        updatedNonces.delete(nonce);
        store.setPendingNonces(updatedNonces);
      } catch (error) {
        // Remove optimistic message on error
        store.setMessages(store.messages().filter((m) => m.id !== optimisticMessage.id));

        // Remove from pending nonces
        const updatedNonces = new Map(store.pendingNonces());
        updatedNonces.delete(nonce);
        store.setPendingNonces(updatedNonces);

        throw error;
      }
    },

    async editMessage(conversationId: string, messageId: string, content: string): Promise<void> {
      const raw = await api.patch<Message>(
        `/api/v1/conversations/${conversationId}/messages/${messageId}`,
        { content }
      );
      const response = normalizeMessage(raw);

      store.setMessages(store.messages().map((m) => (m.id === messageId ? response : m)));
      store.setEditingMessageId(null);
      store.setEditContent('');
    },

    async deleteMessage(conversationId: string, messageId: string): Promise<void> {
      await api.delete(`/api/v1/conversations/${conversationId}/messages/${messageId}`);
      store.setMessages(store.messages().filter((m) => m.id !== messageId));
    },

    clearMessages(): void {
      store.setMessages([]);
      store.setHasMore(true);
    },

    addMessage(message: Message): void {
      const normalized = normalizeMessage(message);
      const exists = store.messages().some(m => m.id === normalized.id);
      if (!exists) {
        store.setMessages([...store.messages(), normalized]);
      }
    },

    updateMessage(message: Message): void {
      const normalized = normalizeMessage(message);
      store.setMessages(store.messages().map((m) => (m.id === normalized.id ? normalized : m)));
    },

    removeMessage(messageId: string): void {
      store.setMessages(store.messages().filter((m) => m.id !== messageId));
    },

    reset(): void {
      store.setMessages([]);
      store.setHasMore(true);
      store.setIsLoading(false);
      store.setPendingNonces(new Map());
      store.setEditingMessageId(null);
      store.setEditContent('');
    },
  };
}
