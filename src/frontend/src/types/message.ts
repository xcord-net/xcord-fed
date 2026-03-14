export interface MessageEmbed {
  url?: string;
  title?: string;
  description?: string;
  imageUrl?: string;
  siteName?: string;
  color?: string;
}

export interface MessageReaction {
  emoji: string;
  count: number;
  userIds: string[];
}

export interface MessageAttachment {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  width?: number;
  height?: number;
  downloadUrl: string;
  thumbnailUrl?: string;
}

export interface Message {
  id: string;
  conversationId: string;
  authorId: string;
  authorUsername?: string;
  authorAvatarUrl?: string;
  authorGroupColor?: string;
  type: string;
  content: string;
  metadata?: Record<string, unknown>;
  replyToId?: string;
  isPinned: boolean;
  editedAt?: string;
  createdAt: string;
  nonce?: string;
  embeds?: MessageEmbed[];
  reactions?: MessageReaction[];
  attachments?: MessageAttachment[];
  pollId?: string;
}
