export interface Call {
  id: string;
  conversationId: string;
  callerId: string;
  callerUsername: string;
  callerAvatarUrl?: string;
  participantIds: string[];
  isVideoCall: boolean;
  status: 'Ringing' | 'Active' | 'Ended';
  startedAt: string;
  endedAt?: string;
}

export interface CallParticipant {
  userId: string;
  username: string;
  avatarUrl?: string;
  isMuted: boolean;
  isDeafened: boolean;
  isVideoEnabled: boolean;
  isScreenSharing: boolean;
}
