import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  useDeck,
  deriveGhostTabs,
  loadPinnedTabs,
  isDeckTab,
  settingsTabId,
  HOME_TAB_ID,
  MAX_GHOST_TABS,
  type DeckTab,
} from './deck.store';

const channel = (id: string, name = `chan-${id}`): DeckTab => ({
  id,
  kind: 'channel',
  name,
  serverId: 's1',
  conversationId: `conv-${id}`,
});

describe('deck.store', () => {
  beforeEach(() => {
    localStorage.clear();
    useDeck().reset();
  });

  describe('pinning', () => {
    it('starts on Home with nothing pinned', () => {
      const deck = useDeck();
      expect(deck.pinned).toEqual([]);
      expect(deck.activeTabId).toBe(HOME_TAB_ID);
      expect(deck.isHomeActive).toBe(true);
    });

    it('pins a tab and reports it as pinned', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      expect(deck.pinned).toHaveLength(1);
      expect(deck.isPinned('1')).toBe(true);
    });

    it('ignores a duplicate pin rather than showing the tab twice', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.pin(channel('1'));
      expect(deck.pinned).toHaveLength(1);
    });

    it('unpinning the active tab falls back to Home, never to a neighbour', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.pin(channel('2'));
      deck.setActive('2');
      deck.unpin('2');
      expect(deck.activeTabId).toBe(HOME_TAB_ID);
      expect(deck.pinned.map((t) => t.id)).toEqual(['1']);
    });

    it('unpinning an inactive tab leaves the active tab alone', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.pin(channel('2'));
      deck.setActive('1');
      deck.unpin('2');
      expect(deck.activeTabId).toBe('1');
    });
  });

  describe('settings tabs', () => {
    it('opens settings as a tab and makes it active', () => {
      const deck = useDeck();
      const tab = deck.openSettings('user', { name: 'Settings' });

      expect(tab.kind).toBe('settings');
      expect(tab.settingsScope).toBe('user');
      expect(deck.ephemeral).toHaveLength(1);
      expect(deck.activeTabId).toBe(tab.id);
    });

    it('keeps settings out of the pinned working set', () => {
      const deck = useDeck();
      deck.openSettings('user', { name: 'Settings' });
      expect(deck.pinned).toEqual([]);
    });

    it('reuses the tab when the same settings are opened again', () => {
      const deck = useDeck();
      deck.openSettings('server', { name: 'Foundry settings', serverId: 's1' });
      deck.goHome();
      deck.openSettings('server', { name: 'Foundry settings', serverId: 's1' });

      expect(deck.ephemeral).toHaveLength(1);
      expect(deck.activeTabId).toBe(settingsTabId('server', 's1'));
    });

    it('keeps a separate tab per community', () => {
      const deck = useDeck();
      deck.openSettings('server', { name: 'A settings', serverId: 's1' });
      deck.openSettings('server', { name: 'B settings', serverId: 's2' });
      expect(deck.ephemeral.map((t) => t.id)).toEqual([
        settingsTabId('server', 's1'),
        settingsTabId('server', 's2'),
      ]);
    });

    it('carries the channel a room-settings tab configures', () => {
      const deck = useDeck();
      const tab = deck.openSettings('channel', { name: '#general settings', serverId: 's1', channelId: 'c1' });
      expect(tab.serverId).toBe('s1');
      expect(tab.channelId).toBe('c1');
    });

    it('closes a settings tab and falls back to Home', () => {
      const deck = useDeck();
      const tab = deck.openSettings('user', { name: 'Settings' });
      deck.close(tab.id);

      expect(deck.ephemeral).toEqual([]);
      expect(deck.activeTabId).toBe(HOME_TAB_ID);
    });

    it('closes a pinned tab through the same call', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.setActive('1');
      deck.close('1');

      expect(deck.pinned).toEqual([]);
      expect(deck.activeTabId).toBe(HOME_TAB_ID);
    });

    it('leaves the active tab alone when closing a different one', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.setActive('1');
      const settings = deck.openSettings('user', { name: 'Settings' });
      deck.setActive('1');
      deck.close(settings.id);

      expect(deck.activeTabId).toBe('1');
    });

    it('resolves the active tab from either list', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.setActive('1');
      expect(deck.activeTab()?.kind).toBe('channel');

      const settings = deck.openSettings('user', { name: 'Settings' });
      expect(deck.activeTab()?.id).toBe(settings.id);
    });

    it('does not survive a reload - settings are session-only', () => {
      const deck = useDeck();
      deck.hydrate('u1');
      deck.openSettings('user', { name: 'Settings' });

      expect(loadPinnedTabs('u1')).toEqual([]);
    });

    // A tampered or stale storage entry must not resurrect a settings tab as
    // though the user had pinned it.
    it('drops a settings tab found in stored pins', () => {
      localStorage.setItem(
        'xcord.deck.pinned.u1',
        JSON.stringify([{ id: 'settings:user', kind: 'settings', name: 'Settings' }, channel('1')]),
      );
      expect(loadPinnedTabs('u1').map((t) => t.id)).toEqual(['1']);
    });
  });

  describe('reorder', () => {
    it('moves a tab to a new index', () => {
      const deck = useDeck();
      ['1', '2', '3'].forEach((id) => deck.pin(channel(id)));
      deck.reorder('3', 0);
      expect(deck.pinned.map((t) => t.id)).toEqual(['3', '1', '2']);
    });

    it('clamps an out-of-range index instead of dropping the tab', () => {
      const deck = useDeck();
      ['1', '2'].forEach((id) => deck.pin(channel(id)));
      deck.reorder('1', 99);
      expect(deck.pinned.map((t) => t.id)).toEqual(['2', '1']);
    });

    it('is a no-op for an unknown id', () => {
      const deck = useDeck();
      deck.pin(channel('1'));
      deck.reorder('nope', 0);
      expect(deck.pinned.map((t) => t.id)).toEqual(['1']);
    });
  });

  describe('opening vs pinning', () => {
    it('open activates without pinning, so the strip stays curated', () => {
      const deck = useDeck();
      deck.open(channel('9'));
      expect(deck.activeTabId).toBe('9');
      expect(deck.isPinned('9')).toBe(false);
    });

    it('promoting a ghost pins it and activates it', () => {
      const deck = useDeck();
      deck.promoteGhost(channel('9'));
      expect(deck.isPinned('9')).toBe(true);
      expect(deck.activeTabId).toBe('9');
    });

    it('promoting an already-pinned tab does not duplicate it', () => {
      const deck = useDeck();
      deck.pin(channel('9'));
      deck.promoteGhost(channel('9'));
      expect(deck.pinned).toHaveLength(1);
    });
  });

  describe('persistence', () => {
    it('round-trips the pinned set for a user', () => {
      const deck = useDeck();
      deck.hydrate('user-1');
      deck.pin(channel('1'));
      deck.pin(channel('2'));

      deck.reset();
      deck.hydrate('user-1');
      expect(deck.pinned.map((t) => t.id)).toEqual(['1', '2']);
    });

    it('keeps each user\'s pins separate', () => {
      const deck = useDeck();
      deck.hydrate('user-1');
      deck.pin(channel('1'));

      deck.hydrate('user-2');
      expect(deck.pinned).toEqual([]);

      deck.hydrate('user-1');
      expect(deck.pinned.map((t) => t.id)).toEqual(['1']);
    });

    it('treats corrupt storage as no pins rather than throwing', () => {
      localStorage.setItem('xcord.deck.pinned.user-1', '{ not json');
      expect(loadPinnedTabs('user-1')).toEqual([]);
    });

    it('drops entries that are not valid tabs', () => {
      localStorage.setItem(
        'xcord.deck.pinned.user-1',
        JSON.stringify([channel('1'), { id: '', kind: 'channel', name: 'x' }, { nope: true }]),
      );
      expect(loadPinnedTabs('user-1').map((t) => t.id)).toEqual(['1']);
    });

    it('survives localStorage throwing on write', () => {
      const deck = useDeck();
      deck.hydrate('user-1');
      const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('QuotaExceededError');
      });
      expect(() => deck.pin(channel('1'))).not.toThrow();
      // The tab still works for this session even though it was not persisted.
      expect(deck.isPinned('1')).toBe(true);
      spy.mockRestore();
    });
  });

  describe('isDeckTab', () => {
    it.each([
      [null, false],
      [undefined, false],
      ['string', false],
      [{ id: '1', kind: 'channel', name: 'a' }, true],
      [{ id: '1', kind: 'dm', name: 'a' }, true],
      [{ id: '1', kind: 'bogus', name: 'a' }, false],
      [{ id: '', kind: 'channel', name: 'a' }, false],
      [{ kind: 'channel', name: 'a' }, false],
    ])('validates %j as %s', (value, expected) => {
      expect(isDeckTab(value)).toBe(expected);
    });
  });

  describe('deriveGhostTabs', () => {
    const unread = (map: Record<string, number>) => (id: string) => map[id] ?? 0;

    it('surfaces unread, unpinned conversations', () => {
      const ghosts = deriveGhostTabs(
        [channel('1'), channel('2')],
        unread({ 'conv-2': 4 }),
        [],
        HOME_TAB_ID,
      );
      expect(ghosts.map((t) => t.id)).toEqual(['2']);
    });

    it('never ghosts a pinned conversation', () => {
      const ghosts = deriveGhostTabs(
        [channel('1')],
        unread({ 'conv-1': 9 }),
        [channel('1')],
        HOME_TAB_ID,
      );
      expect(ghosts).toEqual([]);
    });

    it('never ghosts the tab you are looking at', () => {
      const ghosts = deriveGhostTabs([channel('1')], unread({ 'conv-1': 9 }), [], '1');
      expect(ghosts).toEqual([]);
    });

    it('orders by unread count, busiest first', () => {
      const ghosts = deriveGhostTabs(
        [channel('1'), channel('2'), channel('3')],
        unread({ 'conv-1': 2, 'conv-2': 30, 'conv-3': 7 }),
        [],
        HOME_TAB_ID,
        3,
      );
      expect(ghosts.map((t) => t.id)).toEqual(['2', '3', '1']);
    });

    it('caps the strip so it cannot silt up', () => {
      const many = ['1', '2', '3', '4', '5'].map((id) => channel(id));
      const counts = Object.fromEntries(many.map((t, i) => [`conv-${t.id}`, i + 1]));
      const ghosts = deriveGhostTabs(many, unread(counts), [], HOME_TAB_ID);
      expect(ghosts).toHaveLength(MAX_GHOST_TABS);
    });

    it('evaporates a ghost as soon as its unread clears', () => {
      const candidates = [channel('1')];
      expect(deriveGhostTabs(candidates, unread({ 'conv-1': 3 }), [], HOME_TAB_ID)).toHaveLength(1);
      expect(deriveGhostTabs(candidates, unread({}), [], HOME_TAB_ID)).toHaveLength(0);
    });
  });
});
