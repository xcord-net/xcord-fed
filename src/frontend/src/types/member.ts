export interface Group {
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
  groups: Group[];
  groupColor?: string;
  joinedAt: string;
}
