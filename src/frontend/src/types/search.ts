import type { Message } from './message';

export interface SearchFilters {
  query: string;
  authorId?: string;
  mentionsUserId?: string;
  hasLink?: boolean;
  hasEmbed?: boolean;
  hasAttachment?: boolean;
  cursor?: string;
  channelId?: string;
}

export interface SearchResult {
  messages: Message[];
  totalCount?: number;
  hasMore: boolean;
  nextCursor?: string | null;
}
