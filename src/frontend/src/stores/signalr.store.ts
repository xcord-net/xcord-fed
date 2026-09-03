import { createSignal, createRoot } from 'solid-js';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { api } from '../api/client';
import { normalizeIds } from '../utils/snowflake';
import { usePresence } from './presence.store';
import { useTyping } from './typing.store';
import { useVoice } from './voice.store';
import { useUnread } from './unread.store';
import { useMessages } from './message.store';
import { useChannels } from './channel.store';
import { useFriends } from './friend.store';
import { useDms } from './dm.store';
import { useBroadcast } from './broadcast.store';
import { useMembers } from './member.store';
import { usePolls } from './poll.store';
import { handleNewMessageNotification } from '../services/notification.service';
import type { PresenceStatus } from '../types/presence';
import type { Message, MessageAttachment } from '../types/message';
import type { Channel } from '../types/channel';

interface TicketResponse {
  ticket: string;
  expiresAt: string;
}

// Tracks which connection objects have already had event handlers registered.
// Using WeakSet ensures connections can be garbage-collected once they close.
const handlersRegistered = new WeakSet<HubConnection>();

const store = createRoot(() => {
  const [connection, setConnection] = createSignal<HubConnection | null>(null);
  const [isConnected, setIsConnected] = createSignal(false);
  const [isConnecting, setIsConnecting] = createSignal(false);
  const [currentConversations, setCurrentConversations] = createSignal<Set<string>>(new Set());
  // Notification context - updated by callers so the Chat_MessageCreated handler
  // can decide whether to suppress sound/desktop notifications.
  const [currentUserId, setCurrentUserId] = createSignal<string | null>(null);
  const [activeConversationId, setActiveConversationId] = createSignal<string | null>(null);
  // Set when the server sends System_ShuttingDown - non-null value triggers the
  // suspension overlay so users see a friendly message before the connection drops.
  const [suspensionReason, setSuspensionReason] = createSignal<string | null>(null);

  return {
    connection,
    setConnection,
    isConnected,
    setIsConnected,
    isConnecting,
    setIsConnecting,
    currentConversations,
    setCurrentConversations,
    currentUserId,
    setCurrentUserId,
    activeConversationId,
    setActiveConversationId,
    suspensionReason,
    setSuspensionReason,
  };
});

// The event names used by this application. Listed here so that
// unregisterEventHandlers can remove them all in one pass.
const SIGNALR_EVENTS = [
  'Chat_MessageCreated',
  'Chat_MessageUpdated',
  'Chat_MessageDeleted',
  'Chat_MessageEmbedded',
  'Chat_ReactionsUpdated',
  'Chat_AttachmentsUpdated',
  'Poll_Created',
  'Poll_Voted',
  'Chat_TypingStarted',
  'Chat_TypingStopped',
  'Chat_ChannelCreated',
  'Member_Joined',
  'Member_Left',
  'Member_Kicked',
  'Presence_Updated',
  'Voice_StateUpdated',
  'Notify_UnreadUpdated',
  'Notify_FriendRequest',
  'Notify_FriendAccepted',
  'Broadcast_Started',
  'Broadcast_Ended',
  'Broadcast_LayoutChanged',
  'Broadcast_StreambotsChanged',
  'Broadcast_StageChanged',
  'Broadcast_StatusChanged',
  'Broadcast_StreambotStatusChanged',
  'System_ShuttingDown',
] as const;

export function useSignalR() {
  const presence = usePresence();
  const typing = useTyping();
  const voice = useVoice();
  const unread = useUnread();
  const messages = useMessages();
  const channels = useChannels();
  const members = useMembers();
  const polls = usePolls();

  async function getTicket(): Promise<string> {
    const response = await api.post<TicketResponse>('/api/v1/auth/ws-ticket');
    return response.ticket;
  }

  // Remove all application event handlers from a connection. Called before
  // re-registering to prevent duplicate subscriptions from accumulating across
  // reconnect cycles.
  function unregisterEventHandlers(connection: HubConnection): void {
    for (const event of SIGNALR_EVENTS) {
      connection.off(event);
    }
  }

  // Register all application event handlers on the connection exactly once.
  // If handlers are already registered on this connection object (tracked via
  // the module-level WeakSet), this is a no-op to prevent double-registration
  // during auto-reconnect, where SignalR reuses the same HubConnection instance.
  function registerEventHandlers(connection: HubConnection): void {
    if (handlersRegistered.has(connection)) {
      return;
    }

    // Remove any stale handlers first (defensive - covers edge cases where the
    // WeakSet entry was cleared but the connection object was reused).
    unregisterEventHandlers(connection);

    // Chat events
    connection.on('Chat_MessageCreated', (message: Message) => {
      // Add message to store if it belongs to the active conversation.
      // We check both the message list (if already loaded) and the active conversation ID
      // so that real-time messages arrive even when the list is empty (e.g. first message).
      const activeConvId = store.activeConversationId();
      const currentMessages = messages.messages;
      const messageConvId = String(message.conversationId);
      const isForActiveConv = activeConvId
        ? messageConvId === String(activeConvId)
        : (currentMessages.length > 0 && currentMessages[0]?.conversationId === messageConvId);

      if (isForActiveConv) {
        // Check if we already have this message (by nonce or id)
        const msgId = String(message.id);
        const exists = currentMessages.some(
          (m: Message) => String(m.id) === msgId || (m.nonce && m.nonce === message.nonce)
        );
        if (!exists) {
          messages.addMessage(message);
        }
      }

      // Fire sound/desktop notification if appropriate.
      handleNewMessageNotification(message, {
        currentUserId: store.currentUserId(),
        activeConversationId: store.activeConversationId(),
      });
    });

    connection.on('Chat_MessageUpdated', (message: Message) => {
      messages.updateMessage(message);
    });

    connection.on('Chat_MessageDeleted', (data: { conversationId: string; messageId: string }) => {
      messages.removeMessage(data.messageId);
    });

    connection.on('Chat_MessageEmbedded', (message: Message) => {
      messages.updateMessage(message);
    });

    // Reactions changed on a message someone else is also looking at. The whole
    // set is sent, so this replaces rather than increments - a client can never
    // be left holding a count that will not converge.
    connection.on('Chat_ReactionsUpdated', (data: {
      conversationId: string;
      messageId: string;
      reactions: { emoji: string; count: number; userIds: string[] }[];
    }) => {
      const { messageId } = normalizeIds(data, 'messageId', 'conversationId');
      messages.setReactions(messageId, (data.reactions ?? []).map((r) => ({
        emoji: r.emoji,
        count: r.count,
        userIds: (r.userIds ?? []).map(String),
      })));
    });

    // A thumbnail finished generating for a message already on screen.
    connection.on('Chat_AttachmentsUpdated', (data: {
      conversationId: string;
      messageId: string;
      attachments: MessageAttachment[];
    }) => {
      const { messageId } = normalizeIds(data, 'messageId', 'conversationId');
      messages.setAttachments(messageId, (data.attachments ?? []).map((a) => ({
        ...a,
        id: String(a.id),
      })));
    });

    // A poll is a message, and the server announces it under its own name - so
    // without this a poll appeared only for whoever created it, and everyone
    // else saw the conversation simply skip it.
    connection.on('Poll_Created', (data: {
      pollId: string;
      messageId: string;
      conversationId: string;
      authorId: string;
      question: string;
    }) => {
      const { pollId, messageId, conversationId, authorId } =
        normalizeIds(data, 'pollId', 'messageId', 'conversationId', 'authorId');
      const activeConvId = store.activeConversationId();
      if (activeConvId && String(activeConvId) !== conversationId) return;
      if (messages.messages.some((m) => m.id === messageId)) return;
      messages.addMessage({
        id: messageId,
        conversationId,
        authorId,
        type: 'PollCreated',
        content: data.question ?? '',
        pollId,
        isPinned: false,
        createdAt: new Date().toISOString(),
      } as Message);
    });

    // Vote counts after the poll was rendered. The poll is fetched once, when
    // its message arrives, so without this the tally never moved for anybody -
    // not even the person who voted.
    connection.on('Poll_Voted', (data: {
      pollId: string;
      conversationId: string;
      options: { id: string; voteCount: number }[];
    }) => {
      const { pollId } = normalizeIds(data, 'pollId', 'conversationId');
      polls.applyTally(pollId, (data.options ?? []).map((o) => ({
        id: String(o.id),
        voteCount: o.voteCount,
      })));
    });

    connection.on('Chat_TypingStarted', (data: { conversationId: string; userId: string }) => {
      // Never show the local user their own typing echo.
      if (data.userId === store.currentUserId()) return;
      typing.startTyping(data.conversationId, data.userId);
    });

    connection.on('Chat_TypingStopped', (data: { conversationId: string; userId: string }) => {
      if (data.userId === store.currentUserId()) return;
      typing.stopTyping(data.conversationId, data.userId);
    });

    // Membership events. The server has always broadcast these; nothing listened,
    // so a roster on screen only changed for whoever performed the action and
    // everyone else kept seeing a member who had gone until they reloaded.
    // Departures carry the user id and can be applied directly; an arrival does
    // not carry the new member, so it is refetched.
    const onMemberGone = (data: { serverId: string; userId: string }) => {
      const { userId } = normalizeIds(data, 'serverId', 'userId');
      members.removeMember(userId);
    };
    connection.on('Member_Left', onMemberGone);
    connection.on('Member_Kicked', onMemberGone);
    connection.on('Member_Joined', (data: { serverId: string; userId: string }) => {
      const { serverId } = normalizeIds(data, 'serverId', 'userId');
      void members.refreshMembers(serverId);
    });

    // Channel events - broadcast to all server members when a channel is created
    connection.on('Chat_ChannelCreated', (channel: Channel) => {
      const normalized = normalizeIds(channel, 'id', 'serverId', 'conversationId', 'categoryId');
      channels.addChannel(normalized);
    });

    // Presence events
    connection.on('Presence_Updated', (data: { userId: string; status: PresenceStatus }) => {
      presence.updatePresence(data.userId, data.status);
    });

    // Voice events
    connection.on('Voice_StateUpdated', (data: { userId: string; channelId: string | null; isMuted: boolean; isDeafened: boolean }) => {
      voice.updateVoiceState(data.userId, data.channelId, data.isMuted, data.isDeafened);
    });

    // Broadcast events - route raw payload to the broadcast store which handles
    // per-event state patching or reload as needed.
    connection.on('Broadcast_Started', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_Started', data);
    });
    connection.on('Broadcast_Ended', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_Ended', data);
    });
    connection.on('Broadcast_LayoutChanged', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_LayoutChanged', data);
    });
    connection.on('Broadcast_StreambotsChanged', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_StreambotsChanged', data);
    });
    connection.on('Broadcast_StageChanged', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_StageChanged', data);
    });
    connection.on('Broadcast_StatusChanged', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_StatusChanged', data);
    });
    connection.on('Broadcast_StreambotStatusChanged', (data: Record<string, unknown>) => {
      useBroadcast()._onSignalREvent('Broadcast_StreambotStatusChanged', data);
    });

    // Notification events
    connection.on('Notify_UnreadUpdated', (data: { conversationId: string; count: number; lastMessageId: string }) => {
      unread.updateUnread(data.conversationId, data.count, data.lastMessageId);
    });

    // DM events
    connection.on('Notify_DmCreated', () => {
      useDms().loadDms();
    });

    // Friend events
    connection.on('Notify_FriendRequest', () => {
      useFriends().loadFriendRequests();
    });
    connection.on('Notify_FriendAccepted', () => {
      useFriends().loadFriends();
      useFriends().loadFriendRequests();
    });

    // System events
    connection.on('System_ShuttingDown', (data: { reason: string; timestamp: string }) => {
      // Display a user-friendly suspension overlay. The connection will drop
      // shortly after the hub stops the container; clients can close the
      // overlay manually or wait for the reconnect cycle to clear it.
      const reason = data?.reason ?? 'maintenance';
      store.setSuspensionReason(reason);
      console.warn('Server is shutting down:', reason);
    });

    // Mark this connection object as having handlers registered.
    handlersRegistered.add(connection);
  }

  async function rejoinConversations(connection: HubConnection): Promise<void> {
    const conversations = store.currentConversations();
    for (const conversationId of conversations) {
      try {
        await connection.invoke('JoinConversation', conversationId);
      } catch (error) {
        console.error(`Failed to rejoin conversation ${conversationId}:`, error);
      }
    }
  }

  async function sendHeartbeat(connection: HubConnection): Promise<void> {
    try {
      await connection.invoke('Heartbeat');
    } catch (error) {
      console.error('Failed to send heartbeat:', error);
    }
  }

  async function connectSignalR(): Promise<void> {
    if (store.isConnecting() || store.isConnected()) {
      return;
    }

    store.setIsConnecting(true);

    try {
      const ticket = await getTicket();
      const baseUrl = api.getBaseUrl() || window.location.origin;

      const connection = new HubConnectionBuilder()
        .withUrl(`${baseUrl}/hubs/main?ticket=${ticket}`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
            if (retryContext.previousRetryCount === 0) return 0;
            if (retryContext.previousRetryCount === 1) return 2000;
            if (retryContext.previousRetryCount === 2) return 10000;
            return 30000;
          },
        })
        .build();

      // Register all event handlers exactly once on this new connection object.
      // Auto-reconnect reuses this same object, so onreconnected must NOT call
      // registerEventHandlers again - the WeakSet guard ensures idempotency if
      // it ever does.
      registerEventHandlers(connection);

      // Handle reconnected - handlers are already registered on the same
      // HubConnection object; do NOT call registerEventHandlers here.
      connection.onreconnected(async () => {
        // Connection reference first: `isConnected` is what everything else
        // waits on, and Solid runs those effects the instant it flips - so
        // announcing readiness before handing the connection over let a waiting
        // join fire against a null reference and fail with "not connected".
        voice.setSignalRConnection(connection);
        store.setIsConnected(true);
        await rejoinConversations(connection);
        await sendHeartbeat(connection);
      });

      // Handle reconnecting
      connection.onreconnecting((error) => {
        store.setIsConnected(false);
      });

      // Handle close - auto-reconnect has been exhausted; build a new connection
      // with a fresh auth ticket. The old connection object is discarded, so its
      // WeakSet entry is eligible for GC.
      connection.onclose(async (error) => {
        store.setIsConnected(false);
        store.setConnection(null);

        // Attempt to reconnect with new ticket
        setTimeout(async () => {
          try {
            await connectSignalR();
          } catch (err) {
            console.error('Failed to reconnect:', err);
          }
        }, 5000);
      });

      // Start connection
      await connection.start();
      store.setConnection(connection);
      // Same ordering as the reconnect path above: publish the connection before
      // announcing that there is one.
      voice.setSignalRConnection(connection);
      store.setIsConnected(true);

      // Send initial heartbeat
      await sendHeartbeat(connection);

    } catch (error) {
      console.error('Failed to connect SignalR:', error);
      store.setIsConnected(false);
      throw error;
    } finally {
      store.setIsConnecting(false);
    }
  }

  return {
    get connection() { return store.connection(); },
    get isConnected() { return store.isConnected(); },
    get isConnecting() { return store.isConnecting(); },
    /** Set of conversation IDs currently joined on the SignalR hub. */
    get currentConversations() { return store.currentConversations(); },
    /** Non-null when the server sent System_ShuttingDown. Cleared on reset/reconnect. */
    get suspensionReason() { return store.suspensionReason(); },

    /** Set the currently authenticated user ID so own-message notifications are suppressed. */
    setCurrentUserId(id: string | null): void {
      store.setCurrentUserId(id);
    },

    /** Set the conversation currently visible in the main pane. */
    setActiveConversationId(id: string | null): void {
      store.setActiveConversationId(id);
    },

    connectSignalR,

    async disconnectSignalR(): Promise<void> {
      const connection = store.connection();
      if (connection && connection.state !== HubConnectionState.Disconnected) {
        try {
          await connection.stop();
        } catch (error) {
          console.error('Error stopping SignalR connection:', error);
        }
      }
      store.setConnection(null);
      store.setIsConnected(false);
      store.setCurrentConversations(new Set<string>());
      voice.setSignalRConnection(null);
    },

    async joinConversation(conversationId: string): Promise<void> {
      const connection = store.connection();
      if (!connection || !store.isConnected()) {
        console.warn('Cannot join conversation: not connected');
        return;
      }

      try {
        await connection.invoke('JoinConversation', conversationId);
        const conversations = new Set<string>(store.currentConversations());
        conversations.add(conversationId);
        store.setCurrentConversations(conversations);
      } catch (error) {
        console.error(`Failed to join conversation ${conversationId}:`, error);
        throw error;
      }
    },

    async leaveConversation(conversationId: string): Promise<void> {
      const connection = store.connection();
      if (!connection || !store.isConnected()) {
        return;
      }

      try {
        await connection.invoke('LeaveConversation', conversationId);
        const conversations = new Set<string>(store.currentConversations());
        conversations.delete(conversationId);
        store.setCurrentConversations(conversations);
        typing.clearTyping(conversationId);
      } catch (error) {
        console.error(`Failed to leave conversation ${conversationId}:`, error);
      }
    },

    async sendTyping(conversationId: string): Promise<void> {
      const connection = store.connection();
      if (!connection || !store.isConnected()) {
        return;
      }

      try {
        await connection.invoke('StartTyping', conversationId);
      } catch (error) {
        console.error('Failed to send typing indicator:', error);
      }
    },

    /** Withdraw the typing notice, when the composer is emptied. */
    async sendStoppedTyping(conversationId: string): Promise<void> {
      const connection = store.connection();
      if (!connection || !store.isConnected()) {
        return;
      }

      try {
        await connection.invoke('StopTyping', conversationId);
      } catch (error) {
        console.error('Failed to withdraw typing indicator:', error);
      }
    },

    async updatePresence(status: PresenceStatus): Promise<void> {
      const connection = store.connection();
      if (!connection || !store.isConnected()) {
        return;
      }

      try {
        await connection.invoke('UpdateStatus', status);
      } catch (error) {
        console.error('Failed to update presence:', error);
      }
    },

    async reset(): Promise<void> {
      const connection = store.connection();
      if (connection && connection.state !== HubConnectionState.Disconnected) {
        try {
          await connection.stop();
        } catch (error) {
          console.error('Error stopping SignalR connection during reset:', error);
        }
      }
      store.setConnection(null);
      store.setIsConnected(false);
      store.setIsConnecting(false);
      store.setCurrentConversations(new Set<string>());
      store.setCurrentUserId(null);
      store.setActiveConversationId(null);
      store.setSuspensionReason(null);
      voice.setSignalRConnection(null);
    },
  };
}
