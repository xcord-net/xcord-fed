import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { Call, CallParticipant } from '../types/call';

const store = createRoot(() => {
  const [activeCall, setActiveCall] = createSignal<Call | null>(null);
  const [incomingCall, setIncomingCall] = createSignal<Call | null>(null);
  const [participants, setParticipants] = createSignal<CallParticipant[]>([]);
  const [isVideoEnabled, setIsVideoEnabled] = createSignal(false);
  const [isMuted, setIsMuted] = createSignal(false);

  return {
    activeCall,
    setActiveCall,
    incomingCall,
    setIncomingCall,
    participants,
    setParticipants,
    isVideoEnabled,
    setIsVideoEnabled,
    isMuted,
    setIsMuted,
  };
});

export function useCalls() {
  return {
    get activeCall() { return store.activeCall(); },
    get incomingCall() { return store.incomingCall(); },
    get participants() { return store.participants(); },
    get isVideoEnabled() { return store.isVideoEnabled(); },
    get isMuted() { return store.isMuted(); },

    async startCall(conversationId: string, isVideo: boolean): Promise<Call> {
      const call = await api.post<Call>(`/api/v1/conversations/${conversationId}/calls`, {
        isVideoCall: isVideo,
      });
      store.setActiveCall(call);
      store.setIsVideoEnabled(isVideo);
      return call;
    },

    async answerCall(callId: string): Promise<void> {
      const call = await api.post<Call>(`/api/v1/calls/${callId}/answer`, {});
      store.setActiveCall(call);
      store.setIncomingCall(null);
    },

    async declineCall(callId: string): Promise<void> {
      await api.post(`/api/v1/calls/${callId}/decline`, {});
      store.setIncomingCall(null);
    },

    async endCall(callId: string): Promise<void> {
      await api.post(`/api/v1/calls/${callId}/end`, {});
      store.setActiveCall(null);
      store.setParticipants([]);
      store.setIsVideoEnabled(false);
      store.setIsMuted(false);
    },

    toggleVideo(): void {
      store.setIsVideoEnabled(!store.isVideoEnabled());
      const call = store.activeCall();
      if (call) {
        api.patch(`/api/v1/calls/${call.id}/video`, { enabled: store.isVideoEnabled() });
      }
    },

    toggleMute(): void {
      store.setIsMuted(!store.isMuted());
      const call = store.activeCall();
      if (call) {
        api.patch(`/api/v1/calls/${call.id}/mute`, { muted: store.isMuted() });
      }
    },

    receiveIncomingCall(call: Call): void {
      store.setIncomingCall(call);
    },

    updateParticipants(participants: CallParticipant[]): void {
      store.setParticipants(participants);
    },

    reset(): void {
      store.setActiveCall(null);
      store.setIncomingCall(null);
      store.setParticipants([]);
      store.setIsVideoEnabled(false);
      store.setIsMuted(false);
    },
  };
}
