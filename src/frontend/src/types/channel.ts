export interface Channel {
  id: string;
  serverId: string;
  categoryId?: string;
  name: string;
  topic?: string;
  type: 'Text' | 'Voice' | 'Forum';
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
