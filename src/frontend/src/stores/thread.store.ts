import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import type { Thread } from '../types/thread';

/** Maps the backend ThreadSummary / CreateThreadResponse field names to the frontend Thread shape. */
function mapThread(raw: Record<string, unknown>): Thread {
  const r = normalizeIds(raw, 'id', 'conversationId', 'channelId');
  return {
    id: r.id as string,
    conversationId: r.conversationId as string,
    channelId: r.channelId as string,
    parentMessageId: raw.parentMessageId != null ? String(raw.parentMessageId) : undefined,
    name: (raw.title as string) ?? (raw.name as string) ?? '',
    archived: Boolean(raw.isArchived ?? raw.archived ?? false),
    locked: Boolean(raw.isLocked ?? raw.locked ?? false),
    messageCount: Number(raw.messageCount ?? 0),
    memberCount: Number(raw.memberCount ?? 0),
    createdAt: String(raw.createdAt),
  };
}

const store = createRoot(() => {
  const [threads, setThreads] = createSignal<Thread[]>([]);
  const [activeThreadId, setActiveThreadId] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    threads,
    setThreads,
    activeThreadId,
    setActiveThreadId,
    isLoading,
    setIsLoading,
  };
});

export function useThreads() {
  return {
    get threads() { return store.threads(); },
    get activeThreadId() { return store.activeThreadId(); },
    get isLoading() { return store.isLoading(); },

    async loadThreads(channelId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        // Backend returns { Threads: [...] } wrapped in ListThreadsResponse
        const response = await api.get<{ threads?: unknown[]; Threads?: unknown[] }>(
          `/api/v1/channels/${channelId}/threads?archived=false`
        );
        // Handle both camelCase and PascalCase wrapping
        const rawList = response.threads ?? response.Threads ?? [];
        store.setThreads((rawList as Record<string, unknown>[]).map(mapThread));
      } catch {
        store.setThreads([]);
      } finally {
        store.setIsLoading(false);
      }
    },

    async createThread(channelId: string, messageId: string, name: string): Promise<Thread> {
      // POST /api/v1/channels/{channelId}/threads with { ParentMessageId, Title }
      const raw = await api.post<Record<string, unknown>>(
        `/api/v1/channels/${channelId}/threads`,
        { parentMessageId: messageId, title: name }
      );
      const thread = mapThread(raw);
      store.setThreads([...store.threads(), thread]);
      return thread;
    },

    async joinThread(channelId: string, threadId: string): Promise<void> {
      await api.post(`/api/v1/channels/${channelId}/threads/${threadId}/members`, {});
      // Increment member count locally
      store.setThreads(store.threads().map((t) =>
        t.id === threadId ? { ...t, memberCount: t.memberCount + 1 } : t
      ));
    },

    async leaveThread(channelId: string, threadId: string): Promise<void> {
      await api.delete(`/api/v1/channels/${channelId}/threads/${threadId}/members/@me`);
      // Decrement member count locally
      store.setThreads(store.threads().map((t) =>
        t.id === threadId ? { ...t, memberCount: Math.max(0, t.memberCount - 1) } : t
      ));
    },

    async updateThreadName(channelId: string, threadId: string, name: string): Promise<void> {
      const raw = await api.patch<Record<string, unknown>>(
        `/api/v1/channels/${channelId}/threads/${threadId}`,
        { title: name }
      );
      const updated = mapThread(raw);
      store.setThreads(store.threads().map((t) => (t.id === threadId ? updated : t)));
    },

    async archiveThread(threadId: string): Promise<void> {
      const thread = store.threads().find((t) => t.id === threadId);
      if (!thread) return;
      const raw = await api.patch<Record<string, unknown>>(
        `/api/v1/channels/${thread.channelId}/threads/${threadId}`,
        { isArchived: true }
      );
      const updated = mapThread(raw);
      store.setThreads(store.threads().map((t) => (t.id === threadId ? updated : t)));
    },

    async lockThread(threadId: string): Promise<void> {
      const thread = store.threads().find((t) => t.id === threadId);
      if (!thread) return;
      const raw = await api.patch<Record<string, unknown>>(
        `/api/v1/channels/${thread.channelId}/threads/${threadId}`,
        { isLocked: true }
      );
      const updated = mapThread(raw);
      store.setThreads(store.threads().map((t) => (t.id === threadId ? updated : t)));
    },

    setActiveThread(threadId: string | null): void {
      store.setActiveThreadId(threadId);
    },

    clearThreads(): void {
      store.setThreads([]);
    },

    reset(): void {
      store.setThreads([]);
      store.setActiveThreadId(null);
      store.setIsLoading(false);
    },
  };
}
