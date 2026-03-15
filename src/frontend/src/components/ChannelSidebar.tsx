import { For, Show, createMemo, createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import { useUnread } from '../stores/unread.store';
import { useVoice } from '../stores/voice.store';
import type { Channel } from '../types/channel';
import InviteModal from './InviteModal';
import ServerSettings from './ServerSettings';
import VoicePanel from './VoicePanel';
import Modal from './ui/Modal';
import Menu from './ui/Menu';


export default function ChannelSidebar() {
  const navigate = useNavigate();
  const channelStore = useChannels();
  const serverStore = useServers();
  const unreadStore = useUnread();
  const voiceStore = useVoice();
  const [focusedChannelId, setFocusedChannelId] = createSignal<string | null>(null);
  const [showInviteModal, setShowInviteModal] = createSignal(false);
  const [showServerSettings, setShowServerSettings] = createSignal(false);
  const [showServerMenu, setShowServerMenu] = createSignal(false);
  const [showLeaveConfirm, setShowLeaveConfirm] = createSignal(false);
  const [showCreateChannel, setShowCreateChannel] = createSignal(false);
  const [newChannelName, setNewChannelName] = createSignal('');
  const [newChannelType, setNewChannelType] = createSignal<'Text' | 'Voice' | 'Forum'>('Text');
  let menuButtonRef!: HTMLButtonElement;

  const currentServer = createMemo(() =>
    serverStore.servers.find((s) => s.id === serverStore.selectedServerId)
  );

  const sortedCategories = createMemo(() =>
    [...channelStore.categories].sort((a, b) => a.position - b.position)
  );

  const uncategorizedChannels = createMemo(() =>
    channelStore.channels.filter((c) => !c.categoryId).sort((a, b) => a.position - b.position)
  );

  const channelsByCategory = (categoryId: string) =>
    channelStore.channels.filter((c) => c.categoryId === categoryId).sort((a, b) => a.position - b.position);

  // Build a flat list of visible channel IDs for arrow-key navigation
  const visibleChannelIds = createMemo<string[]>(() => {
    const ids: string[] = [];
    for (const ch of uncategorizedChannels()) {
      ids.push(ch.id);
    }
    for (const cat of sortedCategories()) {
      if (!channelStore.collapsedCategories.has(cat.id)) {
        for (const ch of channelsByCategory(cat.id)) {
          ids.push(ch.id);
        }
      }
    }
    return ids;
  });

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
      if (current !== null) {
        channelStore.selectChannel(current);
      }
    }
  };

  const focusChannelButton = (channelId: string) => {
    const el = document.querySelector<HTMLElement>(`[data-channel-id="${channelId}"]`);
    el?.focus();
  };

  const handleCreateChannel = async () => {
    const name = newChannelName().trim();
    if (!name || !serverStore.selectedServerId) return;
    try {
      await channelStore.createChannel(serverStore.selectedServerId, name, newChannelType());
      setNewChannelName('');
      setNewChannelType('Text');
      setShowCreateChannel(false);
    } catch {
      // Silently ignore; channel creation errors don't need inline display here
    }
  };

  const channelButtonClass = (channel: Channel, hasUnread: boolean) =>
    `w-full px-2 py-1.5 rounded flex items-center gap-1.5 text-sm transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none ${
      channelStore.selectedChannelId === channel.id
        ? 'bg-xcord-bg-primary text-white'
        : hasUnread
          ? 'text-xcord-text-primary hover:bg-xcord-bg-primary'
          : 'text-xcord-text-secondary hover:bg-xcord-bg-primary hover:text-xcord-text-primary'
    }`;

  return (
    <div class="w-60 bg-xcord-bg-secondary flex flex-col">
      {/* Server header */}
      <div class="h-12 px-4 flex items-center justify-between border-b border-xcord-border shadow-sm relative">
        <h2 class="font-semibold text-white truncate flex-1">{currentServer()?.name || 'Select a server'}</h2>
        <Show when={serverStore.selectedServerId}>
          <button
            data-testid="server-menu-trigger"
            ref={menuButtonRef}
            type="button"
            aria-label="Server options"
            aria-haspopup="menu"
            aria-expanded={showServerMenu()}
            title="Server Options"
            onClick={() => setShowServerMenu(!showServerMenu())}
            class="ml-2 flex-shrink-0 text-xcord-text-secondary hover:text-xcord-text-primary transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none rounded p-0.5"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="w-4 h-4" aria-hidden="true">
              <polyline points="6 9 12 15 18 9"/>
            </svg>
          </button>
        </Show>

        {/* Server dropdown menu */}
        <Menu
          open={showServerMenu()}
          onClose={() => { setShowServerMenu(false); menuButtonRef?.focus(); }}
          anchorRef={menuButtonRef}
          placement="bottom-start"
        >
          <button
            data-testid="server-menu-settings"
            type="button"
            role="menuitem"
            onClick={() => { setShowServerSettings(true); setShowServerMenu(false); }}
            class="w-full px-3 py-2 text-left text-sm text-xcord-text-primary hover:bg-xcord-bg-primary hover:text-white transition-colors"
          >
            Server Settings
          </button>
          <button
            data-testid="server-menu-invite"
            type="button"
            role="menuitem"
            onClick={() => { setShowInviteModal(true); setShowServerMenu(false); }}
            class="w-full px-3 py-2 text-left text-sm text-xcord-text-primary hover:bg-xcord-bg-primary hover:text-white transition-colors"
          >
            Invite People
          </button>
          <div class="border-t border-xcord-border my-1" />
          <button
            data-testid="server-menu-leave"
            type="button"
            role="menuitem"
            onClick={() => {
              setShowServerMenu(false);
              setShowLeaveConfirm(true);
            }}
            class="w-full px-3 py-2 text-left text-sm text-red-400 hover:bg-red-600 hover:text-white transition-colors"
          >
            Leave Server
          </button>
        </Menu>
      </div>

      {/* Create channel section */}
      <Show when={serverStore.selectedServerId}>
        <div class="px-2 py-1 border-b border-xcord-border">
          <Show when={showCreateChannel()} fallback={
            <button
              data-testid="create-channel-button"
              class="w-full text-left px-2 py-1 text-xs text-xcord-text-muted hover:text-white"
              onClick={() => setShowCreateChannel(true)}
              title="Create Channel"
            >
              + Create Channel
            </button>
          }>
            <div class="space-y-1">
              <div class="flex space-x-1">
                <input
                  id="new-channel-name"
                  type="text"
                  placeholder="channel-name"
                  value={newChannelName()}
                  onInput={(e) => setNewChannelName(e.currentTarget.value)}
                  class="flex-1 bg-xcord-bg-primary text-white px-2 py-1 rounded text-xs border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                  onKeyPress={(e) => { if (e.key === 'Enter') handleCreateChannel(); }}
                />
                <button
                  class="bg-xcord-brand text-white px-2 py-1 rounded text-xs hover:bg-xcord-brand-hover"
                  onClick={handleCreateChannel}
                >
                  Create
                </button>
                <button
                  class="text-xcord-text-muted hover:text-white px-1 py-1 text-xs"
                  onClick={() => { setShowCreateChannel(false); setNewChannelName(''); setNewChannelType('Text'); }}
                >
                  ✕
                </button>
              </div>
              <select
                id="new-channel-type"
                value={newChannelType()}
                onChange={(e) => setNewChannelType(e.currentTarget.value as 'Text' | 'Voice' | 'Forum')}
                class="w-full bg-xcord-bg-primary text-white px-2 py-1 rounded text-xs focus:outline-none focus:ring-1 focus:ring-xcord-brand"
              >
                <option value="Text">Text</option>
                <option value="Voice">Voice</option>
                <option value="Forum">Forum</option>
              </select>
            </div>
          </Show>
        </div>
      </Show>

      {/* Channel list */}
      <div
        class="flex-1 overflow-y-auto px-2 py-3 space-y-0.5"
        role="listbox"
        aria-label="Channels"
        aria-orientation="vertical"
        onKeyDown={handleChannelListKeyDown}
      >
        {/* Loading skeleton while fetching channels */}
        <Show when={channelStore.isLoading && channelStore.channels.length === 0}>
          <For each={[0, 1, 2, 3, 4]}>
            {() => (
              <div class="flex items-center space-x-2 px-2 py-1.5" aria-hidden="true">
                <div class="w-4 h-4 rounded bg-xcord-bg-primary animate-pulse flex-shrink-0" />
                <div class="h-3 rounded bg-xcord-bg-primary animate-pulse flex-1" />
              </div>
            )}
          </For>
        </Show>

        {/* Uncategorized channels */}
        <For each={uncategorizedChannels()}>
          {(channel) => {
            const unreadCount = () => unreadStore.getUnreadCount(channel.conversationId);
            const hasUnread = () => unreadCount() > 0;
            return (
              <button
                role="option"
                aria-selected={channelStore.selectedChannelId === channel.id}
                aria-label={`${channel.type === 'Voice' ? 'Voice channel' : channel.type === 'Forum' ? 'Forum channel' : 'Text channel'} ${channel.name}${hasUnread() ? `, ${unreadCount()} unread` : ''}`}
                data-channel-id={channel.id}
                tabindex={focusedChannelId() === channel.id || (focusedChannelId() === null && channelStore.selectedChannelId === channel.id) ? 0 : -1}
                class={channelButtonClass(channel, hasUnread())}
                onClick={() => {
                  channelStore.selectChannel(channel.id);
                  setFocusedChannelId(channel.id);
                  if (channel.type === 'Voice') voiceStore.joinVoice(channel.id);
                  const serverId = serverStore.selectedServerId;
                  if (serverId) navigate(`/channels/${serverId}/${channel.id}`);
                }}
                onFocus={() => setFocusedChannelId(channel.id)}
              >
                {/* Unread dot indicator on left */}
                <span
                  class={`flex-shrink-0 w-1.5 h-1.5 rounded-full bg-white ${hasUnread() ? 'visible' : 'invisible'}`}
                  aria-hidden="true"
                />
                <span class="text-xcord-text-muted flex-shrink-0" aria-hidden="true">
                  {channel.type === 'Voice' ? '🔊' : channel.type === 'Forum' ? '📋' : '#'}
                </span>
                <span class={`truncate flex-1 ${hasUnread() ? 'font-semibold' : ''}`}>{channel.name}</span>
                <Show when={hasUnread()}>
                  <span
                    class="flex-shrink-0 bg-red-500 text-white text-xs rounded-full min-w-[18px] h-[18px] flex items-center justify-center px-1"
                    aria-label={`${unreadCount()} unread messages`}
                  >
                    {unreadCount() > 99 ? '99+' : unreadCount()}
                  </span>
                </Show>
              </button>
            );
          }}
        </For>

        {/* Categories with channels */}
        <For each={sortedCategories()}>
          {(category) => {
            const isCollapsed = () => channelStore.collapsedCategories.has(category.id);
            const channels = channelsByCategory(category.id);

            return (
              <div>
                <button
                  class="w-full px-1 py-1 flex items-center justify-between text-xs font-semibold text-xcord-text-muted uppercase tracking-wide hover:text-xcord-text-secondary transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                  aria-expanded={!isCollapsed()}
                  aria-controls={`category-channels-${category.id}`}
                  onClick={() => channelStore.toggleCategory(category.id)}
                >
                  <span class="flex items-center space-x-1">
                    <span class="text-xs" aria-hidden="true">{isCollapsed() ? '▶' : '▼'}</span>
                    <span>{category.name}</span>
                  </span>
                </button>

                <Show when={!isCollapsed()}>
                  <div id={`category-channels-${category.id}`} class="space-y-0.5">
                    <For each={channels}>
                      {(channel) => {
                        const unreadCount = () => unreadStore.getUnreadCount(channel.conversationId);
                        const hasUnread = () => unreadCount() > 0;
                        return (
                          <button
                            role="option"
                            aria-selected={channelStore.selectedChannelId === channel.id}
                            aria-label={`${channel.type === 'Voice' ? 'Voice channel' : channel.type === 'Forum' ? 'Forum channel' : 'Text channel'} ${channel.name}${hasUnread() ? `, ${unreadCount()} unread` : ''}`}
                            data-channel-id={channel.id}
                            tabindex={focusedChannelId() === channel.id || (focusedChannelId() === null && channelStore.selectedChannelId === channel.id) ? 0 : -1}
                            class={channelButtonClass(channel, hasUnread())}
                            onClick={() => {
                              channelStore.selectChannel(channel.id);
                              setFocusedChannelId(channel.id);
                              if (channel.type === 'Voice') voiceStore.joinVoice(channel.id);
                              const serverId = serverStore.selectedServerId;
                              if (serverId) navigate(`/channels/${serverId}/${channel.id}`);
                            }}
                            onFocus={() => setFocusedChannelId(channel.id)}
                          >
                            {/* Unread dot indicator on left */}
                            <span
                              class={`flex-shrink-0 w-1.5 h-1.5 rounded-full bg-white ${hasUnread() ? 'visible' : 'invisible'}`}
                              aria-hidden="true"
                            />
                            <span class="text-xcord-text-muted flex-shrink-0" aria-hidden="true">
                              {channel.type === 'Voice' ? '🔊' : channel.type === 'Forum' ? '📋' : '#'}
                            </span>
                            <span class={`truncate flex-1 ${hasUnread() ? 'font-semibold' : ''}`}>{channel.name}</span>
                            <Show when={hasUnread()}>
                              <span
                                class="flex-shrink-0 bg-red-500 text-white text-xs rounded-full min-w-[18px] h-[18px] flex items-center justify-center px-1"
                                aria-label={`${unreadCount()} unread messages`}
                              >
                                {unreadCount() > 99 ? '99+' : unreadCount()}
                              </span>
                            </Show>
                          </button>
                        );
                      }}
                    </For>
                  </div>
                </Show>
              </div>
            );
          }}
        </For>
      </div>

      {/* Voice panel */}
      <VoicePanel />

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
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-6">Are you sure you want to leave this server?</p>
          <div class="flex justify-end gap-3">
            <button
              data-testid="leave-server-cancel-button"
              type="button"
              onClick={() => setShowLeaveConfirm(false)}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
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
                      // Owner cannot leave - backend rejects with OWNER_CANNOT_LEAVE.
                      // Stay on the server page instead of navigating away.
                    });
                }
              }}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
            >
              Leave Server
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
