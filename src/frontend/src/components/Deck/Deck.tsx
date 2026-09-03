import { Show, createEffect, createMemo, createResource, createSignal, onCleanup, onMount, type JSX } from 'solid-js';
import { useNavigate, useParams } from '@solidjs/router';
import Strip from './Strip';
import Switchboard from './Switchboard';
import HomeTab from './HomeTab';
import CommunityView from './CommunityView';
import SettingsModal from '../Layout/SettingsModal';
import WelcomePanel from '../Layout/WelcomePanel';
import ServerSettings from '../ServerSettings';
import ChannelSettings from '../ChannelSettings';
import UserStatusBar from '../Sidebar/UserStatusBar';
import VoicePanel from '../VoicePanel';
import {
  toSwitchboardEntries,
  toGhostCandidates,
  toUnreadCards,
  toAmbientCards,
  hasLiveVoice,
  accentForServer,
} from './deckModel';
import { fetchDeck, EMPTY_DECK, type DeckResponse } from '../../api/deck';
import {
  useDeck,
  deriveGhostTabs,
  HOME_TAB_ID,
  type DeckTab,
} from '../../stores/deck.store';
import { useChannels } from '../../stores/channel.store';
import { useServers } from '../../stores/server.store';
import { useAuth } from '../../stores/auth.store';
import { useProfiles } from '../../stores/profile.store';
import { useModals } from '../../stores/modal.store';
import { useUnread } from '../../stores/unread.store';
import { useMembers } from '../../stores/member.store';
import { useTiers } from '../../stores/tier.store';
import styles from './Deck.module.css';

export interface DeckProps {
  /** The content pane: whatever the active conversation renders. */
  children?: JSX.Element;
  /**
   * Community context: the masthead and every modal its menu opens. Rendered
   * regardless of which tab is active, because those modals (create community,
   * server settings) are reachable from the switchboard on Home too.
   */
  communityBar?: JSX.Element;
  /** Version string for the account panel. */
  version?: string | null;
  onLogout: () => void;
}

/**
 * The Deck shell: one strip, one content pane.
 *
 * Navigation is still URL-driven, so deep links and the back button keep
 * working exactly as they did with the sidebar; the strip just becomes the
 * thing that drives them.
 */
export default function Deck(props: DeckProps) {
  const navigate = useNavigate();
  const params = useParams<{ serverId?: string; channelId?: string }>();
  const deck = useDeck();
  const auth = useAuth();
  const profileStore = useProfiles();
  const modals = useModals();
  const unread = useUnread();
  const members = useMembers();
  const tiers = useTiers();
  const channels = useChannels();
  const serverStore = useServers();

  const [switchboardOpen, setSwitchboardOpen] = createSignal(false);
  // Which community's view is open, if any. Distinct from the active tab: the
  // community is a different destination from any conversation inside it.
  const [communityId, setCommunityId] = createSignal<string | null>(null);
  // A failed aggregate must not take the shell down with it: the strip, the
  // switchboard and Home all render fine against an empty map, and the user
  // can still navigate by URL while it retries on next mount.
  const [map, { refetch: refetchMap }] = createResource<DeckResponse>(
    () => fetchDeck().catch(() => EMPTY_DECK),
  );

  const data = () => map() ?? EMPTY_DECK;

  // The aggregate is the switchboard's whole world, and the switchboard is the
  // only way to reach a conversation that is not already a tab. Fetched once on
  // mount and never again, it went stale the moment anyone made a channel: you
  // could create one, press the shortcut, and not find it - with no way back
  // short of reloading the page. Refetching when the local channel or community
  // count moves keeps the map honest without polling.
  let lastCounts = '';
  createEffect(() => {
    const counts = `${channels.channels.length}:${serverStore.servers.length}`;
    const first = lastCounts === '';
    lastCounts = counts;
    // The first pass is the initial load, which the resource is already doing.
    if (!first) void refetchMap();
  });

  onMount(() => {
    deck.hydrate(auth.user?.id ?? null);

    const onKey = (e: KeyboardEvent) => {
      // ⌘K / Ctrl+K opens the only full map there is.
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        if (switchboardOpen()) setSwitchboardOpen(false);
        else openSwitchboard();
      }
    };
    document.addEventListener('keydown', onKey);
    onCleanup(() => document.removeEventListener('keydown', onKey));
  });

  // Navigation stays URL-driven, so the strip has to follow the route rather
  // than own it. Without this a deep link or a browser back would leave Home
  // selected while the pane showed a channel.
  let lastRouteChannelId: string | undefined;
  createEffect(() => {
    const channelId = params.channelId;
    const serverId = params.serverId;
    // Only the *route* moving may reselect a tab. Comparing against the active
    // tab instead - and so tracking it - made this fight every tab that is not
    // the routed conversation: opening settings from inside a channel selected
    // the settings tab, woke this effect, and was snapped straight back.
    if (channelId === lastRouteChannelId) return;
    lastRouteChannelId = channelId;

    if (channelId) {
      deck.setActive(channelId);
    } else if (serverId === 'me') {
      // `/channels/me` with no conversation is where login and "leave server"
      // land, and it names no conversation to show - so it is Home. Saying so
      // here keeps the strip and the pane in agreement; without it the tab you
      // were last reading stayed highlighted while Home was on screen, and the
      // pane's content depended on which route you arrived from.
      deck.goHome();
    }
  });

  // Members and tiers load only when a community view is actually opened;
  // the deck aggregate deliberately does not carry them.
  createEffect(() => {
    const id = communityId();
    if (!id) return;
    void members.fetchMembers(id).catch(() => undefined);
    void tiers.fetchTiers(id).catch(() => undefined);
  });

  const openCommunity = (serverId: string | undefined) => {
    if (serverId) setCommunityId(serverId);
  };

  /**
   * Live unread where we have it, the aggregate where we do not.
   *
   * The distinction that matters is "this store has a count" versus "this store
   * says zero" - treating zero as absent meant opening a channel cleared the
   * live count and immediately fell back to the aggregate fetched on page load,
   * so a read channel kept its badge until something else refetched the map.
   */
  const effectiveUnread = (conversationId: string | undefined, fallback: number): number => {
    if (!conversationId) return 0;
    if (unread.isTracked(conversationId)) return unread.getUnreadCount(conversationId);
    return fallback;
  };

  // Entries carry the live count, so every surface built from them - the
  // switchboard rows, the tabs, the ghosts, Home's total - agrees about what is
  // unread. The switchboard was reading the aggregate directly, so a channel you
  // had just read still showed its old count there.
  const entries = createMemo(() => toSwitchboardEntries(data()).map((e) => ({
    ...e,
    unread: effectiveUnread(e.tab.conversationId, e.unread ?? 0),
  })));

  /**
   * Open the switchboard, refetching the map first if it is empty.
   *
   * The aggregate is fetched once on mount and a failure is swallowed to an
   * empty map so the shell still renders - but nothing then retried, so a
   * request that lost a race with a starting server left the only complete map
   * of the app blank for the rest of the session. Asking again when someone
   * actually opens it costs nothing in the normal case, where it is not empty.
   */
  const openSwitchboard = () => {
    if (entries().length === 0) void refetchMap();
    setSwitchboardOpen(true);
  };

  const unreadFor = (tab: DeckTab): number => {
    const entry = entries().find((e) => e.tab.id === tab.id);
    return effectiveUnread(tab.conversationId, entry?.unread ?? 0);
  };

  const ghosts = createMemo(() =>
    deriveGhostTabs(
      toGhostCandidates(data()),
      (conversationId) => effectiveUnread(
        conversationId,
        entries().find((e) => e.tab.conversationId === conversationId)?.unread ?? 0,
      ),
      deck.pinned,
      deck.activeTabId,
    ),
  );

  // Summed the same way, one conversation at a time, so reading one channel
  // reduces the total instead of leaving the page-load figure standing.
  const homeUnread = createMemo(() =>
    entries().reduce((total, e) => total + effectiveUnread(e.tab.conversationId, e.unread ?? 0), 0),
  );

  /**
   * Settings open as tabs rather than dialogs: the strip stays visible, the
   * conversation underneath is not covered, and closing is the same gesture as
   * closing any other tab.
   */
  const openAccountSettings = () => {
    // The section is still modal-store state; the tab only decides presence.
    if (modals.showSettings === null) modals.openSettings('profile');
    deck.openSettings('user', { name: 'Settings' });
    setCommunityId(null);
  };

  const openServerSettings = (serverId: string, name?: string) => {
    deck.openSettings('server', { name: name ? `${name} settings` : 'Community settings', serverId });
    setCommunityId(null);
  };

  const openChannelSettings = (serverId: string, channelId: string, name?: string) => {
    deck.openSettings('channel', { name: name ? `#${name} settings` : 'Room settings', serverId, channelId });
    setCommunityId(null);
  };

  const closeTab = (tab: DeckTab) => {
    if (tab.settingsScope === 'user') modals.closeSettings();
    deck.close(tab.id);
    // Closing the active tab falls back to Home. When the URL still names a
    // conversation that is what the user was reading before they opened this
    // tab, so closing it should hand them back to it rather than to Home.
    if (deck.activeTabId === HOME_TAB_ID && params.channelId) {
      deck.setActive(params.channelId);
    }
  };

  // Every existing entry point - the community menu, a channel's gear, a
  // context menu - still asks the modal store to open settings. The Deck turns
  // those requests into tabs here rather than making each call site know about
  // the strip; the store flag is cleared immediately so nothing renders a
  // dialog behind the tab.
  createEffect(() => {
    if (!modals.showServerSettings) return;
    const serverId = serverStore.selectedServerId;
    if (!serverId) return;
    const name = serverStore.servers.find((sv) => sv.id === serverId)?.name;
    modals.closeServerSettings();
    openServerSettings(serverId, name);
  });

  createEffect(() => {
    if (!modals.showChannelSettings) return;
    const serverId = serverStore.selectedServerId;
    const channelId = channels.selectedChannelId;
    if (!serverId || !channelId) return;
    const name = channels.channels.find((c) => c.id === channelId)?.name;
    modals.closeChannelSettings();
    openChannelSettings(serverId, channelId, name);
  });

  /** The settings tab currently in the pane, if the active tab is one. */
  const activeSettings = createMemo(() => {
    const tab = deck.activeTab();
    return tab?.kind === 'settings' ? tab : undefined;
  });

  /** Route to a conversation and make it the active tab. */
  const openTab = (tab: DeckTab) => {
    // Opening a conversation leaves the community view behind.
    setCommunityId(null);
    deck.open(tab);
    // A settings tab has no conversation behind it, so selecting one changes
    // the pane without touching the URL - the route still points at whatever
    // conversation you were reading, and closing the tab returns you to it.
    if (tab.kind === 'settings') return;
    if (tab.kind === 'dm') navigate(`/channels/me/${tab.id}`);
    else if (tab.serverId) navigate(`/channels/${tab.serverId}/${tab.id}`);
  };

  return (
    <div class={styles.deck} data-testid="deck">
      <Strip
        pinned={deck.pinned}
        ephemeral={deck.ephemeral}
        ghosts={ghosts()}
        activeTabId={deck.activeTabId}
        homeUnread={homeUnread()}
        unreadFor={unreadFor}
        accentFor={(tab) => accentForServer(tab.serverId)}
        liveVoice={hasLiveVoice(data())}
        onSelect={openTab}
        onSelectHome={() => deck.goHome()}
        onClose={closeTab}
        onPromoteGhost={(tab) => {
          deck.promoteGhost(tab);
          openTab(tab);
        }}
        onOpenSwitchboard={openSwitchboard}
        onOpenCommunity={(tab) => openCommunity(tab.serverId)}
        accountSlot={
          <UserStatusBar
            profile={profileStore.userProfile}
            isAdmin={!!auth.user?.isAdmin}
            version={props.version ?? null}
            onOpenSettings={openAccountSettings}
            onLogout={props.onLogout}
          />
        }
      />

      {props.communityBar}

      <div class={styles.pane}>
        <Show when={activeSettings()} keyed>
          {(tab) => (
            <Show when={tab.settingsScope === 'user'} fallback={
              <Show when={tab.settingsScope === 'server'} fallback={
                <ChannelSettings
                  inline
                  serverId={tab.serverId!}
                  channelId={tab.channelId!}
                  onClose={() => closeTab(tab)}
                />
              }>
                <ServerSettings inline serverId={tab.serverId!} onClose={() => closeTab(tab)} />
              </Show>
            }>
              <SettingsModal inline />
            </Show>
          )}
        </Show>

        <Show when={!activeSettings() && communityId()} keyed>
          {(id) => {
            const community = () => data().communities.find((c) => c.id === id);
            return (
              <CommunityView
                name={community()?.name ?? 'Community'}
                accent={accentForServer(id)}
                memberCount={members.members.length}
                voiceCount={
                  community()?.conversations.reduce((n, c) => n + c.liveVoiceCount, 0) ?? 0
                }
                rooms={(community()?.conversations ?? []).map((c) => ({
                  tab: { id: c.id, kind: 'channel' as const, name: c.name, serverId: id, conversationId: c.conversationId },
                  kind: c.kind,
                  unread: c.unread,
                  liveVoiceCount: c.liveVoiceCount,
                }))}
                people={members.members.slice(0, 12).map((m) => ({
                  userId: m.userId,
                  name: m.nickname || m.displayName || m.username,
                  avatarUrl: m.avatarUrl,
                }))}
                peopleTotal={members.members.length}
                tiers={(tiers.tiersByServer[id] ?? [])
                  .filter((t) => t.isActive)
                  .map((t) => ({
                    id: t.id,
                    name: t.name,
                    description: t.description,
                    priceMonthly: t.priceMonthly,
                    currency: t.currency,
                  }))}
                canManage={true}
                isPinned={(tabId) => deck.isPinned(tabId)}
                onOpenRoom={openTab}
                onTogglePin={(tab) => (deck.isPinned(tab.id) ? deck.unpin(tab.id) : deck.pin(tab))}
                onManage={() => openServerSettings(id, community()?.name)}
                onSubscribe={() => openServerSettings(id, community()?.name)}
              />
            );
          }}
        </Show>
        <Show when={!activeSettings() && !communityId()}>
        <Show when={deck.activeTabId === HOME_TAB_ID} fallback={props.children}>
          {/* Someone with no communities yet needs the one thing Home cannot
              tell them: how to start. It sits above the catch-up feed rather
              than replacing it, because they may still have DMs. */}
          <Show when={serverStore.servers.length === 0}>
            <WelcomePanel />
          </Show>
          <HomeTab
            unreadCards={toUnreadCards(data())}
            ambientCards={toAmbientCards(data())}
            onOpen={openTab}
            onPin={(tab) => deck.pin(tab)}
            onMarkRead={(tab) => {
              if (tab.conversationId) unread.markRead(tab.conversationId);
            }}
          />
        </Show>
        </Show>
      </div>

      {/* Voice persists across tabs: joining a room must not be something you
          lose by navigating away from it. */}
      <div class={styles.voicePill}>
        <VoicePanel />
      </div>

      <Switchboard
        open={switchboardOpen()}
        entries={entries()}
        isPinned={(id) => deck.isPinned(id)}
        onOpen={openTab}
        onTogglePin={(tab) => (deck.isPinned(tab.id) ? deck.unpin(tab.id) : deck.pin(tab))}
        onCreateServer={() => modals.openCreateServer()}
        onClose={() => setSwitchboardOpen(false)}
      />
    </div>
  );
}
