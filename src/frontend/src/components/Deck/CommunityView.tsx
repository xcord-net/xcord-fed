import { For, Show } from 'solid-js';
import type { DeckTab } from '../../stores/deck.store';
import styles from './CommunityView.module.css';

export interface CommunityRoom {
  tab: DeckTab;
  /** Text | Voice | Forum */
  kind: string;
  unread: number;
  liveVoiceCount: number;
  /** Set when the room is restricted to a membership group. */
  gatedBy?: string;
}

export interface CommunityPerson {
  userId: string;
  name: string;
  avatarUrl?: string;
  /** Owner-picked emblem for the member's tier, if they hold one. */
  tierEmblem?: string;
  tierName?: string;
}

export interface CommunityTier {
  id: string;
  name: string;
  description?: string | null;
  /** Cents. */
  priceMonthly: number;
  currency: string;
  /** True when this is the pass the viewer currently holds. */
  isCurrent?: boolean;
}

export interface CommunityViewProps {
  name: string;
  domain?: string;
  accent?: string;
  memberCount: number;
  voiceCount: number;
  rooms: CommunityRoom[];
  people: CommunityPerson[];
  peopleTotal: number;
  tiers: CommunityTier[];
  canManage: boolean;
  isPinned: (id: string) => boolean;
  onOpenRoom: (tab: DeckTab) => void;
  onTogglePin: (tab: DeckTab) => void;
  onManage: () => void;
  onSubscribe: (tierId: string) => void;
}

export function formatPrice(cents: number, currency: string): string {
  return `$${(cents / 100).toFixed(2)} ${currency.toUpperCase()}/mo`;
}

/** "128 members · 4 in voice", omitting a zero rather than printing it. */
export function formatCounts(memberCount: number, voiceCount: number): string {
  const parts = [`${memberCount} ${memberCount === 1 ? 'member' : 'members'}`];
  if (voiceCount > 0) parts.push(`${voiceCount} in voice`);
  return parts.join(' · ');
}

/**
 * The community view: what a tab's colour dot, a community name, or the
 * switchboard opens. It is the one place that answers "what is this community
 * and what is in it" without a sidebar to browse.
 */
export default function CommunityView(props: CommunityViewProps) {
  return (
    <div class={styles.view} data-testid="community-view">
      <header class={styles.masthead}>
        <div
          class={styles.mark}
          style={{ 'background-color': props.accent ?? 'var(--color-xcord-bg-floating)' }}
          aria-hidden="true"
        >
          {props.name.charAt(0).toUpperCase()}
        </div>
        <div class={styles.mastheadBody}>
          <h1 class={styles.name} data-testid="community-name">{props.name}</h1>
          <div class={styles.meta}>
            <Show when={props.domain}>
              <span class={styles.domain} data-testid="community-domain">{props.domain}</span>
            </Show>
            <span data-testid="community-counts">
              {formatCounts(props.memberCount, props.voiceCount)}
            </span>
          </div>
        </div>
        <Show when={props.canManage}>
          <button
            type="button"
            class={styles.manage}
            data-testid="community-manage"
            onClick={() => props.onManage()}
          >
            Manage ›
          </button>
        </Show>
      </header>

      <section class={styles.section}>
        <h2 class={styles.sectionLabel}>ROOMS</h2>
        <Show
          when={props.rooms.length > 0}
          fallback={<p class={styles.empty} data-testid="community-rooms-empty">No rooms yet.</p>}
        >
          <ul class={styles.rooms}>
            <For each={props.rooms}>
              {(room) => (
                <li class={styles.room} data-testid="community-room" data-room-id={room.tab.id}>
                  <button
                    type="button"
                    class={styles.roomOpen}
                    data-testid="community-room-open"
                    onClick={() => props.onOpenRoom(room.tab)}
                  >
                    <span class={styles.roomName}>{room.tab.name}</span>
                    <span class={styles.roomKind}>{room.kind}</span>
                    <Show when={room.gatedBy}>
                      <span class={styles.gate} data-testid="community-room-gate">
                        {room.gatedBy}
                      </span>
                    </Show>
                    <Show when={room.liveVoiceCount > 0}>
                      <span class={styles.live} data-testid="community-room-live">
                        live · {room.liveVoiceCount}
                      </span>
                    </Show>
                    <Show when={room.unread > 0}>
                      <span class={styles.unread}>{room.unread}</span>
                    </Show>
                  </button>
                  <button
                    type="button"
                    class={styles.pin}
                    data-testid="community-room-pin"
                    aria-pressed={props.isPinned(room.tab.id)}
                    aria-label={
                      props.isPinned(room.tab.id)
                        ? `Unpin ${room.tab.name}`
                        : `Pin ${room.tab.name}`
                    }
                    onClick={() => props.onTogglePin(room.tab)}
                  >
                    {props.isPinned(room.tab.id) ? 'Pinned' : 'Pin'}
                  </button>
                </li>
              )}
            </For>
          </ul>
        </Show>
      </section>

      <section class={styles.section}>
        <h2 class={styles.sectionLabel}>PEOPLE</h2>
        <div class={styles.people}>
          <For each={props.people}>
            {(person) => (
              <div class={styles.person} data-testid="community-person">
                <div class={styles.avatar} aria-hidden="true">
                  <Show when={person.avatarUrl} fallback={person.name.charAt(0).toUpperCase()}>
                    <img src={person.avatarUrl} alt="" class={styles.avatarImg} />
                  </Show>
                </div>
                <span class={styles.personName}>{person.name}</span>
                {/* Tier name lives in the tooltip; the badge itself stays a glyph
                    so a member list does not turn into a price list. */}
                <Show when={person.tierEmblem}>
                  <span
                    class={styles.emblem}
                    data-testid="community-person-emblem"
                    title={person.tierName}
                  >
                    {person.tierEmblem}
                  </span>
                </Show>
              </div>
            )}
          </For>
          <Show when={props.peopleTotal > props.people.length}>
            <span class={styles.morePeople} data-testid="community-people-more">
              +{props.peopleTotal - props.people.length} more
            </span>
          </Show>
        </div>
      </section>

      <Show when={props.tiers.length > 0}>
        <section class={styles.section}>
          <h2 class={styles.sectionLabel}>MEMBERSHIP</h2>
          <div class={styles.tiers}>
            <For each={props.tiers}>
              {(tier) => (
                <article
                  class={`${styles.tier} ${tier.isCurrent ? styles.tierCurrent : ''}`}
                  data-testid="community-tier"
                  data-tier-id={tier.id}
                >
                  <div class={styles.tierHead}>
                    <span class={styles.tierName}>{tier.name}</span>
                    <span class={styles.tierPrice}>
                      {formatPrice(tier.priceMonthly, tier.currency)}
                    </span>
                  </div>
                  <Show when={tier.description}>
                    <p class={styles.tierBody}>{tier.description}</p>
                  </Show>
                  <Show
                    when={!tier.isCurrent}
                    fallback={
                      <span class={styles.tierCurrentLabel} data-testid="community-tier-current">
                        Your pass
                      </span>
                    }
                  >
                    <button
                      type="button"
                      class={styles.tierCta}
                      data-testid="community-tier-subscribe"
                      onClick={() => props.onSubscribe(tier.id)}
                    >
                      Join this tier
                    </button>
                  </Show>
                </article>
              )}
            </For>
          </div>
        </section>
      </Show>
    </div>
  );
}
