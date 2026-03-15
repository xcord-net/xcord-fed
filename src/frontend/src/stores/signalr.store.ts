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
import { handleNewMessageNotification } from '../services/notification.service';
import type { PresenceStatus } from '../types/presence';
import type { Message } from '../types/message';
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
  'Chat_TypingStarted',
  'Chat_ChannelCreated',
  'Presence_Updated',
  'Voice_StateUpdated',
  'Notify_UnreadUpdated',
  'System_ShuttingDown',
] as const;

export function useSignalR() {
  const presence = usePresence();
  const typing = useTyping();
  const voice = useVoice();
  const unread = useUnread();
  const messages = useMessages();
  const channels = useChannels();

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

    connection.on('Chat_TypingStarted', (data: { conversationId: string; userId: string }) => {
      typing.startTyping(data.conversationId, data.userId);
    });

    // Channel events - broadcast to all server members when a channel is created
    connection.on('Chat_ChannelCreated', (channel: Channel) => {
      const normalized = normalizeIds(
        channel as unknown as Record<string, unknown>,
        'id', 'serverId', 'conversationId', 'categoryId',
      ) as unknown as Channel;
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

    // Notification events
    connection.on('Notify_UnreadUpdated', (data: { conversationId: string; count: number; lastMessageId: string }) => {
      unread.updateUnread(data.conversationId, data.count, data.lastMessageId);
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
        console.log('SignalR reconnected');
        store.setIsConnected(true);
        // Re-inject connection reference so voice store can invoke hub methods again.
        voice.setSignalRConnection(connection);
        await rejoinConversations(connection);
        await sendHeartbeat(connection);
      });

      // Handle reconnecting
      connection.onreconnecting((error) => {
        console.log('SignalR reconnecting...', error);
        store.setIsConnected(false);
      });

      // Handle close - auto-reconnect has been exhausted; build a new connection
      // with a fresh auth ticket. The old connection object is discarded, so its
      // WeakSet entry is eligible for GC.
      connection.onclose(async (error) => {
        console.log('SignalR connection closed', error);
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
      store.setIsConnected(true);
      // Provide voice store with the connection so it can invoke hub methods.
      voice.setSignalRConnection(connection);
      console.log('SignalR connected');

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
