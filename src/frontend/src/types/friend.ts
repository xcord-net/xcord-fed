export interface Friend {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  status: 'Online' | 'Idle' | 'Dnd' | 'Invisible';
  createdAt: string;
}

export interface FriendRequest {
  id: string;
  fromUserId: string;
  fromUsername: string;
  fromAvatarUrl?: string;
  toUserId: string;
  toUsername: string;
  status: 'Pending' | 'Accepted' | 'Declined';
  createdAt: string;
}
