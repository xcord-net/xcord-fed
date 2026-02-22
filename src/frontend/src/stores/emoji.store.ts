import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { CustomEmoji, EmojiCategory } from '../types/emoji';

const store = createRoot(() => {
  const [customEmojis, setCustomEmojis] = createSignal<CustomEmoji[]>([]);
  const [unicodeCategories, setUnicodeCategories] = createSignal<EmojiCategory[]>([
    { name: 'Smileys & People', emojis: ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '🙂', '🙃', '😉', '😊', '😇'] },
    { name: 'Animals & Nature', emojis: ['🐶', '🐱', '🐭', '🐹', '🐰', '🦊', '🐻', '🐼', '🐨', '🐯', '🦁', '🐮', '🐷'] },
    { name: 'Food & Drink', emojis: ['🍏', '🍎', '🍐', '🍊', '🍋', '🍌', '🍉', '🍇', '🍓', '🍈', '🍒', '🍑', '🥭'] },
    { name: 'Activities', emojis: ['⚽', '🏀', '🏈', '⚾', '🥎', '🎾', '🏐', '🏉', '🥏', '🎱', '🏓', '🏸', '🏒'] },
    { name: 'Travel & Places', emojis: ['🚗', '🚕', '🚙', '🚌', '🚎', '🏎️', '🚓', '🚑', '🚒', '🚐', '🚚', '🚛', '🚜'] },
    { name: 'Objects', emojis: ['⌚', '📱', '📲', '💻', '⌨️', '🖥️', '🖨️', '🖱️', '🖲️', '🕹️', '🗜️', '💽', '💾'] },
    { name: 'Symbols', emojis: ['❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '❣️', '💕', '💞'] },
  ]);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    customEmojis,
    setCustomEmojis,
    unicodeCategories,
    setUnicodeCategories,
    isLoading,
    setIsLoading,
  };
});

export function useEmojis() {
  return {
    get customEmojis() { return store.customEmojis(); },
    get unicodeCategories() { return store.unicodeCategories(); },
    get isLoading() { return store.isLoading(); },

    async loadCustomEmojis(serverId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        const emojis = await api.get<CustomEmoji[]>(`/api/v1/servers/${serverId}/emojis`);
        store.setCustomEmojis(emojis);
      } finally {
        store.setIsLoading(false);
      }
    },

    async createEmoji(serverId: string, name: string, imageData: string): Promise<CustomEmoji> {
      const emoji = await api.post<CustomEmoji>(`/api/v1/servers/${serverId}/emojis`, {
        name,
        imageData,
      });
      store.setCustomEmojis([...store.customEmojis(), emoji]);
      return emoji;
    },

    async deleteEmoji(serverId: string, emojiId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/emojis/${emojiId}`);
      store.setCustomEmojis(store.customEmojis().filter((e) => e.id !== emojiId));
    },

    clearEmojis(): void {
      store.setCustomEmojis([]);
    },

    reset(): void {
      store.setCustomEmojis([]);
      store.setIsLoading(false);
    },
  };
}
