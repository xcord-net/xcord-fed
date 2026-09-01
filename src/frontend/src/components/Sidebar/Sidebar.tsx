import { Show, createEffect, createMemo, createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../../stores/channel.store';
import { useServers } from '../../stores/server.store';
import { useUnread } from '../../stores/unread.store';
import { useVoice } from '../../stores/voice.store';
import { useAuth } from '../../stores/auth.store';
import { useProfiles } from '../../stores/profile.store';
import { useModals } from '../../stores/modal.store';
import { api } from '../../api/client';
import { Capability, hasCapability } from '../../types/channel';
import type { Channel } from '../../types/channel';
import InviteModal from '../InviteModal';
import ServerSettings from '../ServerSettings';
import VoicePanel from '../VoicePanel';
import Menu from '../ui/Menu';
import Modal from '../ui/Modal';
import CreateServerModal from '../CreateServerModal';
import MembershipPanel from '../MembershipPanel';
import ServerHeader from './ServerHeader';
import ChannelList from './ChannelList';
import UserStatusBar from './UserStatusBar';
import LeaveServerModal from './LeaveServerModal';
import CreateChannelModal from './CreateChannelModal';
import { makeChannelListKeyDown, toggleFavoriteChannel } from './helpers';
import Flexbox from '../ui/Flexbox';
import styles from './Sidebar.module.css';

export default function Sidebar() {
  const navigate = useNavigate();
  const channelStore = useChannels();
  const serverStore = useServers();
  const unreadStore = useUnread();
  const voiceStore = useVoice();
  const authStore = useAuth();
  const profileStore = useProfiles();
  const modals = useModals();

  // JS only used for "lock open" when interacting with forms/menus
  const [isLocked, setIsLocked] = createSignal(false);
  const [focusedChannelId, setFocusedChannelId] = createSignal<string | null>(null);
  const [showInviteModal, setShowInviteModal] = createSignal(false);
  const [showServerMenu, setShowServerMenu] = createSignal(false);
  const [showLeaveConfirm, setShowLeaveConfirm] = createSignal(false);
  const [showCreateChannel, setShowCreateChannel] = createSignal(false);
  const [newChannelName, setNewChannelName] = createSignal('');
  const [newCapabilities, setNewCapabilities] = createSignal<number>(Capability.Chat);
  const [newAccessGroupId, setNewAccessGroupId] = createSignal<string | undefined>(undefined);
  const [version, setVersion] = createSignal<string | null>(null);
  const [contextMenuChannelId, setContextMenuChannelId] = createSignal<string | null>(null);
  const [contextMenuPos, setContextMenuPos] = createSignal<{ x: number; y: number }>({ x: 0, y: 0 });
  const [favorites, setFavorites] = createSignal<Set<string>>(new Set());
  const [showMembershipModal, setShowMembershipModal] = createSignal(false);
  const [canUseMemberTiers, setCanUseMemberTiers] = createSignal(false);

  onMount(async () => {
    try {
      const data = await api.get<{ currentVersion: string }>('/api/v1/admin/system/version');
      setVersion(data.currentVersion);
    } catch {
      // Best-effort
    }
    // Fetch the public config flag so we know whether to surface the Membership menu item.
    try {
      const config = await api.get<{ canUseMemberTiers?: boolean }>('/api/v1/config');
      setCanUseMemberTiers(!!config.canUseMemberTiers);
    } catch {
      // Best-effort
    }
  });

  /** Membership is shown to members (non-owners) on instances that have the tier feature enabled. */
  const canShowMembership = createMemo(() => {
    if (!canUseMemberTiers()) return false;
    const server = currentServer();
    const userId = authStore.user?.id;
    if (!server || !userId) return false;
    return server.ownerId !== userId;
  });

  const currentServer = createMemo(() =>
    serverStore.servers.find((s) => s.id === serverStore.selectedServerId) ?? serverStore.servers[0]
  );

  // Favorites - synced with backend
  createEffect(() => {
    const sid = serverStore.selectedServerId;
    if (!sid) { setFavorites(new Set<string>()); return; }
    api.get<{ favoriteChannelIds: string[] }>(`/api/v1/servers/${sid}/favorites`)
      .then((res) => setFavorites(new Set<string>(res.favoriteChannelIds)))
      .catch(() => setFavorites(new Set<string>()));
  });

  const toggleFavorite = (channelId: string) => {
    const sid = serverStore.selectedServerId;
    if (!sid) return;
    return toggleFavoriteChannel({ serverId: sid, channelId, current: favorites(), setFavorites });
  };

  const isFavorite = (channelId: string) => favorites().has(channelId);

  // Flat sorted channel list
  const allChannelsSorted = createMemo(() =>
    [...channelStore.channels].sort((a, b) => a.position - b.position)
  );

  const favoriteChannels = createMemo(() =>
    allChannelsSorted().filter((c) => favorites().has(c.id))
  );

  const visibleChannelIds = createMemo<string[]>(() =>
    allChannelsSorted().map((c) => c.id)
  );

  const handleChannelListKeyDown = makeChannelListKeyDown({
    visibleChannelIds,
    focusedChannelId,
    setFocusedChannelId,
    selectChannel: (id) => channelStore.selectChannel(id),
  });

  const navigateToServer = (serverId: string) => {
    serverStore.selectServer(serverId);
    navigate(`/channels/${serverId}`);
  };

  const navigateToChannel = (channel: Channel) => {
    channelStore.selectChannel(channel.id);
    setFocusedChannelId(channel.id);
    if (hasCapability(channel.capabilities, Capability.Voice)) voiceStore.joinVoice(channel.id);
    const serverId = serverStore.selectedServerId;
    if (serverId) navigate(`/channels/${serverId}/${channel.id}`);
  };

  const resetCreateChannelForm = () => {
    setNewChannelName('');
    setNewCapabilities(Capability.Chat);
    setNewAccessGroupId(undefined);
  };

  const handleCreateChannel = async () => {
    const name = newChannelName().trim();
    if (!name || !serverStore.selectedServerId) return;
    try {
      await channelStore.createChannel(serverStore.selectedServerId, name, newCapabilities(), newAccessGroupId());
      resetCreateChannelForm();
      setShowCreateChannel(false);
      setIsLocked(false);
    } catch {
      // Silently ignore
    }
  };

  const closeServerMenu = () => { setShowServerMenu(false); setIsLocked(false); };

  return (
    <Flexbox
      direction="vertical"
      class={`sidebar ${styles.sidebar}`}
      classList={{ 'sidebar-locked': isLocked() }}
    >
      {/* Server header */}
      <Show when={currentServer()}>
        {(server) => (
          <ServerHeader
            server={server()}
            selectedServerId={serverStore.selectedServerId}
            showServerMenu={showServerMenu()}
            canShowMembership={canShowMembership()}
            onMenuOpen={() => { setShowServerMenu(!showServerMenu()); setIsLocked(true); }}
            onMenuClose={closeServerMenu}
            onNavigateToServer={navigateToServer}
            onCreateChannel={() => setShowCreateChannel(true)}
            onOpenServerSettings={() => modals.openServerSettings()}
            onOpenInvite={() => setShowInviteModal(true)}
            onToggleEvents={() => modals.toggleEvents()}
            onOpenGroups={() => modals.toggleGroupManager()}
            onOpenMembership={() => setShowMembershipModal(true)}
            onLeaveServer={() => setShowLeaveConfirm(true)}
          />
        )}
      </Show>

      {/* Channel list */}
      <ChannelList
        isLoading={channelStore.isLoading}
        hasAnyChannels={channelStore.channels.length > 0}
        allChannels={allChannelsSorted()}
        favoriteChannels={favoriteChannels()}
        selectedChannelId={channelStore.selectedChannelId}
        focusedChannelId={focusedChannelId()}
        getUnreadCount={(cid) => unreadStore.getUnreadCount(cid)}
        isFavorite={isFavorite}
        onKeyDown={handleChannelListKeyDown}
        onChannelClick={navigateToChannel}
        onChannelFocus={(id) => setFocusedChannelId(id)}
        onChannelContextMenu={(channel, x, y) => {
          setContextMenuChannelId(channel.id);
          setContextMenuPos({ x, y });
        }}
      />

      {/* Channel context menu */}
      <Menu
        open={contextMenuChannelId() !== null}
        onClose={() => setContextMenuChannelId(null)}
        position={contextMenuPos()}
      >
        <button
          data-testid={contextMenuChannelId() && isFavorite(contextMenuChannelId()!) ? 'channel-context-remove-favorite' : 'channel-context-add-favorite'}
          type="button"
          role="menuitem"
          class={styles.menuItem}
          onClick={() => {
            const id = contextMenuChannelId();
            if (id) toggleFavorite(id);
            setContextMenuChannelId(null);
          }}
        >
          {contextMenuChannelId() && isFavorite(contextMenuChannelId()!) ? 'Remove from Favorites' : 'Add to Favorites'}
        </button>
      </Menu>

      {/* Voice panel - expanded-only via CSS */}
      <div class={`expanded-only ${styles.voicePanelWrapper}`}>
        <VoicePanel />
      </div>

      {/* Create-server affordance for users on instances without a hub iframe.
       *  When the hub overlay is present, server creation typically happens at
       *  the hub level; this button is still safe to show because it just
       *  triggers POST /api/v1/servers locally. */}
      <button
        data-testid="sidebar-create-server-button"
        type="button"
        class={`expanded-only ${styles.menuItem}`}
        onClick={() => modals.openCreateServer()}
      >
        + Create Server
      </button>

      {/* Bottom user panel */}
      <UserStatusBar
        profile={profileStore.userProfile}
        isAdmin={!!authStore.user?.isAdmin}
        version={version()}
        onOpenSettings={() => modals.openSettings('profile')}
        onLogout={() => { modals.closeAll(); authStore.logout(); }}
      />

      {/* Invite modal */}
      <Show when={showInviteModal() && serverStore.selectedServerId}>
        <InviteModal
          serverId={serverStore.selectedServerId!}
          onClose={() => setShowInviteModal(false)}
        />
      </Show>

      {/* Server settings modal */}
      <Show when={modals.showServerSettings && serverStore.selectedServerId}>
        <ServerSettings
          serverId={serverStore.selectedServerId!}
          onClose={() => modals.closeServerSettings()}
          initialTab={modals.serverSettingsTab}
        />
      </Show>

      {/* Leave server confirmation */}
      <LeaveServerModal
        open={showLeaveConfirm()}
        onClose={() => setShowLeaveConfirm(false)}
        onConfirm={() => {
          setShowLeaveConfirm(false);
          const sid = serverStore.selectedServerId;
          if (sid) {
            serverStore.leaveServer(sid)
              .then(() => navigate('/channels/me'))
              .catch(() => {
                // Owner cannot leave
              });
          }
        }}
      />

      {/* Create channel modal */}
      <CreateChannelModal
        open={showCreateChannel()}
        name={newChannelName()}
        capabilities={newCapabilities()}
        onNameInput={(v) => setNewChannelName(v)}
        onToggleCapability={(cap) => setNewCapabilities((p) => p ^ cap)}
        onClose={() => { setShowCreateChannel(false); resetCreateChannelForm(); }}
        onSubmit={handleCreateChannel}
      />

      {/* Create server modal - opened from the rail button or the home welcome CTA */}
      <Show when={modals.showCreateServer}>
        <CreateServerModal onClose={() => modals.closeCreateServer()} />
      </Show>

      {/* Membership modal (member-facing tier subscription) */}
      <Show when={showMembershipModal() && serverStore.selectedServerId}>
        <Modal
          data-testid="membership-modal"
          open={true}
          onClose={() => setShowMembershipModal(false)}
          title="Server Membership"
          size="md"
        >
          <MembershipPanel serverId={serverStore.selectedServerId!} />
        </Modal>
      </Show>
    </Flexbox>
  );
}
