import { For, Show, createSignal, onMount } from 'solid-js';
import { useEmojis } from '../stores/emoji.store';

interface EmojiPickerProps {
  onSelect: (emoji: string) => void;
  serverId?: string;
}

export default function EmojiPicker(props: EmojiPickerProps) {
  const emojiStore = useEmojis();
  const [selectedCategory, setSelectedCategory] = createSignal(0);

  onMount(() => {
    if (props.serverId) {
      emojiStore.loadCustomEmojis(props.serverId);
    }
  });

  return (
    <div class="w-80 h-96 bg-xcord-bg-secondary rounded-lg shadow-xl border border-xcord-border flex flex-col">
      <div class="px-4 py-2 border-b border-xcord-border">
        <h3 class="text-white font-semibold text-sm">Emoji Picker</h3>
      </div>

      <div class="flex border-b border-xcord-border overflow-x-auto">
        <Show when={emojiStore.customEmojis.length > 0}>
          <button
            class={`px-3 py-2 text-sm flex-shrink-0 ${
              selectedCategory() === -1
                ? 'text-white border-b-2 border-xcord-brand'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => setSelectedCategory(-1)}
          >
            Custom
          </button>
        </Show>

        <For each={emojiStore.unicodeCategories}>
          {(category, index) => (
            <button
              class={`px-3 py-2 text-sm flex-shrink-0 ${
                selectedCategory() === index()
                  ? 'text-white border-b-2 border-xcord-brand'
                  : 'text-xcord-text-muted hover:text-white'
              }`}
              onClick={() => setSelectedCategory(index())}
            >
              {category.name}
            </button>
          )}
        </For>
      </div>

      <div class="flex-1 overflow-y-auto p-3">
        <Show when={selectedCategory() === -1}>
          <div class="grid grid-cols-8 gap-2">
            <For each={emojiStore.customEmojis}>
              {(emoji) => (
                <button
                  class="w-8 h-8 hover:bg-xcord-bg-primary rounded flex items-center justify-center"
                  onClick={() => props.onSelect(`:${emoji.name}:`)}
                  title={emoji.name}
                >
                  <img src={emoji.imageUrl} alt={emoji.name} class="w-6 h-6" />
                </button>
              )}
            </For>
          </div>
        </Show>

        <Show when={selectedCategory() >= 0}>
          <div class="grid grid-cols-8 gap-2">
            <For each={emojiStore.unicodeCategories[selectedCategory()]?.emojis || []}>
              {(emoji) => (
                <button
                  class="w-8 h-8 hover:bg-xcord-bg-primary rounded flex items-center justify-center text-xl"
                  onClick={() => props.onSelect(emoji)}
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
