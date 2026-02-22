export interface UserProfile {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  bannerUrl?: string;
  bio?: string;
  pronouns?: string;
  createdAt: string;
  scheduledDeletionAt?: string | null;
}

export interface ServerProfile {
  userId: string;
  serverId: string;
  nickname?: string;
  avatarUrl?: string;
}
