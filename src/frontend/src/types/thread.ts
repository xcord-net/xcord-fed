export interface Thread {
  id: string;
  conversationId: string;
  channelId: string;
  parentMessageId?: string;
  parentConversationId?: string;
  name: string;
  archived: boolean;
  locked: boolean;
  messageCount: number;
  memberCount: number;
  lastMessageAt?: string;
  createdAt: string;
}
