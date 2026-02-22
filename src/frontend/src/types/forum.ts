export interface ForumPost {
  id: string;
  channelId: string;
  conversationId: string;
  authorId?: string;
  authorUsername?: string;
  authorAvatarUrl?: string;
  title: string;
  tags: string[];
  isPinned?: boolean;
  isLocked?: boolean;
  messageCount?: number;
  lastMessageAt?: string;
  createdAt: string;
}

export interface ForumTag {
  id: string;
  channelId: string;
  name: string;
  emoji?: string;
  moderated: boolean;
}
