import { For, Show, createMemo, createSignal, onMount } from 'solid-js';
import { useEmojis } from '../stores/emoji.store';

interface EmojiPickerProps {
  onSelect: (emoji: string) => void;
  onClose?: () => void;
  serverId?: string;
}

export default function EmojiPicker(props: EmojiPickerProps) {
  const emojiStore = useEmojis();
  const [selectedCategory, setSelectedCategory] = createSignal(0);
  const [focusedIndex, setFocusedIndex] = createSignal(0);

  const COLS = 8;

  onMount(() => {
    if (props.serverId) {
      emojiStore.loadCustomEmojis(props.serverId);
    }
  });

  // Flat list of emojis for the current view, used for keyboard navigation.
  const currentEmojis = createMemo<string[]>(() => {
    if (selectedCategory() === -1) {
      return emojiStore.customEmojis.map((e) => `:${e.name}:`);
    }
    return emojiStore.unicodeCategories[selectedCategory()]?.emojis ?? [];
  });

  const changeCategory = (cat: number) => {
    setSelectedCategory(cat);
    setFocusedIndex(0);
  };

  const selectByIndex = (idx: number) => {
    if (selectedCategory() === -1) {
      const emoji = emojiStore.customEmojis[idx];
      if (emoji) props.onSelect(`:${emoji.name}:`);
    } else {
      const emoji = (emojiStore.unicodeCategories[selectedCategory()]?.emojis ?? [])[idx];
      if (emoji) props.onSelect(emoji);
    }
  };

  const handleGridKeyDown = (e: KeyboardEvent) => {
    const count = currentEmojis().length;
    if (count === 0) return;

    let idx = focusedIndex();

    switch (e.key) {
      case 'ArrowRight':
        e.preventDefault();
        idx = (idx + 1) % count;
        break;
      case 'ArrowLeft':
        e.preventDefault();
        idx = (idx - 1 + count) % count;
        break;
      case 'ArrowDown':
        e.preventDefault();
        idx = Math.min(idx + COLS, count - 1);
        break;
      case 'ArrowUp':
        e.preventDefault();
        idx = Math.max(idx - COLS, 0);
        break;
      case 'Enter':
      case ' ':
        e.preventDefault();
        selectByIndex(focusedIndex());
        return;
      case 'Escape':
        e.preventDefault();
        props.onClose?.();
        return;
      default:
        return;
    }

    setFocusedIndex(idx);
    const grid = e.currentTarget as HTMLElement;
    const buttons = grid.querySelectorAll<HTMLElement>('button');
    buttons[idx]?.focus();
  };

  return (
    <div
      data-testid="emoji-picker"
      role="dialog"
      aria-label="Emoji picker"
      class="w-80 h-96 bg-xcord-bg-secondary rounded-lg shadow-xl border border-xcord-border flex flex-col"
    >
      <div class="px-4 py-2 border-b border-xcord-border">
        <h3 class="text-white font-semibold text-sm">Emoji Picker</h3>
      </div>

      {/* Category bar */}
      <div class="flex border-b border-xcord-border overflow-x-auto">
        <Show when={emojiStore.customEmojis.length > 0}>
          <button
            data-testid="emoji-category-custom"
            aria-label="Custom emoji"
            class={`px-3 py-2 text-sm flex-shrink-0 ${
              selectedCategory() === -1
                ? 'text-white border-b-2 border-xcord-brand'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => changeCategory(-1)}
          >
            Custom
          </button>
        </Show>

        <For each={emojiStore.unicodeCategories}>
          {(category, index) => (
            <button
              data-testid={`emoji-category-${index()}`}
              aria-label={`${category.name} emoji`}
              class={`px-3 py-2 text-sm flex-shrink-0 ${
                selectedCategory() === index()
                  ? 'text-white border-b-2 border-xcord-brand'
                  : 'text-xcord-text-muted hover:text-white'
              }`}
              onClick={() => changeCategory(index())}
            >
              {category.name}
            </button>
          )}
        </For>
      </div>

      {/* Emoji grid */}
      <div
        class="flex-1 overflow-y-auto p-3"
        onKeyDown={handleGridKeyDown}
      >
        <Show when={selectedCategory() === -1}>
          <div class="grid grid-cols-8 gap-2">
            <For each={emojiStore.customEmojis}>
              {(emoji, index) => (
                <button
                  class="w-8 h-8 hover:bg-xcord-bg-primary focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none rounded flex items-center justify-center"
                  onClick={() => props.onSelect(`:${emoji.name}:`)}
                  title={emoji.name}
                  aria-label={emoji.name}
                  tabIndex={focusedIndex() === index() ? 0 : -1}
                >
                  <img src={emoji.imageUrl} alt={emoji.name} class="w-6 h-6" />
                </button>
              )}
            </For>
          </div>
        </Show>

        <Show when={selectedCategory() >= 0}>
          <div data-testid="emoji-grid" class="grid grid-cols-8 gap-2">
            <For each={emojiStore.unicodeCategories[selectedCategory()]?.emojis || []}>
              {(emoji, index) => (
                <button
                  data-testid={`emoji-btn-${index()}`}
                  class="w-8 h-8 hover:bg-xcord-bg-primary focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none rounded flex items-center justify-center text-xl"
                  onClick={() => props.onSelect(emoji)}
                  aria-label={emoji}
                  tabIndex={focusedIndex() === index() ? 0 : -1}
                >
                  {emoji}
                </button>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}
