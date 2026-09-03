import type { DeckResponse, DeckConversationDto } from '../../api/deck';
import type { DeckTab } from '../../stores/deck.store';
import type { SwitchboardEntry } from './Switchboard';
import type { UnreadCard, AmbientCard } from './HomeTab';

/**
 * Pure projections from the deck aggregate into what each surface renders.
 * Kept out of the components so the shape of the feed is testable without
 * mounting anything.
 */

const DM_GROUP = 'Direct messages';

export function toTab(dto: DeckConversationDto, serverId?: string): DeckTab {
  return {
    id: dto.id,
    kind: serverId ? 'channel' : 'dm',
    name: dto.name,
    serverId,
    conversationId: dto.conversationId,
  };
}

/** Every conversation the user can reach, grouped for the switchboard. */
export function toSwitchboardEntries(deck: DeckResponse): SwitchboardEntry[] {
  const entries: SwitchboardEntry[] = [];
  for (const community of deck.communities) {
    for (const conv of community.conversations) {
      entries.push({
        tab: toTab(conv, community.id),
        group: community.name,
        kind: conv.kind,
        unread: conv.unread,
      });
    }
  }
  for (const dm of deck.directMessages) {
    entries.push({ tab: toTab(dm), group: DM_GROUP, unread: dm.unread });
  }
  return entries;
}

/** Flat list of every conversation, used as the ghost-tab candidate pool. */
export function toGhostCandidates(deck: DeckResponse): DeckTab[] {
  return toSwitchboardEntries(deck).map((e) => e.tab);
}

/** Mentions and unread, busiest first. */
export function toUnreadCards(deck: DeckResponse, limit = 8): UnreadCard[] {
  const cards: UnreadCard[] = [];
  for (const community of deck.communities) {
    for (const conv of community.conversations) {
      if (conv.unread > 0) {
        cards.push({ tab: toTab(conv, community.id), community: community.name, unread: conv.unread });
      }
    }
  }
  for (const dm of deck.directMessages) {
    if (dm.unread > 0) {
      cards.push({ tab: toTab(dm), community: DM_GROUP, unread: dm.unread });
    }
  }
  // Mentions matter more than volume, so sort by unread but float DMs and
  // anything with a mention to the top by weighting them.
  return cards.sort((a, b) => b.unread - a.unread).slice(0, limit);
}

/**
 * Ambient activity: things the user did not subscribe to but should see.
 * Currently live voice rooms, which is what the aggregate can prove right now.
 */
export function toAmbientCards(deck: DeckResponse, limit = 6): AmbientCard[] {
  const cards: AmbientCard[] = [];
  for (const community of deck.communities) {
    for (const conv of community.conversations) {
      if (conv.liveVoiceCount > 0) {
        cards.push({
          tab: toTab(conv, community.id),
          community: community.name,
          label: 'live now',
          detail: `${conv.liveVoiceCount} ${conv.liveVoiceCount === 1 ? 'person is' : 'people are'} in voice`,
          kind: 'live',
        });
      }
    }
  }
  return cards.slice(0, limit);
}

/** True when any voice room in the map has someone in it. */
export function hasLiveVoice(deck: DeckResponse): boolean {
  return deck.communities.some((c) => c.conversations.some((conv) => conv.liveVoiceCount > 0));
}

/**
 * Owner-set community accent. Until accents are a stored community field, a
 * community's identity still needs to be visible on its tabs, so this derives
 * a stable hue from the community id: the same community always gets the same
 * dot, and two communities rarely collide.
 */
export function accentForServer(serverId: string | undefined): string | undefined {
  if (!serverId) return undefined;
  let hash = 0;
  for (let i = 0; i < serverId.length; i++) {
    hash = (hash * 31 + serverId.charCodeAt(i)) >>> 0;
  }
  const hue = hash % 360;
  return `hsl(${hue} 45% 58%)`;
}
