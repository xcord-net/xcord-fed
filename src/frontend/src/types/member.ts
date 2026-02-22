export interface Role {
  id: string;
  name: string;
  color?: string;
  position: number;
}

export interface Member {
  userId: string;
  serverId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  nickname?: string;
  roles: Role[];
  roleColor?: string;
  joinedAt: string;
}
