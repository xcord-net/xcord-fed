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

/** The message a reply points at, denormalized onto the reply so the quote line
 *  renders without a second fetch. `isDeleted` means the parent is gone: the id is
 *  still present (the reply edge holds) but there is no author or preview. */
export interface ReplyTo {
  id: string;
  authorId?: string;
  authorUsername?: string;
  preview: string;
  isDeleted: boolean;
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
  replyTo?: ReplyTo;
  isPinned: boolean;
  editedAt?: string;
  createdAt: string;
  nonce?: string;
  embeds?: MessageEmbed[];
  reactions?: MessageReaction[];
  attachments?: MessageAttachment[];
  pollId?: string;
}
