/** Bitfield flags matching backend ChannelCapability enum */
export const Capability = {
  Chat: 1,
  Voice: 2,
  Video: 4,
  Forum: 8,
  Announcement: 16,
} as const;

const capabilityNameMap: Record<string, number> = {
  chat: Capability.Chat,
  voice: Capability.Voice,
  video: Capability.Video,
  forum: Capability.Forum,
  announcement: Capability.Announcement,
};

/**
 * Parse a capabilities value from the API (string or number) into a numeric bitfield.
 * The API returns enum flag names like "Chat", "Chat, Forum", etc.
 */
export function parseCapabilities(value: string | number): number {
  if (typeof value === 'number') return value;
  if (!value) return 0;
  return value.split(',').reduce((acc, name) => {
    const bit = capabilityNameMap[name.trim().toLowerCase()];
    return bit ? acc | bit : acc;
  }, 0);
}

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
