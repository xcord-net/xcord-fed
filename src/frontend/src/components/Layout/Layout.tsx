import { Show, createMemo, createEffect, createSignal, onCleanup, onMount } from 'solid-js';
import { useParams } from '@solidjs/router';
import ChannelDirectory from '../ChannelDirectory';
import GroupManager from '../GroupManager';
import HubHeader from '../HubHeader';
import Modal from '../ui/Modal';
import Deck from '../Deck/Deck';
import Sidebar from '../Sidebar';
// Note: `ServerSettings` and the rest of the community menu's modals are
// rendered by `Sidebar`, which the Deck hosts as its community bar.
import { useChannels } from '../../stores/channel.store';
import { useDms } from '../../stores/dm.store';
import { useModals } from '../../stores/modal.store';
import { useServers } from '../../stores/server.store';
import { useAuth } from '../../stores/auth.store';
import { api } from '../../api/client';
import { useSignalR } from '../../stores/signalr.store';
import ChannelHeader from './ChannelHeader';
import DmView from './DmView';
import MessagesArea from './MessagesArea';
import RightPanels from './RightPanels';
import { useLayoutWiring } from './useLayoutWiring';
import Flexbox from '../ui/Flexbox';
import { MenuIcon } from '../ui/icons';
import styles from './Layout.module.css';
import EmptyState from '../ui/EmptyState';
import { Hash } from 'lucide-solid';

export default function Layout() {
  const params = useParams<{ serverId?: string; channelId?: string }>();
  const serverStore = useServers();
  const channelStore = useChannels();
  const signalR = useSignalR();
  const modals = useModals();
  const dmStore = useDms();

  const authStore = useAuth();
  const [version, setVersion] = createSignal<string | null>(null);

  const { hubUrl } = useLayoutWiring();

  // Version badge in the account slot. A failure here is cosmetic, so it stays
  // silent rather than surfacing an error over the shell.
  onMount(async () => {
    try {
      const data = await api.get<{ currentVersion: string }>('/api/v1/admin/system/version');
      setVersion(data.currentVersion);
    } catch {
      setVersion(null);
    }
  });

  // Mobile off-canvas nav: reflect open state on <body> so the global .sidebar
  // mobile rules in index.css can slide the drawer in/out. Auto-close whenever
  // the selected channel changes so picking a channel reveals the chat.
  createEffect(() => {
    document.body.classList.toggle('mobile-nav-open', modals.mobileNavOpen);
  });
  onCleanup(() => document.body.classList.remove('mobile-nav-open'));
  createEffect(() => {
    // Track channelId; closing when already closed is a no-op.
    void params.channelId;
    modals.closeMobileNav();
  });

  const isDmView = createMemo(() => params.serverId === 'me');

  const currentChannel = () =>
    channelStore.channels.find((c) => c.id === channelStore.selectedChannelId);

  const conversationId = () => currentChannel()?.conversationId;

  /** True when the current channel's conversation has been successfully joined on the SignalR hub. */
  const conversationJoined = () => {
    const convId = conversationId();
    if (!convId) return false;
    return signalR.currentConversations.has(convId);
  };

  /** Get conversation ID for the selected DM channel (when in DM view). */
  const dmConversationId = createMemo(() => {
    if (!isDmView() || !params.channelId) return undefined;
    const dm = dmStore.dmChannels.find((d) => d.id === params.channelId);
    return dm?.conversationId;
  });

  return (
    <Flexbox direction="vertical" class={styles.root} data-signalr-connected={String(signalR.isConnected)} data-signalr-conversation-joined={String(conversationJoined())}>
      <Show when={hubUrl()}>
        <HubHeader hubUrl={hubUrl()!} instanceUrl={window.location.origin} />
      </Show>
      {/* Mobile-only hamburger: opens the nav drawer. Hidden on desktop via CSS. */}
      <button
        type="button"
        data-testid="mobile-nav-toggle"
        class={styles.mobileNavToggle}
        aria-label="Open navigation"
        aria-expanded={modals.mobileNavOpen}
        onClick={() => modals.toggleMobileNav()}
      >
        <MenuIcon size={20} />
      </button>

      {/* Backdrop behind the open mobile drawer; tap to close. */}
      <Show when={modals.mobileNavOpen}>
        <div
          data-testid="mobile-nav-backdrop"
          class={styles.mobileNavBackdrop}
          aria-hidden="true"
          onClick={() => modals.closeMobileNav()}
        />
      </Show>

      <Flexbox class={styles.mainRow}>
        {/* The Deck: one tab strip, one content pane. The strip replaces the
            server rail and channel sidebar; everything the sidebar used to
            own lives in the strip's account slot or the ⌘K switchboard. */}
        <Deck
          version={version()}
          onLogout={() => { modals.closeAll(); authStore.logout(); }}
          communityBar={<Sidebar />}
        >
        {/* DM view, channel directory, or channel view */}
        <Show when={isDmView()}>
          <DmView channelId={params.channelId} dmConversationId={dmConversationId()} />
        </Show>
        <Show when={!isDmView()}>
          <Show
            when={params.channelId}
            fallback={
              <Show when={params.serverId} fallback={
                <Flexbox align="center" justify="center" class={styles.loadingState}>
                  <p class={styles.loadingStateText}>Loading...</p>
                </Flexbox>
              }>
                <ChannelDirectory serverId={params.serverId!} />
              </Show>
            }
          >
            {/* Channel selected: show messages */}
            <Flexbox direction="vertical" class={styles.channelView}>
              <Show
                when={conversationId()}
                fallback={
                  <EmptyState
                    icon={Hash}
                    title="No room open"
                    body="Pick a room from the strip above, or press Cmd+K to jump to one."
                    data-testid="layout-no-channel"
                  />
                }
              >
                {(convId) => (
                  <>
                    <ChannelHeader
                      channelName={currentChannel()?.name}
                      channelTopic={currentChannel()?.topic}
                    />

                    {/* Messages + right panels row */}
                    <Flexbox class={styles.contentRow}>
                      <MessagesArea
                        conversationId={convId()}
                        serverId={params.serverId}
                        channelId={params.channelId}
                      />
                      <RightPanels
                        conversationId={conversationId()}
                        channelId={params.channelId}
                      />
                    </Flexbox>
                  </>
                )}
              </Show>
            </Flexbox>
          </Show>
        </Show>
        </Deck>
      </Flexbox>{/* end mainRow */}

      {/* Account and channel settings are Deck tabs; see Deck.tsx. */}

      {/* Group Manager modal */}
      <Modal data-testid="group-manager-modal" open={modals.showGroupManager && !!serverStore.selectedServerId} onClose={() => modals.closeGroupManager()} aria-label="Group Manager" size="xl">
        <div class={styles.groupManagerContent}>
          <GroupManager serverId={serverStore.selectedServerId!} />
        </div>
      </Modal>

      {/* Server Settings modal is rendered by Sidebar */}
    </Flexbox>
  );
}
