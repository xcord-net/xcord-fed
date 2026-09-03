import { api } from './client';

/** Mirrors DeckConversationDto. Snowflake ids arrive as strings. */
export interface DeckConversationDto {
  id: string;
  conversationId: string;
  name: string;
  /** Text | Voice | Forum | Dm */
  kind: string;
  unread: number;
  mentions: number;
  liveVoiceCount: number;
}

export interface DeckCommunityDto {
  id: string;
  name: string;
  iconUrl?: string;
  conversations: DeckConversationDto[];
}

export interface DeckResponse {
  communities: DeckCommunityDto[];
  directMessages: DeckConversationDto[];
  totalUnread: number;
  totalMentions: number;
}

/**
 * The whole map in one call. The Deck has no sidebar to fill in lazily, so the
 * switchboard needs every conversation the moment it opens.
 */
export function fetchDeck(): Promise<DeckResponse> {
  return api.get<DeckResponse>('/api/v1/deck');
}

export const EMPTY_DECK: DeckResponse = {
  communities: [],
  directMessages: [],
  totalUnread: 0,
  totalMentions: 0,
};
