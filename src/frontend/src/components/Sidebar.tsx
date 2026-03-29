import { For, Show, createEffect, createMemo, createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import { useUnread } from '../stores/unread.store';
import { useVoice } from '../stores/voice.store';
import { useAuth } from '../stores/auth.store';
import { useProfiles } from '../stores/profile.store';
import { useModals } from '../stores/modal.store';
import { api } from '../api/client';
import { Capability, hasCapability } from '../types/channel';
import type { Channel } from '../types/channel';
import InviteModal from './InviteModal';
import ServerSettings from './ServerSettings';
import VoicePanel from './VoicePanel';
import StatusPicker from './StatusPicker';
import Modal from './ui/Modal';
import Menu from './ui/Menu';
import styles from './Sidebar.module.css';

// --- SVG Icon Components ---

function TextChannelIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'} aria-hidden="true">
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  );
}

function VoiceChannelIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'} aria-hidden="true">
      <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
      <path d="M19.07 4.93a10 10 0 0 1 0 14.14" />
      <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
    </svg>
  );
}

function ForumChannelIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'} aria-hidden="true">
      <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
      <rect x="8" y="2" width="8" height="4" rx="1" ry="1" />
    </svg>
  );
}

function ChannelIcon(props: { capabilities: number; class?: string }) {
  return (
    <Show when={hasCapability(props.capabilities, Capability.Voice)} fallback={
      <Show when={hasCapability(props.capabilities, Capability.Forum)} fallback={<TextChannelIcon class={props.class} />}>
        <ForumChannelIcon class={props.class} />
      </Show>
    }>
      <VoiceChannelIcon class={props.class} />
    </Show>
  );
}

function GearIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

function LogoutIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <polyline points="16 17 21 12 16 7" />
      <line x1="21" y1="12" x2="9" y2="12" />
    </svg>
  );
}

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
  const [showServerSettings, setShowServerSettings] = createSignal(false);
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
  let menuButtonRef!: HTMLButtonElement;

  onMount(async () => {
    try {
      const data = await api.get<{ currentVersion: string }>('/api/v1/admin/system/version');
      setVersion(data.currentVersion);
    } catch {
      // Best-effort
    }
  });

  const isValidVersion = (v: string) =>
    v !== '0.0.0' && v !== '0.0.0-dev' && v !== '';

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

  const toggleFavorite = async (channelId: string) => {
    const sid = serverStore.selectedServerId;
    if (!sid) return;
    const next = new Set(favorites());
    if (next.has(channelId)) next.delete(channelId); else next.add(channelId);
    setFavorites(next);
    try {
      await api.put(`/api/v1/servers/${sid}/favorites`, {
        favoriteChannelIds: [...next],
      });
    } catch {
      // Revert on failure
      if (next.has(channelId)) next.delete(channelId); else next.add(channelId);
      setFavorites(new Set(next));
    }
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

  const handleChannelListKeyDown = (e: KeyboardEvent) => {
    const ids = visibleChannelIds();
    if (ids.length === 0) return;
    const current = focusedChannelId();
    const currentIdx = current !== null ? ids.indexOf(current) : -1;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const next = currentIdx < ids.length - 1 ? ids[currentIdx + 1] : ids[0];
      setFocusedChannelId(next);
      focusChannelButton(next);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prev = currentIdx > 0 ? ids[currentIdx - 1] : ids[ids.length - 1];
      setFocusedChannelId(prev);
      focusChannelButton(prev);
    } else if (e.key === 'Home') {
      e.preventDefault();
      setFocusedChannelId(ids[0]);
      focusChannelButton(ids[0]);
    } else if (e.key === 'End') {
      e.preventDefault();
      setFocusedChannelId(ids[ids.length - 1]);
      focusChannelButton(ids[ids.length - 1]);
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      if (current !== null) channelStore.selectChannel(current);
    }
  };

  const focusChannelButton = (channelId: string) => {
    const el = document.querySelector<HTMLElement>(`[data-channel-id="${channelId}"]`);
    el?.focus();
  };

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

  const handleCreateChannel = async () => {
    const name = newChannelName().trim();
    if (!name || !serverStore.selectedServerId) return;
    try {
      await channelStore.createChannel(serverStore.selectedServerId, name, newCapabilities(), newAccessGroupId());
      setNewChannelName('');
      setNewCapabilities(Capability.Chat);
      setNewAccessGroupId(undefined);
      setShowCreateChannel(false);
      setIsLocked(false);
    } catch {
      // Silently ignore
    }
  };

  const channelButtonClass = (channel: Channel, hasUnread: boolean) => {
    const stateClass = channelStore.selectedChannelId === channel.id
      ? styles.channelButtonActive
      : hasUnread
        ? styles.channelButtonUnread
        : styles.channelButtonDefault;
    return `${styles.channelButton} channel-icon-wrap ${stateClass}`;
  };

  const renderChannelItem = (channel: Channel) => {
    const unreadCount = () => unreadStore.getUnreadCount(channel.conversationId);
    const hasUnread = () => unreadCount() > 0;
    return (
      <button
        role="option"
        aria-selected={channelStore.selectedChannelId === channel.id}
        aria-label={`${hasCapability(channel.capabilities, Capability.Voice) ? 'Voice channel' : hasCapability(channel.capabilities, Capability.Forum) ? 'Forum channel' : 'Text channel'} ${channel.name}${hasUnread() ? `, ${unreadCount()} unread` : ''}`}
        data-channel-id={channel.id}
        data-testid={`channel-item-${channel.id}`}
        tabindex={focusedChannelId() === channel.id || (focusedChannelId() === null && channelStore.selectedChannelId === channel.id) ? 0 : -1}
        class={channelButtonClass(channel, hasUnread())}
        onClick={() => navigateToChannel(channel)}
        onFocus={() => setFocusedChannelId(channel.id)}
        onContextMenu={(e) => {
          e.preventDefault();
          setContextMenuChannelId(channel.id);
          setContextMenuPos({ x: e.clientX, y: e.clientY });
        }}
      >
        {/* Channel type icon with unread indicator */}
        <span class={styles.channelIconWrap}>
          <ChannelIcon capabilities={channel.capabilities} class={styles.channelTypeIcon} />
          <Show when={hasUnread()}>
            <span class={`${styles.unreadDot} collapsed-only`} aria-hidden="true" />
          </Show>
        </span>

        {/* Channel name + unread badge - visible only when expanded (CSS) */}
        <span class={`expanded-only ${styles.channelNameArea} ${hasUnread() ? styles.channelNameAreaUnread : ''}`}>
          <span
            class={styles.unreadIndicatorDot}
            style={{ visibility: hasUnread() ? 'visible' : 'hidden' }}
            aria-hidden="true"
          />
          <span class={styles.channelName}>{channel.name}</span>
          <Show when={hasUnread()}>
            <span
              class={styles.unreadBadge}
              aria-label={`${unreadCount()} unread messages`}
            >
              {unreadCount() > 99 ? '99+' : unreadCount()}
            </span>
          </Show>
        </span>
      </button>
    );
  };

  return (
    <div
      class={`sidebar ${styles.sidebar}`}
      classList={{ 'sidebar-locked': isLocked() }}
    >
      {/* Server header - fixed height, icon always in same position */}
      <Show when={currentServer()}>
        {(server) => (
          <div class={styles.serverHeader}>
            <button
              data-testid="nav-server-icon"
              aria-label={server().name}
              class={styles.serverIconButton}
              onClick={() => navigateToServer(server().id)}
            >
              <Show when={server().iconUrl} fallback={
                <span class={styles.serverIconText}>{server().name.length <= 4 ? server().name : server().name.split(/\s+/).map(w => w[0]).join('').slice(0, 3).toUpperCase()}</span>
              }>
                <img src={server().iconUrl} alt={server().name} class={styles.serverIconImg} />
              </Show>
            </button>

            {/* Create channel - always in the header row, never displaces anything */}
            <button
              data-testid="create-channel-button"
              aria-label="Create Channel"
              title="Create Channel"
              class={styles.headerPlusButton}
              onClick={() => setShowCreateChannel(true)}
            >
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.createChannelPlusIcon} aria-hidden="true">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
            </button>

            {/* Server name + menu - CSS expanded-only */}
            <h2 class={`expanded-only ${styles.serverNameHeading}`} data-testid="server-name-heading">{server().name}</h2>
            <Show when={serverStore.selectedServerId}>
              <button
                data-testid="server-menu-trigger"
                ref={menuButtonRef}
                type="button"
                aria-label="Server options"
                aria-haspopup="menu"
                aria-expanded={showServerMenu()}
                title="Server Options"
                onClick={() => {
                  setShowServerMenu(!showServerMenu());
                  setIsLocked(true);
                }}
                class={`expanded-only ${styles.serverMenuTrigger}`}
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.chevronIcon} aria-hidden="true">
                  <polyline points="6 9 12 15 18 9"/>
                </svg>
              </button>
            </Show>

            {/* Server dropdown menu */}
            <Menu
              open={showServerMenu()}
              onClose={() => { setShowServerMenu(false); setIsLocked(false); menuButtonRef?.focus(); }}
              anchorRef={menuButtonRef}
              placement="bottom-start"
            >
              <button
                data-testid="server-menu-settings"
                type="button"
                role="menuitem"
                onClick={() => { setShowServerSettings(true); setShowServerMenu(false); setIsLocked(false); }}
                class={styles.menuItem}
              >
                Server Settings
              </button>
              <button
                data-testid="server-menu-invite"
                type="button"
                role="menuitem"
                onClick={() => { setShowInviteModal(true); setShowServerMenu(false); setIsLocked(false); }}
                class={styles.menuItem}
              >
                Invite People
              </button>
              <button
                type="button"
                role="menuitem"
                onClick={() => { modals.toggleEvents(); setShowServerMenu(false); setIsLocked(false); }}
                class={styles.menuItem}
              >
                Scheduled Events
              </button>
              <div class={styles.menuDivider} />
              <button
                data-testid="server-menu-leave"
                type="button"
                role="menuitem"
                onClick={() => {
                  setShowServerMenu(false);
                  setIsLocked(false);
                  setShowLeaveConfirm(true);
                }}
                class={styles.menuItemDanger}
              >
                Leave Server
              </button>
            </Menu>
          </div>
        )}
      </Show>

      {/* Channel list */}
      <div
        class={styles.channelList}
        role="listbox"
        aria-label="Channels"
        aria-orientation="vertical"
        onKeyDown={handleChannelListKeyDown}
      >
        {/* Loading skeleton */}
        <Show when={channelStore.isLoading && channelStore.channels.length === 0}>
          <For each={[0, 1, 2, 3, 4]}>
            {() => (
              <div class={styles.skeletonRow} aria-hidden="true">
                <div class={styles.skeletonIcon} />
              </div>
            )}
          </For>
        </Show>

        {/* Favorite channels */}
        <Show when={favoriteChannels().length > 0}>
          <div class={`expanded-only ${styles.favoritesLabel}`}>Favorites</div>
          <For each={favoriteChannels()}>
            {(channel) => renderChannelItem(channel)}
          </For>
          <div class={`expanded-only ${styles.sectionDivider}`} />
        </Show>

        {/* All channels (flat list, no categories) */}
        <For each={allChannelsSorted()}>
          {(channel) => renderChannelItem(channel)}
        </For>

      </div>

      {/* Channel context menu */}
      <Menu
        open={contextMenuChannelId() !== null}
        onClose={() => setContextMenuChannelId(null)}
        position={contextMenuPos()}
      >
        <button
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

      {/* Bottom user panel */}
      <div class={styles.userPanel}>
        {/* Version badge - expanded-only via CSS */}
        <Show when={version() && isValidVersion(version()!)}>
          <div class={`expanded-only ${styles.versionBadgeWrapper}`}>
            <span data-testid="version-badge" class={styles.versionBadge}>
              v{version()}
            </span>
          </div>
        </Show>

        <Show when={profileStore.userProfile}>
          {(profile) => (
            <div data-testid="nav-user-avatar" id="current-user-bar" class={styles.userBar}>
              {/* Avatar */}
              <div class={styles.avatarGroup}>
                <div
                  class={styles.avatarButton}
                  onClick={() => modals.openSettings('profile')}
                >
                  <Show when={profile().avatarUrl} fallback={<span class={styles.avatarInitial}>{profile().username.charAt(0).toUpperCase()}</span>}>
                    <img src={profile().avatarUrl} alt={profile().username} class={styles.avatarImg} />
                  </Show>
                </div>
                <StatusPicker />
                <Show when={authStore.user?.isAdmin}>
                  <div class={styles.adminBadge} title="Admin">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" class={styles.adminIcon}>
                      <path fill-rule="evenodd" d="M8.5 1.709a.75.75 0 0 0-1 0 8.963 8.963 0 0 1-4.84 2.217.75.75 0 0 0-.654.72 10.499 10.499 0 0 0 5.647 9.672.75.75 0 0 0 .694 0 10.499 10.499 0 0 0 5.647-9.672.75.75 0 0 0-.654-.72A8.963 8.963 0 0 1 8.5 1.71z" clip-rule="evenodd" />
                    </svg>
                  </div>
                </Show>
                {/* Tooltip - shown via CSS when collapsed (not hovered) */}
                <div class={`collapsed-only ${styles.userTooltip}`}>
                  {profile().displayName || profile().username}
                </div>
              </div>

              {/* Username - expanded-only via CSS */}
              <span class={`expanded-only ${styles.username}`}>{profile().displayName || profile().username}</span>

              {/* Settings + Logout */}
              <div class={styles.userActions}>
                <button
                  data-testid="nav-user-settings-button"
                  class={styles.iconButton}
                  aria-label="Settings"
                  title="Settings"
                  onClick={() => modals.openSettings('profile')}
                >
                  <GearIcon class={styles.smallIcon} />
                </button>
                <button
                  data-testid="nav-logout-button"
                  class={`${styles.iconButton} ${styles.iconButtonDanger}`}
                  aria-label="Log Out"
                  title="Log Out"
                  onClick={() => authStore.logout()}
                >
                  <LogoutIcon class={styles.smallIcon} />
                </button>
              </div>
            </div>
          )}
        </Show>
      </div>

      {/* Invite modal */}
      <Show when={showInviteModal() && serverStore.selectedServerId}>
        <InviteModal
          serverId={serverStore.selectedServerId!}
          onClose={() => setShowInviteModal(false)}
        />
      </Show>

      {/* Server settings modal */}
      <Show when={showServerSettings() && serverStore.selectedServerId}>
        <ServerSettings
          serverId={serverStore.selectedServerId!}
          onClose={() => setShowServerSettings(false)}
        />
      </Show>

      {/* Leave server confirmation */}
      <Modal data-testid="leave-server-dialog" open={showLeaveConfirm()} onClose={() => setShowLeaveConfirm(false)} title="Leave Server" size="sm" role="alertdialog">
        <div class={styles.leaveModalBody}>
          <p class={styles.leaveModalText}>Are you sure you want to leave this server?</p>
          <div class={styles.leaveModalActions}>
            <button
              data-testid="leave-server-cancel-button"
              type="button"
              onClick={() => setShowLeaveConfirm(false)}
              class={styles.cancelButton}
            >
              Cancel
            </button>
            <button
              data-testid="leave-server-confirm-button"
              type="button"
              onClick={() => {
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
              class={styles.leaveButton}
            >
              Leave Server
            </button>
          </div>
        </div>
      </Modal>

      {/* Create channel modal */}
      <Modal open={showCreateChannel()} onClose={() => { setShowCreateChannel(false); setNewChannelName(''); setNewCapabilities(Capability.Chat); setNewAccessGroupId(undefined); }} title="Create Channel" size="sm">
        <div class={styles.createModalBody}>
          <div class={styles.createModalField}>
            <label for="new-channel-name" class={styles.createModalLabel}>Channel Name</label>
            <input
              id="new-channel-name"
              data-testid="create-channel-name-input"
              type="text"
              placeholder="new-channel"
              value={newChannelName()}
              onInput={(e) => setNewChannelName(e.currentTarget.value)}
              class={styles.createModalInput}
              onKeyPress={(e) => { if (e.key === 'Enter') handleCreateChannel(); }}
            />
          </div>
          <div class={styles.createModalField}>
            <span class={styles.createModalLabel}>Capabilities</span>
            <div class={styles.createModalCheckboxes}>
              <label class={styles.createModalCheckbox}>
                <input type="checkbox" checked={hasCapability(newCapabilities(), Capability.Chat)} onChange={() => setNewCapabilities(p => p ^ Capability.Chat)} />
                Chat
              </label>
              <label class={styles.createModalCheckbox}>
                <input type="checkbox" checked={hasCapability(newCapabilities(), Capability.Voice)} onChange={() => setNewCapabilities(p => p ^ Capability.Voice)} />
                Voice
              </label>
              <label class={styles.createModalCheckbox}>
                <input type="checkbox" checked={hasCapability(newCapabilities(), Capability.Video)} onChange={() => setNewCapabilities(p => p ^ Capability.Video)} />
                Video
              </label>
              <label class={styles.createModalCheckbox}>
                <input type="checkbox" checked={hasCapability(newCapabilities(), Capability.Forum)} onChange={() => setNewCapabilities(p => p ^ Capability.Forum)} />
                Forum
              </label>
              <label class={styles.createModalCheckbox}>
                <input type="checkbox" checked={hasCapability(newCapabilities(), Capability.Announcement)} onChange={() => setNewCapabilities(p => p ^ Capability.Announcement)} />
                Announcement
              </label>
            </div>
          </div>
          <div class={styles.createModalActions}>
            <button
              type="button"
              class={styles.cancelButton}
              onClick={() => { setShowCreateChannel(false); setNewChannelName(''); setNewCapabilities(Capability.Chat); setNewAccessGroupId(undefined); }}
            >
              Cancel
            </button>
            <button
              data-testid="create-channel-submit-button"
              type="button"
              class={styles.createModalSubmit}
              onClick={handleCreateChannel}
            >
              Create Channel
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
