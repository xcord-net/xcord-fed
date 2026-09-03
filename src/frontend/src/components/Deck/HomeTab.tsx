import { For, Show } from 'solid-js';
import type { DeckTab } from '../../stores/deck.store';
import styles from './HomeTab.module.css';

export interface UnreadCard {
  tab: DeckTab;
  community: string;
  unread: number;
}

export interface AmbientCard {
  tab: DeckTab;
  community: string;
  /** "live now", "new channel" - the reason this is on your Home. */
  label: string;
  detail: string;
  kind: 'live' | 'new';
}

export interface HomeTabProps {
  unreadCards: UnreadCard[];
  ambientCards: AmbientCard[];
  onOpen: (tab: DeckTab) => void;
  onPin: (tab: DeckTab) => void;
  onMarkRead: (tab: DeckTab) => void;
}

/** "3 conversations · 2 things happening in your communities" */
export function summarise(unreadCount: number, ambientCount: number): string {
  const parts: string[] = [];
  if (unreadCount > 0) {
    parts.push(`${unreadCount} ${unreadCount === 1 ? 'conversation' : 'conversations'}`);
  }
  if (ambientCount > 0) {
    parts.push(`${ambientCount} ${ambientCount === 1 ? 'thing' : 'things'} happening in your communities`);
  }
  return parts.join(' · ');
}

/**
 * Home: the catch-up surface. Mentions and unread first, then the ambient
 * things you did not subscribe to but should see.
 *
 * It is deliberately never blank. A brand new account with no unread still
 * gets its communities' activity here, which is what stops the Deck from
 * opening onto nothing.
 */
export default function HomeTab(props: HomeTabProps) {
  const isEmpty = () => props.unreadCards.length === 0 && props.ambientCards.length === 0;

  return (
    <div class={styles.home} data-testid="deck-home">
      <div class={styles.column}>
        <h1 class={styles.greeting}>While you were away</h1>
        <Show
          when={!isEmpty()}
          fallback={
            <p class={styles.subtitle} data-testid="deck-home-empty">
              Nothing new. Press ⌘K to jump to any channel, room or person.
            </p>
          }
        >
          <p class={styles.subtitle} data-testid="deck-home-summary">
            {summarise(props.unreadCards.length, props.ambientCards.length)}
          </p>
        </Show>

        <Show when={props.unreadCards.length > 0}>
          <section class={styles.group}>
            <h5 class={styles.groupLabel}>MENTIONS &amp; UNREAD</h5>
            <For each={props.unreadCards}>
              {(card) => (
                <article class={styles.card} data-testid="home-unread-card" data-tab-id={card.tab.id}>
                  <div class={styles.mark} aria-hidden="true">
                    {card.community.charAt(0).toUpperCase()}
                  </div>
                  <div class={styles.body}>
                    <div class={styles.row1}>
                      <span class={styles.name}>{card.tab.name}</span>
                      <span class={styles.community}>{card.community}</span>
                      <span class={styles.count}>{card.unread} new</span>
                    </div>
                    <div class={styles.actions}>
                      <button
                        type="button"
                        class={`${styles.action} ${styles.actionPrimary}`}
                        data-testid="home-card-open"
                        onClick={() => props.onOpen(card.tab)}
                      >
                        Open
                      </button>
                      <button
                        type="button"
                        class={styles.action}
                        data-testid="home-card-pin"
                        onClick={() => props.onPin(card.tab)}
                      >
                        Pin
                      </button>
                      <button
                        type="button"
                        class={styles.action}
                        data-testid="home-card-mark-read"
                        onClick={() => props.onMarkRead(card.tab)}
                      >
                        Mark read
                      </button>
                    </div>
                  </div>
                </article>
              )}
            </For>
          </section>
        </Show>

        <Show when={props.ambientCards.length > 0}>
          <section class={styles.group}>
            <h5 class={styles.groupLabel}>IN YOUR COMMUNITIES</h5>
            <For each={props.ambientCards}>
              {(card) => (
                <article
                  class={`${styles.card} ${styles.cardSlim}`}
                  data-testid="home-ambient-card"
                  data-tab-id={card.tab.id}
                >
                  <div class={styles.mark} aria-hidden="true">
                    {card.community.charAt(0).toUpperCase()}
                  </div>
                  <div class={styles.body}>
                    <div class={styles.row1}>
                      <button
                        type="button"
                        class={styles.nameButton}
                        data-testid="home-ambient-open"
                        onClick={() => props.onOpen(card.tab)}
                      >
                        {card.tab.name}
                      </button>
                      <span class={styles.community}>{card.community}</span>
                      <span
                        class={`${styles.pill} ${card.kind === 'live' ? styles.pillLive : styles.pillNew}`}
                      >
                        {card.label}
                      </span>
                    </div>
                    <div class={styles.detail}>{card.detail}</div>
                  </div>
                </article>
              )}
            </For>
          </section>
        </Show>
      </div>
    </div>
  );
}
