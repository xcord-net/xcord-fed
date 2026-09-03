import { createSignal, createRoot, untrack } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Message, MessageAttachment, MessageReaction } from '../types/message';

/** The message a new message will reply to, shared between the action bar and the composer. */
export interface ReplyTarget {
  id: string;
  authorUsername: string;
  content: string;
}

function normalizeMessage(m: Message): Message {
  const base = normalizeIds(m, 'id', 'conversationId');
  return {
    ...base,
    authorId: m.authorId ? String(m.authorId) : '',
    // Guard against null/undefined content from incomplete SignalR payloads
    // (system messages dispatched by backend handlers like BanMemberHandler).
    content: base.content ?? '',
    replyToId: m.replyToId ? String(m.replyToId) : undefined,
    replyTo: m.replyTo ? normalizeIds(m.replyTo, 'id') : undefined,
    pollId: m.pollId ? String(m.pollId) : undefined,
  };
}

/**
 * Merges an update into the message already in the store, keeping any field the
 * incoming payload leaves undefined.
 *
 * Update payloads are not all equally complete: pin and unpin broadcast a message
 * without its attachments, reactions or poll, and an edit may arrive without the
 * reply reference. Replacing wholesale would drop whichever of those the sender
 * happened not to include. An explicit empty array still overwrites, so genuinely
 * clearing a collection works.
 */
function mergeMessage(existing: Message, incoming: Message): Message {
  const defined = Object.fromEntries(
    Object.entries(incoming).filter(([, value]) => value !== undefined),
  );
  return { ...existing, ...defined };
}

const store = createRoot(() => {
  const [messages, setMessages] = createSignal<Message[]>([]);
  const [hasMore, setHasMore] = createSignal(true);
  const [nextCursor, setNextCursor] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [pendingNonces, setPendingNonces] = createSignal<Map<string, string>>(new Map());
  const [editingMessageId, setEditingMessageId] = createSignal<string | null>(null);
  const [editContent, setEditContent] = createSignal('');
  const [replyTarget, setReplyTarget] = createSignal<ReplyTarget | null>(null);

  return {
    messages, setMessages,
    hasMore, setHasMore,
    nextCursor, setNextCursor,
    isLoading, setIsLoading,
    pendingNonces, setPendingNonces,
    editingMessageId, setEditingMessageId,
    editContent, setEditContent,
    replyTarget, setReplyTarget,
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
    get replyTarget() { return store.replyTarget(); },

    startReply(target: ReplyTarget): void {
      store.setReplyTarget(target);
    },

    cancelReply(): void {
      store.setReplyTarget(null);
    },

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
      // Anything already on screen when the request goes out is history the
      // response will carry itself. What is *not* here yet, and arrives while
      // the request is in flight, is the interesting case - see below.
      //
      // Untracked deliberately: this runs synchronously inside the effect that
      // calls loadMessages, so a plain read would make the message list a
      // dependency of its own fetch - every setMessages below would re-enter
      // here, which is an accelerating request loop, not a subscription.
      const idsBeforeFetch = untrack(() => new Set(store.messages().map((m) => m.id)));
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
          // Initial load. Two things must survive it.
          //
          // Optimistic messages (id starts with "pending-"), so an in-flight send
          // is not wiped by the fetch returning before the POST response.
          //
          // And messages someone else pushed while this request was in flight.
          // Opening a channel is a subscribe and a fetch racing each other, and
          // this replaced the list wholesale - so a message posted in that window
          // was announced, applied, and then thrown away, invisible until the
          // next reload. Whoever opened a channel at the moment someone spoke
          // simply did not see them.
          const current = store.messages();
          const fetchedIds = new Set(newMessages.map((m) => m.id));
          const pending = current.filter((m) => m.id.startsWith('pending-'));
          const arrivedDuringFetch = current.filter(
            (m) =>
              !m.id.startsWith('pending-') &&
              !idsBeforeFetch.has(m.id) &&
              !fetchedIds.has(m.id) &&
              m.conversationId === conversationId,
          );
          store.setMessages([...newMessages, ...arrivedDuringFetch, ...pending]);
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
      store.setMessages(store.messages().map((m) =>
        m.id === normalized.id ? mergeMessage(m, normalized) : m,
      ));
    },

    /**
     * Replace one message's reactions, from a realtime event.
     *
     * Deliberately not routed through `updateMessage`: that path normalizes a
     * whole message, and normalization turns a missing author or body into an
     * empty string rather than leaving it undefined - so merging a
     * reactions-only payload through it silently blanked the message it was
     * decorating. This touches the one field the event actually carries.
     */
    /**
     * Replace one message's attachments, from a realtime event.
     *
     * Thumbnails are generated after the message is delivered, so this is how a
     * download link becomes a picture without a reload. Same reasoning as
     * `setReactions`: only the field the event carries is touched.
     */
    setAttachments(messageId: string, attachments: MessageAttachment[]): void {
      store.setMessages(store.messages().map((m) =>
        m.id === messageId ? { ...m, attachments } : m,
      ));
    },

    setReactions(messageId: string, reactions: MessageReaction[]): void {
      store.setMessages(store.messages().map((m) =>
        m.id === messageId ? { ...m, reactions } : m,
      ));
    },

    /**
     * Re-fetches a single message and patches it in place. Used after reaction
     * changes so the view updates without clearing+reloading the whole list,
     * which would reset the user's scroll position mid-read.
     */
    async refreshMessage(conversationId: string, messageId: string): Promise<void> {
      const fresh = await api.get<Message>(
        `/api/v1/conversations/${conversationId}/messages/${messageId}`,
      );
      const normalized = normalizeMessage(fresh);
      // Merged, not replaced. This says "patch in place", and the single-message
      // endpoint is a narrower view than the list one - so replacing wholesale
      // dropped whatever it does not carry, which meant reacting to a message
      // with an attachment made the attachment disappear from your own screen.
      store.setMessages(store.messages().map((m) =>
        m.id === normalized.id ? mergeMessage(m, normalized) : m,
      ));
    },

    /**
     * Remove a message, and tell anything quoting it that it has gone.
     *
     * A reply carries its own copy of what it answered, so deleting the parent
     * used to leave the quote showing text that is no longer anywhere - the
     * placeholder only appeared after a reload, which is when the server next
     * described the reply. The reply itself stays: that is the point of the
     * placeholder.
     */
    removeMessage(messageId: string): void {
      store.setMessages(store.messages()
        .filter((m) => m.id !== messageId)
        .map((m) => (m.replyToId === messageId && m.replyTo && !m.replyTo.isDeleted
          ? { ...m, replyTo: { ...m.replyTo, isDeleted: true, preview: '' } }
          : m)));
    },

    reset(): void {
      store.setMessages([]);
      store.setHasMore(true);
      store.setIsLoading(false);
      store.setPendingNonces(new Map());
      store.setEditingMessageId(null);
      store.setEditContent('');
      store.setReplyTarget(null);
    },
  };
}
