import { For, Show } from 'solid-js';
import { Settings } from 'lucide-solid';
import WaveformMark from './WaveformMark';
import { Icon } from '../ui/Icon';
import { HOME_TAB_ID, type DeckTab } from '../../stores/deck.store';
import styles from './Strip.module.css';

export interface StripProps {
  pinned: DeckTab[];
  /** Session-only tabs (settings surfaces), sitting after the pinned set. */
  ephemeral?: DeckTab[];
  ghosts: DeckTab[];
  activeTabId: string;
  /** Aggregate unread across everything, shown on Home. */
  homeUnread: number;
  /** Per-tab unread, looked up by conversation id. */
  unreadFor: (tab: DeckTab) => number;
  /** Owner-set community accent for a tab's colour dot. */
  accentFor: (tab: DeckTab) => string | undefined;
  /** True while a voice room this tab represents has live audio. */
  liveVoice?: boolean;
  onSelect: (tab: DeckTab) => void;
  onSelectHome: () => void;
  onClose: (tab: DeckTab) => void;
  onPromoteGhost: (tab: DeckTab) => void;
  onOpenSwitchboard: () => void;
  /** Opening a tab's community: the colour dot is the affordance. */
  onOpenCommunity?: (tab: DeckTab) => void;
  /** Account slot; the Deck hands the existing user panel in here. */
  accountSlot?: unknown;
}

/**
 * One strip, one content pane. No server rail, no channel sidebar.
 *
 * Left to right: Home (waveform, aggregate badge), pinned tabs, ghost tabs,
 * the `+` that opens the switchboard, then a spacer, the shortcut hint and the
 * account slot.
 */
export default function Strip(props: StripProps) {
  const isActive = (id: string) => props.activeTabId === id;

  return (
    <div class={styles.strip} data-testid="deck-strip" role="tablist" aria-label="Open conversations">
      <button
        type="button"
        data-testid="deck-tab-home"
        role="tab"
        aria-selected={isActive(HOME_TAB_ID)}
        class={`${styles.tab} ${styles.home} ${isActive(HOME_TAB_ID) ? styles.active : ''}`}
        onClick={() => props.onSelectHome()}
        title="Home"
        aria-label="Home"
      >
        <WaveformMark live={props.liveVoice} />
        <Show when={props.homeUnread > 0}>
          <span class={styles.badge} data-testid="deck-home-unread">
            {props.homeUnread > 99 ? '99+' : props.homeUnread}
          </span>
        </Show>
      </button>

      <For each={props.pinned}>
        {(tab) => (
          <div
            class={`${styles.tab} ${isActive(tab.id) ? styles.active : ''}`}
            data-testid="deck-tab"
            data-tab-id={tab.id}
            role="tab"
            aria-selected={isActive(tab.id)}
          >
            <button
              type="button"
              class={styles.dotButton}
              data-testid="deck-tab-community"
              aria-label={`Open ${tab.name}'s community`}
              onClick={() => props.onOpenCommunity?.(tab)}
            >
              <span
                class={styles.dot}
                style={{ 'background-color': props.accentFor(tab) ?? 'var(--color-xcord-text-faint)' }}
              />
            </button>
            <button
              type="button"
              data-testid="deck-tab-select"
              class={styles.tabLabel}
              onClick={() => props.onSelect(tab)}
              title={tab.name}
            >
              <span class={styles.tabName}>{tab.name}</span>
              <Show when={props.unreadFor(tab) > 0}>
                <span class={styles.badge} data-testid="channel-unread-badge">
                  {props.unreadFor(tab)}
                </span>
              </Show>
            </button>
            <button
              type="button"
              class={styles.close}
              data-testid="deck-tab-close"
              aria-label={`Close ${tab.name}`}
              onClick={() => props.onClose(tab)}
            >
              ×
            </button>
          </div>
        )}
      </For>

      {/* Settings tabs. They carry a gear instead of a community dot and are
          not pinnable: they are where you went, not part of the working set. */}
      <For each={props.ephemeral ?? []}>
        {(tab) => (
          <div
            class={`${styles.tab} ${styles.utility} ${isActive(tab.id) ? styles.active : ''}`}
            data-testid="deck-utility-tab"
            data-tab-id={tab.id}
            role="tab"
            aria-selected={isActive(tab.id)}
          >
            <button
              type="button"
              data-testid="deck-tab-select"
              class={styles.tabLabel}
              onClick={() => props.onSelect(tab)}
              title={tab.name}
            >
              <Icon icon={Settings} size={13} class={styles.utilityIcon} />
              <span class={styles.tabName}>{tab.name}</span>
            </button>
            <button
              type="button"
              class={styles.close}
              data-testid="deck-tab-close"
              aria-label={`Close ${tab.name}`}
              onClick={() => props.onClose(tab)}
            >
              ×
            </button>
          </div>
        )}
      </For>

      {/* Ghosts: dashed and italic, so a tab you did not choose never looks
          like one you did. Clicking commits to it. */}
      <For each={props.ghosts}>
        {(tab) => (
          <button
            type="button"
            class={`${styles.tab} ${styles.ghost}`}
            data-testid="deck-ghost-tab"
            data-tab-id={tab.id}
            role="tab"
            aria-selected="false"
            onClick={() => props.onPromoteGhost(tab)}
            title={`${tab.name} (unpinned, surfaced by activity)`}
          >
            <span
              class={styles.dot}
              style={{ 'background-color': props.accentFor(tab) ?? 'var(--color-xcord-text-faint)' }}
              aria-hidden="true"
            />
            <span class={styles.tabName}>{tab.name}</span>
            <Show when={props.unreadFor(tab) > 0}>
              <span class={`${styles.badge} ${styles.ghostBadge}`} data-testid="channel-unread-badge">
                {props.unreadFor(tab)}
              </span>
            </Show>
          </button>
        )}
      </For>

      <button
        type="button"
        class={`${styles.tab} ${styles.add}`}
        data-testid="deck-add-tab"
        aria-label="Open switchboard"
        title="Open switchboard"
        onClick={() => props.onOpenSwitchboard()}
      >
        +
      </button>

      <div class={styles.spacer}>
        <button
          type="button"
          class={styles.kbd}
          data-testid="deck-switchboard-hint"
          onClick={() => props.onOpenSwitchboard()}
          aria-label="Open switchboard"
        >
          ⌘K
        </button>
        {props.accountSlot as never}
      </div>
    </div>
  );
}
