export interface CustomEmoji {
  id: string;
  serverId: string;
  name: string;
  imageUrl: string;
  creatorId: string;
  requiresColons: boolean;
  isAnimated: boolean;
  createdAt: string;
}

export interface EmojiCategory {
  name: string;
  icon: string;
  emojis: string[];
}
