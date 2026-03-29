import { For, Show, createMemo, createSignal, onMount } from 'solid-js';
import { useEmojis } from '../stores/emoji.store';
import { useModals } from '../stores/modal.store';
import { tooltip } from '../directives/tooltip';
import styles from './EmojiPicker.module.css';

// Ensure the directive is not tree-shaken
void tooltip;

interface EmojiPickerProps {
  onSelect: (emoji: string) => void;
  onClose?: () => void;
  serverId?: string;
  isAdmin?: boolean;
}

export default function EmojiPicker(props: EmojiPickerProps) {
  const emojiStore = useEmojis();
  const modals = useModals();
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
      class={styles.picker}
    >
      <div class={styles.header}>
        <h3 class={styles.headerTitle}>Emoji Picker</h3>
        <Show when={props.isAdmin && props.serverId}>
          <button
            data-testid="emoji-manage-link"
            class={styles.manageLink}
            onClick={() => { modals.openServerSettings('emoji'); props.onClose?.(); }}
          >
            Manage Emoji
          </button>
        </Show>
      </div>

      {/* Category bar */}
      <div class={styles.categoryBar}>
        <Show when={emojiStore.customEmojis.length > 0}>
          <button
            data-testid="emoji-category-custom"
            aria-label="Custom emoji"
            use:tooltip="Custom"
            class={`${styles.categoryBtn} ${selectedCategory() === -1 ? styles.categoryBtnActive : ''}`}
            onClick={() => changeCategory(-1)}
          >
            ⭐
          </button>
        </Show>

        <For each={emojiStore.unicodeCategories}>
          {(category, index) => (
            <button
              data-testid={`emoji-category-${index()}`}
              aria-label={`${category.name} emoji`}
              use:tooltip={category.name}
              class={`${styles.categoryBtn} ${selectedCategory() === index() ? styles.categoryBtnActive : ''}`}
              onClick={() => changeCategory(index())}
            >
              {category.icon}
            </button>
          )}
        </For>
      </div>

      {/* Emoji grid */}
      <div
        class={styles.gridArea}
        onKeyDown={handleGridKeyDown}
      >
        <Show when={selectedCategory() === -1}>
          <div class={styles.emojiGrid}>
            <For each={emojiStore.customEmojis}>
              {(emoji, index) => (
                <button
                  class={styles.emojiBtn}
                  onClick={() => props.onSelect(`:${emoji.name}:`)}
                  title={emoji.name}
                  aria-label={emoji.name}
                  tabIndex={focusedIndex() === index() ? 0 : -1}
                >
                  <img src={emoji.imageUrl} alt={emoji.name} class={styles.emojiImage} />
                </button>
              )}
            </For>
          </div>
        </Show>

        <Show when={selectedCategory() >= 0}>
          <div data-testid="emoji-grid" class={styles.emojiGrid}>
            <For each={emojiStore.unicodeCategories[selectedCategory()]?.emojis || []}>
              {(emoji, index) => (
                <button
                  data-testid={`emoji-btn-${index()}`}
                  class={styles.emojiBtn}
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
