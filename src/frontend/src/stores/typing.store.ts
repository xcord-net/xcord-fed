import { createSignal, createRoot } from 'solid-js';

const TYPING_TIMEOUT_MS = 8000; // 8 seconds

interface TypingTimeout {
  userId: string;
  timerId: number;
}

const store = createRoot(() => {
  const [typingMap, setTypingMap] = createSignal<Map<string, Set<string>>>(new Map());
  const [timeoutMap, setTimeoutMap] = createSignal<Map<string, TypingTimeout[]>>(new Map());

  return { typingMap, setTypingMap, timeoutMap, setTimeoutMap };
});

export function useTyping() {
  return {
    get typingMap() { return store.typingMap(); },

    startTyping(conversationId: string, userId: string): void {
      // Update typing set
      const map = new Map(store.typingMap());
      const users = map.get(conversationId) || new Set<string>();
      users.add(userId);
      map.set(conversationId, users);
      store.setTypingMap(map);

      // Clear any existing timeout for this user in this conversation
      const timeouts = store.timeoutMap().get(conversationId) || [];
      const existingTimeout = timeouts.find(t => t.userId === userId);
      if (existingTimeout) {
        clearTimeout(existingTimeout.timerId);
      }

      // Set new timeout to auto-remove
      const timerId = window.setTimeout(() => {
        this.stopTyping(conversationId, userId);
      }, TYPING_TIMEOUT_MS);

      // Store timeout reference
      const newTimeouts = timeouts.filter(t => t.userId !== userId);
      newTimeouts.push({ userId, timerId });
      const timeoutMapCopy = new Map(store.timeoutMap());
      timeoutMapCopy.set(conversationId, newTimeouts);
      store.setTimeoutMap(timeoutMapCopy);
    },

    stopTyping(conversationId: string, userId: string): void {
      // Update typing set
      const map = new Map(store.typingMap());
      const users = map.get(conversationId);
      if (users) {
        users.delete(userId);
        if (users.size === 0) {
          map.delete(conversationId);
        } else {
          map.set(conversationId, users);
        }
        store.setTypingMap(map);
      }

      // Clear timeout if exists
      const timeouts = store.timeoutMap().get(conversationId) || [];
      const timeout = timeouts.find(t => t.userId === userId);
      if (timeout) {
        clearTimeout(timeout.timerId);
        const newTimeouts = timeouts.filter(t => t.userId !== userId);
        const timeoutMapCopy = new Map(store.timeoutMap());
        if (newTimeouts.length === 0) {
          timeoutMapCopy.delete(conversationId);
        } else {
          timeoutMapCopy.set(conversationId, newTimeouts);
        }
        store.setTimeoutMap(timeoutMapCopy);
      }
    },

    getTypingUsers(conversationId: string): string[] {
      const users = store.typingMap().get(conversationId);
      return users ? Array.from(users) : [];
    },

    clearTyping(conversationId?: string): void {
      if (conversationId) {
        // Clear specific conversation
        const map = new Map(store.typingMap());
        map.delete(conversationId);
        store.setTypingMap(map);

        // Clear timeouts
        const timeouts = store.timeoutMap().get(conversationId) || [];
        timeouts.forEach(t => clearTimeout(t.timerId));
        const timeoutMapCopy = new Map(store.timeoutMap());
        timeoutMapCopy.delete(conversationId);
        store.setTimeoutMap(timeoutMapCopy);
      } else {
        // Clear all
        store.timeoutMap().forEach(timeouts => {
          timeouts.forEach(t => clearTimeout(t.timerId));
        });
        store.setTypingMap(new Map());
        store.setTimeoutMap(new Map());
      }
    },

    reset(): void {
      store.timeoutMap().forEach(timeouts => {
        timeouts.forEach(t => clearTimeout(t.timerId));
      });
      store.setTypingMap(new Map());
      store.setTimeoutMap(new Map());
    },
  };
}
