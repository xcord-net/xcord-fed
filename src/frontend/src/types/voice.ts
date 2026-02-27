export interface VoiceParticipant {
  userId: string;
  isMuted: boolean;
  isDeafened: boolean;
  isSpeaking?: boolean;
  isScreenSharing?: boolean;
}

export interface VoiceState {
  currentChannelId: string | null;
  participants: Map<string, VoiceParticipant>;
  isMuted: boolean;
  isDeafened: boolean;
}

export interface VoiceStateUpdate {
  userId: string;
  channelId: string | null;
  isMuted: boolean;
  isDeafened: boolean;
}
