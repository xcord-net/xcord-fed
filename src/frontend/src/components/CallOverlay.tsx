import { Show, For } from 'solid-js';
import { useCalls } from '../stores/call.store';
import { useVoice } from '../stores/voice.store';
import Modal from './ui/Modal';
import type { Call } from '../types/call';
import styles from './CallOverlay.module.css';

interface IncomingCallDialogProps {
  call: Call;
}

function IncomingCallDialog(props: IncomingCallDialogProps) {
  const callStore = useCalls();

  return (
    <Modal open={true} onClose={() => callStore.declineCall(props.call.id)} aria-label="Incoming call" size="sm">
      <div class={styles.incomingCallBody}>
        <div class={styles.callerAvatar}>
          {props.call.callerUsername.charAt(0).toUpperCase()}
        </div>

        <h2 class={styles.callerName}>
          {props.call.callerUsername}
        </h2>
        <p class={styles.callerSubtitle}>
          {props.call.isVideoCall ? 'Video' : 'Voice'} call incoming...
        </p>

        <div class={styles.incomingCallActions}>
          <button
            class={styles.answerButton}
            onClick={() => callStore.answerCall(props.call.id)}
          >
            Answer
          </button>
          <button
            class={styles.declineButton}
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
  const voice = useVoice();

  return (
    <>
      <Show when={callStore.incomingCall}>
        {(call) => <IncomingCallDialog call={call()} />}
      </Show>

      <Show when={callStore.activeCall}>
        <div class={styles.activeCallBar}>
          <div class={styles.activeCallInner}>
            <div class={styles.activeCallInfo}>
              <div class={styles.activeCallAvatar}>
                Call
              </div>
              <div>
                <h3 class={styles.activeCallTitle}>
                  {callStore.activeCall!.isVideoCall ? 'Video' : 'Voice'} Call
                </h3>
                <p class={styles.activeCallSubtitle}>
                  {callStore.participants.length} {callStore.participants.length === 1 ? 'participant' : 'participants'}
                </p>
              </div>
            </div>

            <div class={styles.activeCallControls}>
              <button
                classList={{
                  [styles.controlButton]: true,
                  [styles.controlButtonActive]: callStore.isMuted,
                  [styles.controlButtonDefault]: !callStore.isMuted,
                }}
                onClick={() => callStore.toggleMute()}
                title={callStore.isMuted ? 'Unmute' : 'Mute'}
              >
                {callStore.isMuted ? '🔇' : '🎤'}
              </button>

              <Show when={callStore.activeCall!.isVideoCall}>
                <button
                  classList={{
                    [styles.controlButton]: true,
                    [styles.controlButtonDefault]: callStore.isVideoEnabled,
                    [styles.controlButtonActive]: !callStore.isVideoEnabled,
                  }}
                  onClick={() => callStore.toggleVideo()}
                  title={callStore.isVideoEnabled ? 'Stop Video' : 'Start Video'}
                >
                  📹
                </button>
              </Show>

              <button
                classList={{
                  [styles.controlButton]: true,
                  [styles.controlButtonBrand]: voice.isScreenSharing,
                  [styles.controlButtonDefault]: !voice.isScreenSharing,
                }}
                onClick={() => voice.toggleScreenShare()}
                title={voice.isScreenSharing ? 'Stop Sharing' : 'Share Screen'}
                aria-label={voice.isScreenSharing ? 'Stop sharing screen' : 'Share screen'}
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.screenShareIcon} aria-hidden="true">
                  <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
                  <line x1="8" y1="21" x2="16" y2="21" />
                  <line x1="12" y1="17" x2="12" y2="21" />
                </svg>
              </button>

              <button
                class={styles.endCallButton}
                onClick={() => callStore.endCall(callStore.activeCall!.id)}
              >
                End Call
              </button>
            </div>
          </div>

          <Show when={callStore.participants.length > 0}>
            <div class={styles.participantList}>
              <For each={callStore.participants}>
                {(participant) => (
                  <div class={styles.participantItem}>
                    <div class={styles.participantAvatar}>
                      {participant.username.charAt(0).toUpperCase()}
                    </div>
                    <p class={styles.participantName}>{participant.username}</p>
                    <div class={styles.participantStatusIcons}>
                      <Show when={participant.isMuted}>
                        <span class={styles.participantStatusIcon}>🔇</span>
                      </Show>
                      <Show when={participant.isVideoEnabled}>
                        <span class={styles.participantStatusIcon}>📹</span>
                      </Show>
                      <Show when={participant.isScreenSharing}>
                        <span class={styles.participantStatusIcon} title="Sharing screen">
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.participantScreenShareIcon} aria-hidden="true">
                            <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
                            <line x1="8" y1="21" x2="16" y2="21" />
                            <line x1="12" y1="17" x2="12" y2="21" />
                          </svg>
                        </span>
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
