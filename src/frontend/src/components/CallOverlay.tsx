import { Show, For } from 'solid-js';
import { useCalls } from '../stores/call.store';
import Modal from './ui/Modal';
import type { Call } from '../types/call';

interface IncomingCallDialogProps {
  call: Call;
}

function IncomingCallDialog(props: IncomingCallDialogProps) {
  const callStore = useCalls();

  return (
    <Modal open={true} onClose={() => callStore.declineCall(props.call.id)} aria-label="Incoming call" size="sm">
      <div class="p-6 text-center">
        <div class="w-20 h-20 rounded-full bg-xcord-brand mx-auto mb-4 flex items-center justify-center text-white text-2xl font-semibold">
          {props.call.callerUsername.charAt(0).toUpperCase()}
        </div>

        <h2 class="text-white font-semibold text-xl mb-2">
          {props.call.callerUsername}
        </h2>
        <p class="text-xcord-text-muted mb-6">
          {props.call.isVideoCall ? 'Video' : 'Voice'} call incoming...
        </p>

        <div class="flex space-x-3 justify-center">
          <button
            class="bg-green-600 text-white px-6 py-3 rounded-full hover:bg-green-700 focus-visible:ring-2 focus-visible:ring-green-400 focus-visible:outline-none transition"
            onClick={() => callStore.answerCall(props.call.id)}
          >
            Answer
          </button>
          <button
            class="bg-red-600 text-white px-6 py-3 rounded-full hover:bg-red-700 focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none transition"
            onClick={() => callStore.declineCall(props.call.id)}
          >
            Decline
          </button>
        </div>
      </div>
    </Modal>
  );
}

export default function CallOverlay() {
  const callStore = useCalls();

  return (
    <>
      <Show when={callStore.incomingCall}>
        {(call) => <IncomingCallDialog call={call()} />}
      </Show>

      <Show when={callStore.activeCall}>
        <div class="fixed bottom-0 left-0 right-0 bg-xcord-bg-primary border-t border-xcord-border p-4 z-40">
          <div class="max-w-4xl mx-auto flex items-center justify-between">
            <div class="flex items-center space-x-4">
              <div class="w-12 h-12 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                Call
              </div>
              <div>
                <h3 class="text-white font-medium">
                  {callStore.activeCall!.isVideoCall ? 'Video' : 'Voice'} Call
                </h3>
                <p class="text-xs text-xcord-text-muted">
                  {callStore.participants.length} {callStore.participants.length === 1 ? 'participant' : 'participants'}
                </p>
              </div>
            </div>

            <div class="flex items-center space-x-3">
              <button
                class={`w-12 h-12 rounded-full flex items-center justify-center transition ${
                  callStore.isMuted ? 'bg-red-600 text-white' : 'bg-xcord-bg-secondary text-white hover:bg-xcord-bg-primary'
                }`}
                onClick={() => callStore.toggleMute()}
                title={callStore.isMuted ? 'Unmute' : 'Mute'}
              >
                {callStore.isMuted ? '🔇' : '🎤'}
              </button>

              <Show when={callStore.activeCall!.isVideoCall}>
                <button
                  class={`w-12 h-12 rounded-full flex items-center justify-center transition ${
                    callStore.isVideoEnabled ? 'bg-xcord-bg-secondary text-white hover:bg-xcord-bg-primary' : 'bg-red-600 text-white'
                  }`}
                  onClick={() => callStore.toggleVideo()}
                  title={callStore.isVideoEnabled ? 'Stop Video' : 'Start Video'}
                >
                  📹
                </button>
              </Show>

              <button
                class="bg-red-600 text-white px-4 py-2 rounded-full hover:bg-red-700 transition"
                onClick={() => callStore.endCall(callStore.activeCall!.id)}
              >
                End Call
              </button>
            </div>
          </div>

          <Show when={callStore.participants.length > 0}>
            <div class="mt-4 flex space-x-2 overflow-x-auto">
              <For each={callStore.participants}>
                {(participant) => (
                  <div class="flex-shrink-0 text-center">
                    <div class="w-16 h-16 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold mb-1">
                      {participant.username.charAt(0).toUpperCase()}
                    </div>
                    <p class="text-xs text-white truncate w-16">{participant.username}</p>
                    <div class="flex justify-center space-x-1 mt-1">
                      <Show when={participant.isMuted}>
                        <span class="text-xs">🔇</span>
                      </Show>
                      <Show when={participant.isVideoEnabled}>
                        <span class="text-xs">📹</span>
                      </Show>
                    </div>
                  </div>
                )}
              </For>
            </div>
          </Show>
        </div>
      </Show>
    </>
  );
}
