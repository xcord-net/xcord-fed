import { Show, createMemo, createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../../stores/channel.store';
import { useServers } from '../../stores/server.store';
import { useAuth } from '../../stores/auth.store';
import { useModals } from '../../stores/modal.store';
import { api } from '../../api/client';
import { Capability } from '../../types/channel';
import InviteModal from '../InviteModal';
import Modal from '../ui/Modal';
import CreateServerModal from '../CreateServerModal';
import MembershipPanel from '../MembershipPanel';
import ServerHeader from './ServerHeader';
import LeaveServerModal from './LeaveServerModal';
import CreateChannelModal from './CreateChannelModal';
import Flexbox from '../ui/Flexbox';
import styles from './Sidebar.module.css';

export default function Sidebar() {
  const navigate = useNavigate();
  const channelStore = useChannels();
  const serverStore = useServers();
  const authStore = useAuth();
  const modals = useModals();

  // JS only used for "lock open" when interacting with forms/menus
  const [isLocked, setIsLocked] = createSignal(false);
  const [showInviteModal, setShowInviteModal] = createSignal(false);
  const [showServerMenu, setShowServerMenu] = createSignal(false);
  const [showLeaveConfirm, setShowLeaveConfirm] = createSignal(false);
  const [showCreateChannel, setShowCreateChannel] = createSignal(false);
  const [newChannelName, setNewChannelName] = createSignal('');
  const [newCapabilities, setNewCapabilities] = createSignal<number>(Capability.Chat);
  const [newAccessGroupId, setNewAccessGroupId] = createSignal<string | undefined>(undefined);
  const [showMembershipModal, setShowMembershipModal] = createSignal(false);
  const [canUseMemberTiers, setCanUseMemberTiers] = createSignal(false);

  onMount(async () => {
    // Public config flag: whether the community menu offers Membership.
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

  const navigateToServer = (serverId: string) => {
    serverStore.selectServer(serverId);
    navigate(`/channels/${serverId}`);
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
      class={styles.communityBar}
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

      {/* Invite modal */}
      <Show when={showInviteModal() && serverStore.selectedServerId}>
        <InviteModal
          serverId={serverStore.selectedServerId!}
          onClose={() => setShowInviteModal(false)}
        />
      </Show>

      {/* Community settings is a Deck tab; see Deck.tsx. */}

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
