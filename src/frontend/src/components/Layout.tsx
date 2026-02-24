import { Show, createEffect, createSignal, onMount } from 'solid-js';
import { useParams, useNavigate } from '@solidjs/router';
import ServerSidebar from './ServerSidebar';
import ChannelSidebar from './ChannelSidebar';
import MessageList from './MessageList';
import MessageCompose from './MessageCompose';
import TypingIndicator from './TypingIndicator';
import MemberList from './MemberList';
import DmList from './DmList';
import FriendList from './FriendList';
import SearchPanel from './SearchPanel';
import PinList from './PinList';
import ThreadPanel from './ThreadPanel';
import ForumPostList from './ForumPostList';
import UserProfileEditor from './UserProfileEditor';
import BlockList from './BlockList';
import NotificationSettings from './NotificationSettings';
import ChannelSettings from './ChannelSettings';
import RoleManager from './RoleManager';
import ScheduledEvents from './ScheduledEvents';
import UserNotes from './UserNotes';
import ConnectedAccounts from './ConnectedAccounts';
import ProfileDecorations from './ProfileDecorations';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useMembers } from '../stores/member.store';
import { useMessages } from '../stores/message.store';
import { useDms } from '../stores/dm.store';
import { useSignalR } from '../stores/signalr.store';
import { useUnread } from '../stores/unread.store';
import { useForums } from '../stores/forum.store';
import type { ForumPost } from '../types/forum';

export default function Layout() {
  const params = useParams<{ serverId?: string; channelId?: string }>();
  const navigate = useNavigate();
  const serverStore = useServers();
  const channelStore = useChannels();
  const memberStore = useMembers();
  const messageStore = useMessages();
  const dmStore = useDms();
  const signalR = useSignalR();

  const unreadStore = useUnread();
  const forumStore = useForums();
  const [showSearch, setShowSearch] = createSignal(false);
  const [showPins, setShowPins] = createSignal(false);
  const [showThreads, setShowThreads] = createSignal(false);
  const [showSettings, setShowSettings] = createSignal<'profile' | 'blocks' | 'notifications' | 'notes' | 'connected-accounts' | 'profile-decorations' | null>(null);
  const [showChannelSettings, setShowChannelSettings] = createSignal(false);
  const [showRoleManager, setShowRoleManager] = createSignal(false);
  const [showEvents, setShowEvents] = createSignal(false);
  const [selectedForumPost, setSelectedForumPost] = createSignal<ForumPost | null>(null);

  // Load servers, DMs, and connect to SignalR on mount
  onMount(() => {
    serverStore.fetchServers();
    dmStore.loadDms();
    // Connect to the real-time hub so message/presence/typing events are received.
    // After connecting, join the current conversation in case the channels effect
    // fired before SignalR was ready (a common race on initial page load).
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
      // Fetch channels for the server.  If the request fails with a 403/forbidden
      // error the user is no longer a member (e.g. they were banned).  Redirect
      // them to the DM view rather than leaving them on an inaccessible channel.
      // The backend returns RFC 7807 Problem Details on errors, so we check
      // `err.status` (HTTP status code) and `err.title` (error code).
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
          navigate('/channels/me', { replace: true });
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
      setSelectedForumPost(null);
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
  // when the SignalR connection is (re)established. Reading signalR.isConnected
  // makes this effect reactive to connection state changes, so the conversation is
  // joined even when SignalR connects after the channels have already loaded.
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

    // Always keep the active conversation ID current — used by Chat_MessageCreated
    // handler to determine which conversation is currently being viewed.
    if (conversationChanged) {
      signalR.setActiveConversationId(convId ?? null);
      // Clear unread indicator immediately when the user navigates to a channel.
      // The server-side read-state will be updated the next time MessageList loads
      // messages and the user scrolls to the bottom.
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
    <div class="h-screen flex bg-xcord-bg-primary text-xcord-text-primary" data-signalr-connected={String(signalR.isConnected)} data-signalr-conversation-joined={String(conversationJoined())}>
      {/* Server sidebar */}
      <ServerSidebar />

      {/* Channel sidebar or DM list */}
      <Show when={params.serverId} fallback={<DmList />}>
        <ChannelSidebar />
      </Show>

      {/* Main content area */}
      <Show
        when={params.serverId}
        fallback={
          <div class="flex-1 flex flex-col min-w-0">
            <Show
              when={dmStore.selectedDmId}
              fallback={<FriendList />}
            >
              {(selectedDmId) => {
                const selectedChannel = () =>
                  dmStore.dmChannels.find((c) => c.id === selectedDmId()) ??
                  dmStore.dmGroups.find((g) => g.id === selectedDmId());
                const dmConvId = () => selectedChannel()?.conversationId;
                const dmName = () => {
                  const ch = selectedChannel();
                  if (!ch) return '';
                  if ('recipientUsername' in ch) return ch.recipientUsername;
                  return ch.name;
                };
                return (
                  <Show when={dmConvId()}>
                    {(convId) => (
                      <div class="flex flex-col h-full">
                        {/* DM header */}
                        <div class="h-12 px-4 flex items-center border-b border-xcord-border shadow-sm bg-xcord-bg-primary">
                          <h2 class="font-semibold text-white" id="dm-conversation-header">{dmName()}</h2>
                        </div>
                        {/* Messages */}
                        <MessageList conversationId={convId()} />
                        <TypingIndicator conversationId={convId()} />
                        <MessageCompose conversationId={convId()} />
                      </div>
                    )}
                  </Show>
                );
              }}
            </Show>
          </div>
        }
      >
        <div class="flex-1 flex flex-col min-w-0">
          <Show
            when={conversationId()}
            fallback={
              <div class="flex-1 flex items-center justify-center">
                <p class="text-xcord-text-muted">Select a channel to start chatting</p>
              </div>
            }
          >
            {(convId) => (
              <>
                {/* Channel header */}
                <div class="h-12 px-4 flex items-center border-b border-xcord-border shadow-sm bg-xcord-bg-primary">
                  <span class="text-xcord-text-muted mr-2">#</span>
                  <h2 class="font-semibold text-white">{currentChannel()?.name}</h2>
                  <Show when={currentChannel()?.topic}>
                    <span class="ml-4 text-sm text-xcord-text-muted border-l border-xcord-border pl-4">
                      {currentChannel()?.topic}
                    </span>
                  </Show>

                  {/* Header action buttons */}
                  <div class="ml-auto flex items-center space-x-2">
                    <button
                      title="Search"
                      class={`px-2 py-1 text-sm rounded transition ${showSearch() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowSearch(!showSearch())}
                    >
                      &#128269;
                    </button>
                    <button
                      title="Pinned Messages"
                      class={`px-2 py-1 text-sm rounded transition ${showPins() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowPins(!showPins())}
                    >
                      &#128204;
                    </button>
                    <button
                      title="Threads"
                      class={`px-2 py-1 text-sm rounded transition ${showThreads() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowThreads(!showThreads())}
                    >
                      &#35;&#xFE0F;&#8203;
                    </button>
                    <button
                      title="Channel Settings"
                      aria-label="Channel Settings"
                      class={`px-2 py-1 text-sm rounded transition ${showChannelSettings() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowChannelSettings(!showChannelSettings())}
                    >
                      &#9965;
                    </button>
                    <button
                      title="Roles"
                      aria-label="Role Manager"
                      class={`px-2 py-1 text-sm rounded transition ${showRoleManager() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowRoleManager(!showRoleManager())}
                    >
                      &#127775;
                    </button>
                    <button
                      title="Scheduled Events"
                      aria-label="Scheduled Events"
                      class={`px-2 py-1 text-sm rounded transition ${showEvents() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowEvents(!showEvents())}
                    >
                      &#128197;
                    </button>
                    <button
                      title="Settings"
                      class={`px-2 py-1 text-sm rounded transition ${showSettings() ? 'text-white bg-xcord-bg-secondary' : 'text-xcord-text-muted hover:text-white hover:bg-xcord-bg-secondary'}`}
                      onClick={() => setShowSettings(showSettings() ? null : 'profile')}
                    >
                      &#9881;&#65039;
                    </button>
                  </div>
                </div>

                {/* Messages + right panels row */}
                <div class="flex-1 flex min-h-0">
                  {/* Messages area */}
                  <div class="flex-1 flex flex-col min-w-0">
                    <Show when={currentChannel()?.type === 'Forum'}>
                      <Show when={!selectedForumPost()}>
                        <ForumPostList
                          serverId={params.serverId!}
                          channelId={params.channelId!}
                          onSelectPost={(post) => setSelectedForumPost(post)}
                        />
                      </Show>
                      <Show when={selectedForumPost()}>
                        {(post) => (
                          <div class="flex flex-col h-full min-h-0">
                            {/* Thread header with back button */}
                            <div class="h-12 px-4 flex items-center border-b border-xcord-border shadow-sm bg-xcord-bg-primary flex-shrink-0">
                              <button
                                class="text-xcord-brand hover:underline text-sm mr-3"
                                onClick={() => setSelectedForumPost(null)}
                                aria-label="Back to forum posts"
                              >
                                &larr; Back
                              </button>
                              <h3 class="font-semibold text-white truncate">{post().title}</h3>
                            </div>
                            <MessageList conversationId={post().conversationId} />
                            <MessageCompose conversationId={post().conversationId} channelId={params.channelId} />
                          </div>
                        )}
                      </Show>
                    </Show>
                    <Show when={currentChannel()?.type !== 'Forum'}>
                    <MessageList conversationId={convId()} />
                    <TypingIndicator conversationId={convId()} />
                    <Show
                      when={messageStore.editingMessageId}
                      fallback={<MessageCompose conversationId={convId()} channelId={params.channelId} />}
                    >
                      {/* Edit bar replaces compose while editing — textarea is last in DOM so .last() finds it */}
                      <div class="px-4 pb-6">
                        <div class="bg-xcord-bg-primary rounded-lg px-4 py-3 flex flex-col gap-1">
                          <div class="text-xs text-xcord-text-muted mb-1">Editing message</div>
                          <textarea
                            class="bg-xcord-bg-tertiary text-xcord-text-primary text-sm rounded p-2 border border-xcord-border resize-none outline-none"
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
                          <div class="text-xs text-xcord-text-muted">Enter to save, Escape to cancel</div>
                        </div>
                      </div>
                    </Show>
                    </Show>
                  </div>

                  {/* Right-side panels */}
                  <Show when={showSearch()}>
                    <div class="w-80 border-l border-xcord-border bg-xcord-bg-secondary overflow-y-auto">
                      <SearchPanel />
                    </div>
                  </Show>
                  <Show when={showPins() && conversationId()}>
                    <div class="w-80 border-l border-xcord-border bg-xcord-bg-secondary overflow-y-auto">
                      <PinList conversationId={conversationId()!} />
                    </div>
                  </Show>
                  <Show when={showThreads()}>
                    <div class="w-80 border-l border-xcord-border bg-xcord-bg-secondary overflow-y-auto">
                      <ThreadPanel channelId={params.channelId || ''} />
                    </div>
                  </Show>
                  <Show when={showEvents() && serverStore.selectedServerId}>
                    <div class="w-80 border-l border-xcord-border bg-xcord-bg-secondary overflow-y-auto">
                      <ScheduledEvents serverId={serverStore.selectedServerId!} />
                    </div>
                  </Show>
                </div>
              </>
            )}
          </Show>
        </div>
      </Show>

      {/* Member list */}
      <Show when={serverStore.selectedServerId}>
        <MemberList />
      </Show>

      {/* Settings modal */}
      <Show when={showSettings()}>
        <div
          class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center"
          onClick={() => setShowSettings(null)}
        >
          <div
            id="settings-modal-panel"
            class="bg-xcord-bg-secondary rounded-lg w-[600px] max-h-[80vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div class="flex border-b border-xcord-border">
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'profile' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('profile')}
              >
                Profile
              </button>
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'notifications' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('notifications')}
              >
                Notifications
              </button>
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'blocks' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('blocks')}
              >
                Blocked Users
              </button>
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'notes' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('notes')}
              >
                User Notes
              </button>
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'connected-accounts' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('connected-accounts')}
              >
                Connected Accounts
              </button>
              <button
                class={`px-4 py-3 text-sm ${showSettings() === 'profile-decorations' ? 'text-white border-b-2 border-xcord-brand' : 'text-xcord-text-muted hover:text-white'}`}
                onClick={() => setShowSettings('profile-decorations')}
              >
                Profile Decorations
              </button>
            </div>
            <Show when={showSettings() === 'profile'}><UserProfileEditor /></Show>
            <Show when={showSettings() === 'notifications'}><NotificationSettings /></Show>
            <Show when={showSettings() === 'blocks'}><BlockList /></Show>
            <Show when={showSettings() === 'notes'}><UserNotes /></Show>
            <Show when={showSettings() === 'connected-accounts'}><ConnectedAccounts /></Show>
            <Show when={showSettings() === 'profile-decorations'}><ProfileDecorations /></Show>
          </div>
        </div>
      </Show>

      {/* Channel Settings modal */}
      <Show when={showChannelSettings() && channelStore.selectedChannelId && serverStore.selectedServerId}>
        <ChannelSettings
          serverId={serverStore.selectedServerId!}
          channelId={channelStore.selectedChannelId!}
          onClose={() => setShowChannelSettings(false)}
        />
      </Show>

      {/* Role Manager modal */}
      <Show when={showRoleManager() && serverStore.selectedServerId}>
        <div
          class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center"
          onClick={() => setShowRoleManager(false)}
        >
          <div
            class="bg-xcord-bg-secondary rounded-lg w-[720px] h-[600px] overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            <RoleManager serverId={serverStore.selectedServerId!} />
          </div>
        </div>
      </Show>
    </div>
  );
}
