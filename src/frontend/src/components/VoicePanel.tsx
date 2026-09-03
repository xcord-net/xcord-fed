import { Show } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useVoice } from '../stores/voice.store';
import { useChannels } from '../stores/channel.store';
import { ScreenShareIcon } from './ui/icons';
import styles from './VoicePanel.module.css';

export default function VoicePanel() {
  const navigate = useNavigate();
  const voice = useVoice();
  const channels = useChannels();

  const currentChannel = () => {
    if (!voice.currentChannelId) return null;
    return channels.channels.find(c => c.id === voice.currentChannelId);
  };

  return (
    <Show when={voice.currentChannelId && currentChannel()}>
      <div class={styles.panel}>
        {/* Voice outlives the tab you joined from, so the pill has to be the
            way back to it - otherwise you are connected to a room with no
            route to the people in it. */}
        <button
          type="button"
          class={styles.channelInfo}
          data-testid="voice-return-to-room"
          aria-label={`Back to ${currentChannel()?.name}`}
          onClick={() => {
            const channel = currentChannel();
            if (!channel) return;
            if (channel.serverId) navigate(`/channels/${channel.serverId}/${channel.id}`);
          }}
        >
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
        </button>

        {/* Screen share indicator */}
        <Show when={voice.screenShareParticipantId}>
          <div class={styles.screenShareBadge}>
            <ScreenShareIcon class={styles.screenShareIcon} />
            <span class={styles.screenShareText}>
              {voice.isScreenSharing ? 'You are sharing your screen' : 'Someone is sharing their screen'}
            </span>
          </div>
        </Show>

        <div class={styles.controls}>
          <button
            data-testid="voice-mute-button"
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
            data-testid="voice-screenshare-button"
            aria-label={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
            classList={{
              [styles.voiceButton]: true,
              [styles.voiceButtonShareActive]: voice.isScreenSharing,
              [styles.voiceButtonShareInactive]: !voice.isScreenSharing,
            }}
            onClick={() => voice.toggleScreenShare()}
            title={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
          >
            <ScreenShareIcon class={styles.screenShareButtonIcon} />
          </button>
          <button
            data-testid="voice-deafen-button"
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
            data-testid="voice-leave-button"
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
