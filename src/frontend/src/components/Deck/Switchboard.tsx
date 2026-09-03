import { For, Show, createMemo, createSignal, onCleanup, onMount } from 'solid-js';
import type { DeckTab } from '../../stores/deck.store';
import styles from './Switchboard.module.css';

export interface SwitchboardEntry {
  tab: DeckTab;
  /** Community name, or a stable label like "Direct messages" for DMs. */
  group: string;
  /** Text/Voice/Forum, surfaced so a voice room is obvious before you jump. */
  kind?: string;
  unread?: number;
}

export interface SwitchboardProps {
  open: boolean;
  entries: SwitchboardEntry[];
  isPinned: (id: string) => boolean;
  onOpen: (tab: DeckTab) => void;
  onTogglePin: (tab: DeckTab) => void;
  onCreateServer: () => void;
  onClose: () => void;
}

/** Case-insensitive match on the conversation name or its community. */
export function filterEntries(entries: SwitchboardEntry[], query: string): SwitchboardEntry[] {
  const q = query.trim().toLowerCase();
  if (!q) return entries;
  return entries.filter(
    (e) => e.tab.name.toLowerCase().includes(q) || e.group.toLowerCase().includes(q),
  );
}

/** Preserves the order groups first appear in, so the list does not jump around. */
export function groupEntries(entries: SwitchboardEntry[]): [string, SwitchboardEntry[]][] {
  const groups = new Map<string, SwitchboardEntry[]>();
  for (const entry of entries) {
    const bucket = groups.get(entry.group);
    if (bucket) bucket.push(entry);
    else groups.set(entry.group, [entry]);
  }
  return [...groups.entries()];
}

/**
 * The switchboard is the only full map in the Deck. The strip stays curated
 * precisely because everything else is one keystroke away here.
 */
export default function Switchboard(props: SwitchboardProps) {
  const [query, setQuery] = createSignal('');
  const [cursor, setCursor] = createSignal(0);

  const results = createMemo(() => filterEntries(props.entries, query()));
  const grouped = createMemo(() => groupEntries(results()));

  // Keep the cursor inside the result set as the query narrows it.
  const clampedCursor = createMemo(() => {
    const max = results().length - 1;
    return max < 0 ? 0 : Math.min(cursor(), max);
  });

  const onKeyDown = (e: KeyboardEvent) => {
    if (!props.open) return;
    if (e.key === 'Escape') {
      e.preventDefault();
      props.onClose();
      return;
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setCursor((c) => Math.min(c + 1, Math.max(results().length - 1, 0)));
      return;
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      setCursor((c) => Math.max(c - 1, 0));
      return;
    }
    if (e.key === 'Enter') {
      e.preventDefault();
      const entry = results()[clampedCursor()];
      if (entry) {
        props.onOpen(entry.tab);
        props.onClose();
      }
    }
  };

  onMount(() => {
    document.addEventListener('keydown', onKeyDown);
    onCleanup(() => document.removeEventListener('keydown', onKeyDown));
  });

  // A flat index across groups so keyboard order matches visual order.
  let flatIndex = 0;

  return (
    <Show when={props.open}>
      <div
        class={styles.backdrop}
        data-testid="switchboard-backdrop"
        onClick={() => props.onClose()}
      />
      <div class={styles.panel} data-testid="switchboard" role="dialog" aria-label="Switchboard">
        <input
          type="text"
          class={styles.input}
          data-testid="switchboard-input"
          placeholder="Jump to a channel, room or person"
          autofocus
          value={query()}
          onInput={(e) => {
            setQuery(e.currentTarget.value);
            setCursor(0);
          }}
          aria-label="Search conversations"
        />

        <div class={styles.results}>
          {(flatIndex = 0)}
          <For each={grouped()}>
            {([group, entries]) => (
              <div class={styles.group}>
                <h5 class={styles.groupLabel}>{group}</h5>
                <For each={entries}>
                  {(entry) => {
                    const myIndex = flatIndex++;
                    return (
                      <div
                        class={`${styles.row} ${myIndex === clampedCursor() ? styles.rowActive : ''}`}
                        data-testid="switchboard-result"
                        data-tab-id={entry.tab.id}
                        data-tab-name={entry.tab.name}
                      >
                        <button
                          type="button"
                          data-testid="switchboard-result-open"
                          class={styles.rowMain}
                          onClick={() => {
                            props.onOpen(entry.tab);
                            props.onClose();
                          }}
                        >
                          <span class={styles.rowName}>{entry.tab.name}</span>
                          <Show when={entry.kind}>
                            <span class={styles.rowKind}>{entry.kind}</span>
                          </Show>
                          <Show when={(entry.unread ?? 0) > 0}>
                            <span data-testid="switchboard-result-unread" class={styles.rowUnread}>
                              {entry.unread}
                            </span>
                          </Show>
                        </button>
                        <button
                          type="button"
                          class={styles.pin}
                          data-testid="switchboard-pin"
                          aria-label={
                            props.isPinned(entry.tab.id)
                              ? `Unpin ${entry.tab.name}`
                              : `Pin ${entry.tab.name}`
                          }
                          aria-pressed={props.isPinned(entry.tab.id)}
                          onClick={() => props.onTogglePin(entry.tab)}
                        >
                          {props.isPinned(entry.tab.id) ? 'Pinned' : 'Pin'}
                        </button>
                      </div>
                    );
                  }}
                </For>
              </div>
            )}
          </For>

          <Show when={results().length === 0}>
            <p class={styles.empty} data-testid="switchboard-empty">
              Nothing matches "{query()}". Try a channel, room or person's name.
            </p>
          </Show>
        </div>

        <div class={styles.footer}>
          <button
            type="button"
            class={styles.createServer}
            data-testid="sidebar-create-server-button"
            onClick={() => {
              props.onCreateServer();
              props.onClose();
            }}
          >
            Create a community
          </button>
          <span class={styles.hint}>↑↓ to move · enter to jump · esc to close</span>
        </div>
      </div>
    </Show>
  );
}
