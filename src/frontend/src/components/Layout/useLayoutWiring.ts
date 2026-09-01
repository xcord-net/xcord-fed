import { createEffect, createSignal, onMount } from 'solid-js';
import { useNavigate, useParams } from '@solidjs/router';
import { api } from '../../api/client';
import { useAuth } from '../../stores/auth.store';
import { useChannels } from '../../stores/channel.store';
import { useDms } from '../../stores/dm.store';
import { useMembers } from '../../stores/member.store';
import { useMessages } from '../../stores/message.store';
import { useModals } from '../../stores/modal.store';
import { useServers } from '../../stores/server.store';
import { useSignalR } from '../../stores/signalr.store';
import { useUnread } from '../../stores/unread.store';
import { requestPermission } from '../../services/notification.service';

/**
 * Wires up Layout-level side effects: document title, server/channel/conversation
 * routing sync, SignalR connect/join/leave, and hub URL fetching.
 *
 * Returns the hubUrl signal getter (used to render the optional HubHeader).
 */
export function useLayoutWiring() {
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
  const dmStore = useDms();

  const [hubUrl, setHubUrl] = createSignal<string | null>(null);

  // Tell the SignalR store who the local user is. Its Chat_TypingStarted and
  // Chat_MessageCreated handlers compare against this to drop the user's own
  // typing echo and to suppress notifications for their own messages. Kept in an
  // effect so it also lands when auth resolves after this hook mounts.
  createEffect(() => {
    signalR.setCurrentUserId(authStore.user?.id ?? null);
  });

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
    // Fetch public config to determine if a hub header should be shown
    api.get<{ hubUrl: string | null }>('/api/v1/config')
      .then(config => setHubUrl(config.hubUrl ?? null))
      .catch(() => {});
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

  // Update selected server when route changes (skip for DM view)
  let prevServerId: string | undefined;
  createEffect(() => {
    const serverId = params.serverId;
    if (serverId === 'me') {
      prevServerId = serverId;
      dmStore.loadDms();
      return;
    }
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

  return { hubUrl };
}
