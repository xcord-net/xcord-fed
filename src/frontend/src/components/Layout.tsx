import { Show, createEffect, onMount } from 'solid-js';
import { useParams, useNavigate } from '@solidjs/router';
import Sidebar from './Sidebar';
import ChannelDirectory from './ChannelDirectory';
import { Capability, hasCapability } from '../types/channel';
import MessageList from './MessageList';
import MessageCompose from './MessageCompose';
import TypingIndicator from './TypingIndicator';
import SearchPanel from './SearchPanel';
import PinList from './PinList';
import ThreadPanel from './ThreadPanel';
import ForumPostList from './ForumPostList';
import UserProfileEditor from './UserProfileEditor';
import BlockList from './BlockList';
import NotificationSettings from './NotificationSettings';
import ChannelSettings from './ChannelSettings';
import GroupManager from './GroupManager';
import ScheduledEvents from './ScheduledEvents';
import ScheduledMessages from './ScheduledMessages';
import UserNotes from './UserNotes';
import ScreenShareViewer from './ScreenShareViewer';
import Modal from './ui/Modal';
import ServerSettings from './ServerSettings';
import { useAuth } from '../stores/auth.store';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useMembers } from '../stores/member.store';
import { useMessages } from '../stores/message.store';
import { useSignalR } from '../stores/signalr.store';
import { useUnread } from '../stores/unread.store';
import { useModals } from '../stores/modal.store';
import { requestPermission } from '../services/notification.service';
import styles from './Layout.module.css';

// SVG icons for header
function SearchIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <circle cx="11" cy="11" r="8" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
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

export default function Layout() {
  const params = useParams<{ serverId?: string; channelId?: string }>();
  const navigate = useNavigate();
  const authStore = useAuth();
  const serverStore = useServers();
  const channelStore = useChannels();
  const memberStore = useMembers();
  const messageStore = useMessages();
  const signalR = useSignalR();

  const unreadStore = useUnread();
  const modals = useModals();

  createEffect(() => {
    const server = serverStore.servers.find(s => s.id === params.serverId);
    const channel = channelStore.channels.find(c => c.id === params.channelId);
    if (channel && server) {
      document.title = `${channel.name} | ${server.name}`;
    } else if (server) {
      document.title = server.name;
    } else {
      document.title = 'Xcord';
    }
  });

  // Load servers and connect to SignalR on mount
  onMount(() => {
    serverStore.fetchServers().then(() => {
      // Auto-redirect to first server if no serverId in params
      if (!params.serverId) {
        const servers = serverStore.servers;
        if (servers.length > 0) {
          navigate(`/channels/${servers[0].id}`, { replace: true });
        }
      }
    });
    // Request notification permission after auth (not on login page)
    requestPermission().catch(() => {});
    // Connect to the real-time hub so message/presence/typing events are received.
    signalR.connectSignalR().then(() => {
      const channel = channelStore.channels.find(
        (c) => c.id === channelStore.selectedChannelId,
      );
      const convId = channel?.conversationId;
      if (convId) {
        signalR.joinConversation(convId).catch((err) => {
          console.error('Failed to join conversation after SignalR connect:', err);
        });
      }
    }).catch((err) => {
      console.error('Failed to connect to SignalR:', err);
    });
  });

  // Update selected server when route changes
  let prevServerId: string | undefined;
  createEffect(() => {
    const serverId = params.serverId;
    if (serverId && serverId !== prevServerId) {
      prevServerId = serverId;
      serverStore.selectServer(serverId);
      channelStore.fetchChannels(serverId).catch((err: unknown) => {
        const e = err as { status?: number; title?: string; code?: string; error?: string };
        const httpStatus = e?.status;
        const errorCode = (e?.title ?? e?.code ?? e?.error ?? '').toUpperCase();
        if (
          httpStatus === 403 ||
          httpStatus === 404 ||
          errorCode === 'NOT_A_MEMBER' ||
          errorCode === 'BANNED' ||
          errorCode === 'FORBIDDEN' ||
          errorCode === 'SERVER_NOT_FOUND'
        ) {
          navigate('/', { replace: true });
        }
      });
      memberStore.fetchMembers(serverId);
    }
  });

  // Update selected channel when route changes, and clear forum post selection
  let prevChannelId: string | undefined;
  createEffect(() => {
    const channelId = params.channelId;
    if (channelId && channelId !== prevChannelId) {
      prevChannelId = channelId;
      channelStore.selectChannel(channelId);
      modals.selectForumPost(null);
    }
  });

  // Clear messages when channel changes
  createEffect(() => {
    const channelId = channelStore.selectedChannelId;
    if (channelId) {
      messageStore.clearMessages();
    }
  });

  // Join/leave SignalR conversation group when the active conversation changes, or
  // when the SignalR connection is (re)established.
  let prevConversationId: string | undefined;
  let prevConnected = false;
  createEffect(() => {
    const channel = channelStore.channels.find(
      (c) => c.id === channelStore.selectedChannelId,
    );
    const convId = channel?.conversationId;
    const connected = signalR.isConnected;

    const conversationChanged = convId !== prevConversationId;
    const justConnected = connected && !prevConnected;

    prevConnected = connected;

    if (!conversationChanged && !justConnected) return;

    // Leave the previous conversation group only when the conversation actually changes
    if (conversationChanged && prevConversationId) {
      signalR.leaveConversation(prevConversationId).catch(() => { /* non-fatal */ });
    }

    // Join the new (or current) conversation group
    if (convId && connected) {
      signalR.joinConversation(convId).catch((err) => {
        console.error('Failed to join conversation:', err);
      });
    }

    if (conversationChanged) {
      signalR.setActiveConversationId(convId ?? null);
      if (convId) {
        unreadStore.markRead(convId);
      }
      prevConversationId = convId;
    }
  });

  const currentChannel = () =>
    channelStore.channels.find((c) => c.id === channelStore.selectedChannelId);

  const conversationId = () => currentChannel()?.conversationId;

  /** True when the current channel's conversation has been successfully joined on the SignalR hub. */
  const conversationJoined = () => {
    const convId = conversationId();
    if (!convId) return false;
    return signalR.currentConversations.has(convId);
  };

  return (
    <div class={styles.root} data-signalr-connected={String(signalR.isConnected)} data-signalr-conversation-joined={String(conversationJoined())}>
      {/* Unified sidebar - always present */}
      <Sidebar />

      {/* Channel directory (no channelId) or channel view (with channelId) */}
      <Show
        when={params.channelId}
        fallback={
          <Show when={params.serverId} fallback={
            <div class={styles.emptyState}>
              <p class={styles.emptyStateText}>Loading...</p>
            </div>
          }>
            <ChannelDirectory serverId={params.serverId!} />
          </Show>
        }
      >
        {/* Channel selected: show messages */}
        <div class={styles.channelView}>
          <Show
            when={conversationId()}
            fallback={
              <div class={styles.emptyState}>
                <p class={styles.emptyStateText}>Select a channel to start chatting</p>
              </div>
            }
          >
            {(convId) => (
              <>
                {/* Channel header - stripped down */}
                <div class={styles.channelHeader}>
                  <h2 class={styles.channelName}>{currentChannel()?.name}</h2>
                  <Show when={currentChannel()?.topic}>
                    <span class={styles.channelTopic}>
                      {currentChannel()?.topic}
                    </span>
                  </Show>

                  {/* Header action buttons */}
                  <div class={styles.headerActions}>
                    <button
                      data-testid="search-button"
                      title="Search"
                      aria-label="Search"
                      class={`${styles.headerBtn}${modals.showSearch ? ` ${styles.headerBtnActive}` : ''}`}
                      onClick={() => modals.toggleSearch()}
                    >
                      <SearchIcon />
                    </button>
                    <button
                      data-testid="channel-settings-button"
                      title="Channel Settings"
                      aria-label="Channel Settings"
                      class={`${styles.headerBtn}${modals.showChannelSettings ? ` ${styles.headerBtnActive}` : ''}`}
                      onClick={() => modals.toggleChannelSettings()}
                    >
                      <GearIcon />
                    </button>
                    <Show when={authStore.user?.isAdmin}>
                      <button
                        data-testid="server-settings-button"
                        title="Server Settings"
                        aria-label="Server Settings"
                        class={`${styles.headerBtn}${modals.showServerSettings ? ` ${styles.headerBtnActive}` : ''}`}
                        onClick={() => modals.openServerSettings()}
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.headerIcon}>
                          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                        </svg>
                      </button>
                    </Show>
                  </div>
                </div>

                {/* Messages + right panels row */}
                <div class={styles.contentRow}>
                  {/* Messages area */}
                  <div class={styles.messagesArea}>
                    <Show when={currentChannel()?.capabilities && hasCapability(currentChannel()!.capabilities, Capability.Forum)}>
                      <Show when={!modals.selectedForumPost}>
                        <ForumPostList
                          serverId={params.serverId!}
                          channelId={params.channelId!}
                          onSelectPost={(post) => modals.selectForumPost(post)}
                        />
                      </Show>
                      <Show when={modals.selectedForumPost}>
                        {(post) => (
                          <div data-testid="forum-thread-view" class={styles.forumThreadView}>
                            {/* Thread header with back button */}
                            <div class={styles.forumThreadHeader}>
                              <button
                                data-testid="forum-back-button"
                                class={styles.forumBackButton}
                                onClick={() => modals.selectForumPost(null)}
                                aria-label="Back to forum posts"
                              >
                                &larr; Back
                              </button>
                              <h3 class={styles.forumPostTitle}>{post().title}</h3>
                            </div>
                            <MessageList conversationId={post().conversationId} />
                            <MessageCompose conversationId={post().conversationId} channelId={params.channelId} />
                          </div>
                        )}
                      </Show>
                    </Show>
                    <Show when={!currentChannel()?.capabilities || !hasCapability(currentChannel()!.capabilities, Capability.Forum)}>
                      <ScreenShareViewer />
                      <MessageList conversationId={convId()} />
                      <TypingIndicator conversationId={convId()} />
                      <Show
                        when={messageStore.editingMessageId}
                        fallback={<MessageCompose conversationId={convId()} channelId={params.channelId} />}
                      >
                        {/* Edit bar replaces compose while editing */}
                        <div class={styles.editBarWrapper}>
                          <div class={styles.editBarInner}>
                            <div class={styles.editBarLabel}>Editing message</div>
                            <textarea
                              data-testid="message-edit-textarea"
                              class={styles.editTextarea}
                              value={messageStore.editContent}
                              onInput={(e) => messageStore.setEditContent((e.target as HTMLTextAreaElement).value)}
                              onKeyDown={(e) => {
                                if (e.key === 'Enter' && !e.shiftKey) {
                                  e.preventDefault();
                                  messageStore.editMessage(convId(), messageStore.editingMessageId!, messageStore.editContent);
                                }
                                if (e.key === 'Escape') messageStore.cancelEditing();
                              }}
                              rows={2}
                            />
                            <div class={styles.editBarHint}>Enter to save, Escape to cancel</div>
                          </div>
                        </div>
                      </Show>
                    </Show>
                  </div>

                  {/* Right-side panels */}
                  <Show when={modals.showSearch}>
                    <div class={styles.rightPanel}>
                      <SearchPanel />
                    </div>
                  </Show>
                  <Show when={modals.showPins && conversationId()}>
                    <div class={styles.rightPanel}>
                      <PinList conversationId={conversationId()!} />
                    </div>
                  </Show>
                  <Show when={modals.showThreads}>
                    <div class={styles.rightPanel}>
                      <ThreadPanel channelId={params.channelId || ''} />
                    </div>
                  </Show>
                  <Show when={modals.showEvents && serverStore.selectedServerId}>
                    <div class={styles.rightPanel}>
                      <ScheduledEvents serverId={serverStore.selectedServerId!} />
                    </div>
                  </Show>
                  <Show when={modals.showScheduledMessages && channelStore.selectedChannelId}>
                    <div class={styles.rightPanel}>
                      <ScheduledMessages channelId={channelStore.selectedChannelId!} />
                    </div>
                  </Show>
                </div>
              </>
            )}
          </Show>
        </div>
      </Show>

      {/* Settings modal */}
      <Modal open={modals.showSettings !== null} onClose={() => modals.closeSettings()} aria-label="User Settings" size="lg">
        <div id="settings-modal-panel">
        <div class={styles.settingsTabs}>
          <button
            data-testid="settings-tab-profile"
            class={`${styles.settingsTab}${modals.showSettings === 'profile' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('profile')}
          >
            Profile
          </button>
          <button
            data-testid="settings-tab-notifications"
            class={`${styles.settingsTab}${modals.showSettings === 'notifications' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notifications')}
          >
            Notifications
          </button>
          <button
            class={`${styles.settingsTab}${modals.showSettings === 'blocks' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('blocks')}
          >
            Blocked Users
          </button>
          <button
            class={`${styles.settingsTab}${modals.showSettings === 'notes' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notes')}
          >
            User Notes
          </button>
        </div>
        <Show when={modals.showSettings === 'profile'}><UserProfileEditor /></Show>
        <Show when={modals.showSettings === 'notifications'}><NotificationSettings /></Show>
        <Show when={modals.showSettings === 'blocks'}><BlockList /></Show>
        <Show when={modals.showSettings === 'notes'}><UserNotes /></Show>
        </div>
      </Modal>

      {/* Channel Settings modal */}
      <Show when={modals.showChannelSettings && channelStore.selectedChannelId && serverStore.selectedServerId}>
        <ChannelSettings
          serverId={serverStore.selectedServerId!}
          channelId={channelStore.selectedChannelId!}
          onClose={() => modals.closeChannelSettings()}
        />
      </Show>

      {/* Group Manager modal */}
      <Modal data-testid="group-manager-modal" open={modals.showGroupManager && !!serverStore.selectedServerId} onClose={() => modals.closeGroupManager()} aria-label="Group Manager" size="xl">
        <div class={styles.groupManagerContent}>
          <GroupManager serverId={serverStore.selectedServerId!} />
        </div>
      </Modal>

      {/* Server Settings modal */}
      <Show when={modals.showServerSettings && serverStore.selectedServerId}>
        <ServerSettings
          serverId={serverStore.selectedServerId!}
          onClose={() => modals.closeServerSettings()}
        />
      </Show>
    </div>
  );
}
