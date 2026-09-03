import { createSignal, createRoot } from 'solid-js';

/**
 * The Deck: one tab strip, one content pane, no rails.
 *
 * Three kinds of tab live in the strip:
 *  - Home, permanent and leftmost, carrying the aggregate unread badge.
 *  - Pinned tabs, the user's curated working set. Persisted per user in
 *    localStorage (the spec puts this on the client on purpose - it is a
 *    per-device working set, not account state).
 *  - Ghost tabs, which are not stored at all. They are derived from unread
 *    activity on conversations the user has not pinned, capped so the strip
 *    cannot silt up, and they evaporate as soon as the unread clears.
 *
 * Deriving ghosts rather than storing them is what makes "ignore it and it
 * goes away" fall out for free: there is no ghost state to clean up.
 */

export type DeckTabKind = 'channel' | 'dm' | 'settings';

/** Which settings surface a settings tab shows. */
export type SettingsScope = 'user' | 'server' | 'channel';

export interface DeckTab {
  /** Channel id, DM channel id, or a settings tab's synthetic id. */
  id: string;
  kind: DeckTabKind;
  name: string;
  /** Owning community, for channel and server-settings tabs. Absent on DMs. */
  serverId?: string;
  /** Used to look up unread state, which is keyed by conversation. */
  conversationId?: string;
  /** Set on settings tabs only. */
  settingsScope?: SettingsScope;
  /** The channel a channel-settings tab configures. */
  channelId?: string;
}

/** Home is addressed by this sentinel rather than being a DeckTab. */
export const HOME_TAB_ID = 'home';

/** Ghosts are capped so high-traffic servers cannot flood the strip. */
export const MAX_GHOST_TABS = 2;

const STORAGE_PREFIX = 'xcord.deck.pinned';

function storageKey(userId: string | null): string {
  return userId ? `${STORAGE_PREFIX}.${userId}` : STORAGE_PREFIX;
}

/**
 * Reads the pinned set for a user. Any malformed payload is treated as "no
 * pins" rather than throwing: a corrupt localStorage entry must not be able to
 * stop the app shell from rendering.
 */
export function loadPinnedTabs(userId: string | null): DeckTab[] {
  try {
    const raw = localStorage.getItem(storageKey(userId));
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    // Settings tabs are session-only by design; one arriving from storage means
    // the entry is stale or tampered with, so it is dropped rather than trusted.
    return parsed.filter(isDeckTab).filter((t) => t.kind !== 'settings');
  } catch {
    return [];
  }
}

export function isDeckTab(value: unknown): value is DeckTab {
  if (typeof value !== 'object' || value === null) return false;
  const t = value as Partial<DeckTab>;
  return (
    typeof t.id === 'string' &&
    t.id.length > 0 &&
    (t.kind === 'channel' || t.kind === 'dm' || t.kind === 'settings') &&
    typeof t.name === 'string'
  );
}

/**
 * Settings tabs are addressed by what they configure, not by when they were
 * opened, so opening the same settings twice reuses the one tab instead of
 * stacking duplicates in the strip.
 */
export function settingsTabId(scope: SettingsScope, targetId?: string): string {
  return targetId ? `settings:${scope}:${targetId}` : `settings:${scope}`;
}

function persist(userId: string | null, tabs: DeckTab[]): void {
  try {
    localStorage.setItem(storageKey(userId), JSON.stringify(tabs));
  } catch {
    // A full or unavailable quota must not break navigation; the tabs still
    // work for this session, they just will not survive a reload.
  }
}

const store = createRoot(() => {
  const [pinned, setPinned] = createSignal<DeckTab[]>([]);
  // Tabs that exist for this session only: settings surfaces, which used to be
  // modals. They sit in the strip like anything else, so opening settings does
  // not cover the conversation you were reading.
  const [ephemeral, setEphemeral] = createSignal<DeckTab[]>([]);
  const [activeTabId, setActiveTabId] = createSignal<string>(HOME_TAB_ID);
  const [userId, setUserId] = createSignal<string | null>(null);
  return { pinned, setPinned, ephemeral, setEphemeral, activeTabId, setActiveTabId, userId, setUserId };
});

export function useDeck() {
  const write = (tabs: DeckTab[]) => {
    store.setPinned(tabs);
    persist(store.userId(), tabs);
  };

  return {
    get pinned() { return store.pinned(); },
    get ephemeral() { return store.ephemeral(); },
    get activeTabId() { return store.activeTabId(); },
    get isHomeActive() { return store.activeTabId() === HOME_TAB_ID; },

    /** The tab the pane should render, whichever list it came from. */
    activeTab(): DeckTab | undefined {
      const id = store.activeTabId();
      return store.ephemeral().find((t) => t.id === id)
        ?? store.pinned().find((t) => t.id === id);
    },

    /** Bind the deck to a user and load their pinned set. */
    hydrate(userId: string | null): void {
      store.setUserId(userId);
      store.setPinned(loadPinnedTabs(userId));
    },

    isPinned(id: string): boolean {
      return store.pinned().some((t) => t.id === id);
    },

    pin(tab: DeckTab): void {
      if (store.pinned().some((t) => t.id === tab.id)) return;
      write([...store.pinned(), tab]);
    },

    unpin(id: string): void {
      write(store.pinned().filter((t) => t.id !== id));
      // Closing the active tab falls back to Home rather than to a neighbour:
      // Home is the one surface that is never empty.
      if (store.activeTabId() === id) store.setActiveTabId(HOME_TAB_ID);
    },

    /** Move a pinned tab to a new index, clamped into range. */
    reorder(id: string, toIndex: number): void {
      const tabs = [...store.pinned()];
      const from = tabs.findIndex((t) => t.id === id);
      if (from === -1) return;
      const [moved] = tabs.splice(from, 1);
      const target = Math.max(0, Math.min(toIndex, tabs.length));
      tabs.splice(target, 0, moved);
      write(tabs);
    },

    setActive(id: string): void {
      store.setActiveTabId(id);
    },

    goHome(): void {
      store.setActiveTabId(HOME_TAB_ID);
    },

    /**
     * Opening a conversation makes it the active tab. It does not pin it -
     * pinning is an explicit act, which is what keeps the strip curated.
     */
    open(tab: DeckTab): void {
      store.setActiveTabId(tab.id);
    },

    /** A ghost becomes real when the user commits to it. */
    promoteGhost(tab: DeckTab): void {
      if (!store.pinned().some((t) => t.id === tab.id)) {
        write([...store.pinned(), tab]);
      }
      store.setActiveTabId(tab.id);
    },

    /**
     * Open a settings surface as a tab. Reopening the same one focuses the tab
     * that is already there, so the strip cannot fill up with duplicates.
     */
    openSettings(scope: SettingsScope, opts: {
      name: string;
      serverId?: string;
      channelId?: string;
    }): DeckTab {
      const targetId = scope === 'server' ? opts.serverId
        : scope === 'channel' ? opts.channelId
        : undefined;
      const id = settingsTabId(scope, targetId);
      const existing = store.ephemeral().find((t) => t.id === id);
      const tab: DeckTab = existing ?? {
        id,
        kind: 'settings',
        name: opts.name,
        settingsScope: scope,
        serverId: opts.serverId,
        channelId: opts.channelId,
      };
      if (!existing) store.setEphemeral([...store.ephemeral(), tab]);
      store.setActiveTabId(id);
      return tab;
    },

    /**
     * Close any tab by id, whichever list holds it. The strip's close control
     * should not have to know whether a tab is pinned or ephemeral.
     */
    close(id: string): void {
      if (store.ephemeral().some((t) => t.id === id)) {
        store.setEphemeral(store.ephemeral().filter((t) => t.id !== id));
        if (store.activeTabId() === id) store.setActiveTabId(HOME_TAB_ID);
        return;
      }
      this.unpin(id);
    },

    reset(): void {
      store.setPinned([]);
      store.setEphemeral([]);
      store.setActiveTabId(HOME_TAB_ID);
      store.setUserId(null);
    },
  };
}

/**
 * Ghost tabs for the current strip: unread conversations the user has not
 * pinned, most unread first, capped. The active tab is never ghosted - if you
 * are looking at it, it is not a notification.
 */
export function deriveGhostTabs(
  candidates: DeckTab[],
  unreadFor: (conversationId: string) => number,
  pinned: DeckTab[],
  activeTabId: string,
  cap: number = MAX_GHOST_TABS,
): DeckTab[] {
  const pinnedIds = new Set(pinned.map((t) => t.id));
  return candidates
    .filter((t) => !pinnedIds.has(t.id) && t.id !== activeTabId)
    .map((t) => ({ tab: t, unread: t.conversationId ? unreadFor(t.conversationId) : 0 }))
    .filter((entry) => entry.unread > 0)
    .sort((a, b) => b.unread - a.unread)
    .slice(0, cap)
    .map((entry) => entry.tab);
}
