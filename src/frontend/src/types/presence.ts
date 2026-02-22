export type PresenceStatus = 'online' | 'idle' | 'dnd' | 'offline';

export interface PresenceUpdate {
  userId: string;
  status: PresenceStatus;
}
