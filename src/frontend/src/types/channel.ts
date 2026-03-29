/** Bitfield flags matching backend ChannelCapability enum */
export const Capability = {
  Chat: 1,
  Voice: 2,
  Video: 4,
  Forum: 8,
  Announcement: 16,
} as const;

export function hasCapability(capabilities: number, cap: number): boolean {
  return (capabilities & cap) !== 0;
}

export interface Channel {
  id: string;
  serverId: string;
  categoryId?: string;
  name: string;
  topic?: string;
  type: 'Text' | 'Voice' | 'Forum';
  capabilities: number;
  accessGroupId?: string;
  position: number;
  isNsfw: boolean;
  slowModeSeconds: number;
  createdAt: string;
  conversationId: string;
}

export interface Category {
  id: string;
  serverId: string;
  name: string;
  position: number;
}
