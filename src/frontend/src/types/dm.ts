export interface DmChannel {
  id: string;
  conversationId: string;
  recipientId: string;
  recipientUsername: string;
  recipientAvatarUrl?: string;
  lastMessageAt?: string;
  createdAt: string;
}

export interface DmGroupMember {
  userId: string;
  username: string;
}

export interface DmGroup {
  id: string;
  conversationId: string;
  name: string;
  iconUrl?: string;
  ownerId: string;
  memberIds: string[];
  members: DmGroupMember[];
  lastMessageAt?: string;
  createdAt: string;
}
