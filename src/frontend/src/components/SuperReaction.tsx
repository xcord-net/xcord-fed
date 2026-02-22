import { createSignal, For, Show } from 'solid-js';
import { api } from '../api/client';

export interface SuperReactionData {
  emoji: string;
  count: number;
  hasSuperReacted: boolean;
  animating?: boolean;
}

interface SuperReactionProps {
  conversationId: string;
  messageId: string;
  reactions: SuperReactionData[];
  isPremium?: boolean;
  onReactionsChange?: (reactions: SuperReactionData[]) => void;
}

export function buildSuperReactionPath(
  conversationId: string,
  messageId: string,
  emoji: string,
): string {
  return `/api/v1/conversations/${conversationId}/messages/${messageId}/reactions/${encodeURIComponent(emoji)}?super=true`;
}

export function updateReactionList(
  reactions: SuperReactionData[],
  emoji: string,
  hasSuperReacted: boolean,
): SuperReactionData[] {
  const existing = reactions.find((r) => r.emoji === emoji);
  if (existing) {
    return reactions.map((r) =>
      r.emoji === emoji
        ? {
            ...r,
            hasSuperReacted,
            count: hasSuperReacted ? r.count + 1 : Math.max(0, r.count - 1),
            animating: hasSuperReacted,
          }
        : r,
    );
  }
  if (hasSuperReacted) {
    return [...reactions, { emoji, count: 1, hasSuperReacted: true, animating: true }];
  }
  return reactions;
}

export default function SuperReaction(props: SuperReactionProps) {
  const [reactions, setReactions] = createSignal<SuperReactionData[]>(props.reactions ?? []);
  const [animatingEmojis, setAnimatingEmojis] = createSignal<Set<string>>(new Set());
  const [error, setError] = createSignal<string | null>(null);
  const [customEmoji, setCustomEmoji] = createSignal('');

  async function handleSuperReact(emoji: string) {
    if (!props.isPremium) {
      setError('Super Reactions are a premium feature. Upgrade to use them!');
      return;
    }

    const current = reactions().find((r) => r.emoji === emoji);
    const wasReacted = current?.hasSuperReacted ?? false;

    // Optimistic update with animation
    const updatedList = updateReactionList(reactions(), emoji, !wasReacted);
    setReactions(updatedList);

    if (!wasReacted) {
      const next = new Set(animatingEmojis());
      next.add(emoji);
      setAnimatingEmojis(next);
      setTimeout(() => {
        setAnimatingEmojis((prev) => {
          const s = new Set(prev);
          s.delete(emoji);
          return s;
        });
      }, 700);
    }

    try {
      if (!wasReacted) {
        await api.put(buildSuperReactionPath(props.conversationId, props.messageId, emoji));
      } else {
        await api.delete(
          `/api/v1/conversations/${props.conversationId}/messages/${props.messageId}/reactions/${encodeURIComponent(emoji)}`,
        );
      }
      props.onReactionsChange?.(reactions());
    } catch (err: unknown) {
      // Revert optimistic update on failure
      setReactions(props.reactions ?? []);
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to add super reaction');
    }
  }

  async function handleCustomEmoji(e: Event) {
    e.preventDefault();
    const emoji = customEmoji().trim();
    if (!emoji) return;
    await handleSuperReact(emoji);
    setCustomEmoji('');
  }

  return (
    <div class="flex flex-col space-y-3 p-3 bg-xcord-bg-secondary rounded-lg">
      {/* Premium badge */}
      <div class="flex items-center space-x-2">
        <span class="text-xs font-semibold text-yellow-400 bg-yellow-400/10 px-2 py-0.5 rounded-full">
          Super Reactions
        </span>
        <Show when={!props.isPremium}>
          <span class="text-xs text-xcord-text-muted">Premium only</span>
        </Show>
      </div>

      <Show when={error()}>
        <div class="px-3 py-2 bg-red-500/20 text-red-400 text-xs rounded">{error()}</div>
      </Show>

      {/* Reaction burst display */}
      <div class="flex flex-wrap gap-2">
        <For each={reactions()}>
          {(reaction) => (
            <button
              class={`relative flex items-center space-x-1 px-3 py-1.5 rounded-full border transition-all ${
                reaction.hasSuperReacted
                  ? 'bg-yellow-400/20 border-yellow-400/50 text-yellow-300'
                  : 'bg-xcord-bg-tertiary border-xcord-border text-xcord-text-muted hover:border-yellow-400/30 hover:text-yellow-300'
              } ${!props.isPremium ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}`}
              onClick={() => handleSuperReact(reaction.emoji)}
              title={reaction.hasSuperReacted ? 'Remove super reaction' : 'Add super reaction'}
            >
              {/* Burst animation overlay */}
              <Show when={animatingEmojis().has(reaction.emoji)}>
                <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
                  <div class="absolute w-8 h-8 rounded-full bg-yellow-400/40 animate-ping" />
                </div>
              </Show>
              <span
                class={`text-lg transition-transform ${
                  animatingEmojis().has(reaction.emoji) ? 'scale-125' : 'scale-100'
                }`}
              >
                {reaction.emoji}
              </span>
              <span class="text-xs font-semibold">{reaction.count}</span>
              <Show when={reaction.hasSuperReacted}>
                <span class="text-yellow-400 text-xs font-bold">*</span>
              </Show>
            </button>
          )}
        </For>
      </div>

      {/* Custom emoji input */}
      <Show when={props.isPremium}>
        <form onSubmit={handleCustomEmoji} class="flex items-center space-x-2">
          <input
            type="text"
            placeholder="Add emoji (e.g. )"
            value={customEmoji()}
            onInput={(e) => setCustomEmoji(e.currentTarget.value)}
            maxLength={2}
            class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-yellow-400 w-32"
          />
          <button
            type="submit"
            disabled={!customEmoji().trim()}
            class="bg-yellow-400 text-black px-3 py-1.5 rounded text-sm font-semibold hover:bg-yellow-300 disabled:opacity-50 transition-colors"
          >
            Burst!
          </button>
        </form>
      </Show>

      {/* Burst count summary */}
      <Show when={reactions().length > 0}>
        <p class="text-xcord-text-muted text-xs">
          {reactions().reduce((sum, r) => sum + r.count, 0)} total super reactions
        </p>
      </Show>
    </div>
  );
}
