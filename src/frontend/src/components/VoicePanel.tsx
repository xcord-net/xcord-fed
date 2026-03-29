import { Show } from 'solid-js';
import { useVoice } from '../stores/voice.store';
import { useChannels } from '../stores/channel.store';
import styles from './VoicePanel.module.css';

export default function VoicePanel() {
  const voice = useVoice();
  const channels = useChannels();

  const currentChannel = () => {
    if (!voice.currentChannelId) return null;
    return channels.channels.find(c => c.id === voice.currentChannelId);
  };

  return (
    <Show when={voice.currentChannelId && currentChannel()}>
      <div class={styles.panel}>
        <div class={styles.channelInfo}>
          <p class={styles.voiceLabel}>Voice Channel</p>
          <p class={styles.channelName}>{currentChannel()?.name}</p>
          <p
            data-testid="voice-connection-status"
            classList={{
              [styles.connectionStatus]: true,
              [styles.connectionStatusConnecting]: voice.isConnecting,
              [styles.connectionStatusConnected]: !voice.isConnecting,
            }}
          >
            {voice.isConnecting ? 'Connecting...' : 'Connected'}
          </p>
        </div>

        {/* Screen share indicator */}
        <Show when={voice.screenShareParticipantId}>
          <div class={styles.screenShareBadge}>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.screenShareIcon} aria-hidden="true">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
            <span class={styles.screenShareText}>
              {voice.isScreenSharing ? 'You are sharing your screen' : 'Someone is sharing their screen'}
            </span>
          </div>
        </Show>

        <div class={styles.controls}>
          <button
            aria-label={voice.isMuted ? 'Unmute' : 'Mute'}
            classList={{
              [styles.voiceButton]: true,
              [styles.voiceButtonMuteActive]: voice.isMuted,
              [styles.voiceButtonMuteInactive]: !voice.isMuted,
            }}
            onClick={() => voice.toggleMute()}
          >
            {voice.isMuted ? 'Unmute' : 'Mute'}
          </button>
          <button
            aria-label={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
            classList={{
              [styles.voiceButton]: true,
              [styles.voiceButtonShareActive]: voice.isScreenSharing,
              [styles.voiceButtonShareInactive]: !voice.isScreenSharing,
            }}
            onClick={() => voice.toggleScreenShare()}
            title={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.screenShareButtonIcon} aria-hidden="true">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
          </button>
          <button
            aria-label="Deafen"
            aria-pressed={voice.isDeafened}
            classList={{
              [styles.voiceButton]: true,
              [styles.voiceButtonDeafenActive]: voice.isDeafened,
              [styles.voiceButtonDeafenInactive]: !voice.isDeafened,
            }}
            onClick={() => voice.toggleDeafen()}
          >
            Deafen
          </button>
          <button
            aria-label="Leave"
            class={`${styles.voiceButton} ${styles.voiceButtonLeave}`}
            onClick={() => voice.leaveVoice()}
          >
            Leave
          </button>
        </div>
      </div>
    </Show>
  );
}
