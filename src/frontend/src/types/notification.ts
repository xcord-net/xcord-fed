export interface NotificationSettings {
  userId: string;
  muteAll: boolean;
  allowDirectMessages: boolean;
  allowFriendRequests: boolean;
  showOnlineStatus: boolean;
  mutedServerIds: string[];
  mutedChannelIds: string[];
  mentionKeywords: string[];
}

export interface ChannelNotificationOverride {
  channelId: string;
  level: 'All' | 'Mentions' | 'Nothing';
}

/** Notification level values as returned by the backend enum (serialised as strings). */
export type NotificationLevel = 'All' | 'MentionsOnly' | 'None';

/** A persisted per-server or per-channel notification override from the backend. */
export interface NotificationSettingDto {
  id: string;
  userId: string;
  serverId: string | null;
  channelId: string | null;
  level: NotificationLevel;
  suppressEveryone: boolean;
  suppressRoles: boolean;
  muteUntil: string | null;
}
