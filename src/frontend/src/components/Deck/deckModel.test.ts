import { describe, it, expect } from 'vitest';
import {
  toSwitchboardEntries,
  toGhostCandidates,
  toUnreadCards,
  toAmbientCards,
  hasLiveVoice,
  accentForServer,
  toTab,
} from './deckModel';
import type { DeckResponse } from '../../api/deck';

const conv = (over: Partial<DeckResponse['communities'][0]['conversations'][0]> = {}) => ({
  id: '1',
  conversationId: 'c1',
  name: 'general',
  kind: 'Text',
  unread: 0,
  mentions: 0,
  liveVoiceCount: 0,
  ...over,
});

const deck: DeckResponse = {
  communities: [
    {
      id: 's1',
      name: 'The Foundry',
      conversations: [
        conv({ id: '1', conversationId: 'c1', name: 'general' }),
        conv({ id: '2', conversationId: 'c2', name: 'forge-floor', kind: 'Voice', liveVoiceCount: 4 }),
        conv({ id: '3', conversationId: 'c3', name: 'builds', unread: 12 }),
      ],
    },
    {
      id: 's2',
      name: 'Night Shift',
      conversations: [conv({ id: '4', conversationId: 'c4', name: 'patch-notes', unread: 4 })],
    },
  ],
  directMessages: [conv({ id: '5', conversationId: 'c5', name: 'mara', kind: 'Dm', unread: 1 })],
  totalUnread: 17,
  totalMentions: 2,
};

describe('toTab', () => {
  it('marks a conversation with a server as a channel tab', () => {
    expect(toTab(conv(), 's1')).toMatchObject({ kind: 'channel', serverId: 's1' });
  });

  it('marks a conversation without a server as a DM tab', () => {
    expect(toTab(conv())).toMatchObject({ kind: 'dm', serverId: undefined });
  });
});

describe('toSwitchboardEntries', () => {
  it('includes every channel and DM', () => {
    expect(toSwitchboardEntries(deck)).toHaveLength(5);
  });

  it('groups channels under their community name', () => {
    const entry = toSwitchboardEntries(deck).find((e) => e.tab.name === 'builds');
    expect(entry?.group).toBe('The Foundry');
  });

  it('groups DMs separately', () => {
    const entry = toSwitchboardEntries(deck).find((e) => e.tab.name === 'mara');
    expect(entry?.group).toBe('Direct messages');
  });

  it('carries the conversation kind so a voice room is obvious before jumping', () => {
    const entry = toSwitchboardEntries(deck).find((e) => e.tab.name === 'forge-floor');
    expect(entry?.kind).toBe('Voice');
  });

  it('returns nothing for an empty deck rather than throwing', () => {
    expect(
      toSwitchboardEntries({ communities: [], directMessages: [], totalUnread: 0, totalMentions: 0 }),
    ).toEqual([]);
  });
});

describe('toGhostCandidates', () => {
  it('offers every conversation as a possible ghost', () => {
    expect(toGhostCandidates(deck).map((t) => t.name)).toContain('patch-notes');
  });
});

describe('toUnreadCards', () => {
  it('includes only conversations with unread', () => {
    expect(toUnreadCards(deck).map((c) => c.tab.name)).toEqual(['builds', 'patch-notes', 'mara']);
  });

  it('orders by unread count, busiest first', () => {
    expect(toUnreadCards(deck).map((c) => c.unread)).toEqual([12, 4, 1]);
  });

  it('labels a DM card with the direct messages group', () => {
    const dm = toUnreadCards(deck).find((c) => c.tab.name === 'mara');
    expect(dm?.community).toBe('Direct messages');
  });

  it('respects the limit', () => {
    expect(toUnreadCards(deck, 1)).toHaveLength(1);
  });

  it('is empty when everything is read', () => {
    expect(toUnreadCards({ ...deck, communities: [], directMessages: [] })).toEqual([]);
  });
});

describe('toAmbientCards', () => {
  it('surfaces live voice rooms', () => {
    const cards = toAmbientCards(deck);
    expect(cards).toHaveLength(1);
    expect(cards[0].tab.name).toBe('forge-floor');
    expect(cards[0].label).toBe('live now');
  });

  it('describes how many people are in there, with correct grammar', () => {
    expect(toAmbientCards(deck)[0].detail).toBe('4 people are in voice');
    const one = toAmbientCards({
      ...deck,
      communities: [
        { id: 's1', name: 'X', conversations: [conv({ liveVoiceCount: 1, name: 'v' })] },
      ],
    });
    expect(one[0].detail).toBe('1 person is in voice');
  });

  it('ignores empty voice rooms', () => {
    expect(toAmbientCards({ ...deck, communities: [{ id: 's', name: 'S', conversations: [conv()] }] })).toEqual([]);
  });
});

describe('hasLiveVoice', () => {
  it('is true when someone is in a room', () => {
    expect(hasLiveVoice(deck)).toBe(true);
  });

  it('is false when every room is empty, so the mark stays still', () => {
    expect(hasLiveVoice({ ...deck, communities: [] })).toBe(false);
  });
});

describe('accentForServer', () => {
  it('is stable for the same community', () => {
    expect(accentForServer('s1')).toBe(accentForServer('s1'));
  });

  it('differs between communities', () => {
    expect(accentForServer('s1')).not.toBe(accentForServer('s2'));
  });

  it('is undefined without a community, so DMs get the neutral dot', () => {
    expect(accentForServer(undefined)).toBeUndefined();
  });
});
