import { Show } from 'solid-js';
import { useVoice } from '../stores/voice.store';
import { useChannels } from '../stores/channel.store';

export default function VoicePanel() {
  const voice = useVoice();
  const channels = useChannels();

  const currentChannel = () => {
    if (!voice.currentChannelId) return null;
    return channels.channels.find(c => c.id === voice.currentChannelId);
  };

  return (
    <Show when={voice.currentChannelId && currentChannel()}>
      <div class="bg-xcord-bg-tertiary border-t border-xcord-border px-4 py-3">
        <div class="mb-2">
          <p class="text-xs font-semibold text-green-400 uppercase tracking-wide">Voice Channel</p>
          <p class="text-sm text-white truncate">{currentChannel()?.name}</p>
          <p
            data-testid="voice-connection-status"
            class={`text-xs mt-0.5 ${voice.isConnecting ? 'text-yellow-400' : 'text-green-400'}`}
          >
            {voice.isConnecting ? 'Connecting...' : 'Connected'}
          </p>
        </div>
        <div class="flex items-center gap-2">
          <button
            aria-label={voice.isMuted ? 'Unmute' : 'Mute'}
            class={`flex-1 px-3 py-1.5 text-xs font-medium rounded transition-colors ${
              voice.isMuted
                ? 'bg-red-500/20 text-red-400 hover:bg-red-500/30'
                : 'bg-xcord-bg-secondary text-xcord-text-primary hover:bg-xcord-bg-primary'
            }`}
            onClick={() => voice.toggleMute()}
          >
            {voice.isMuted ? 'Unmute' : 'Mute'}
          </button>
          <button
            aria-label="Deafen"
            class={`px-3 py-1.5 text-xs font-medium rounded transition-colors ${
              voice.isDeafened
                ? 'bg-red-500/20 text-red-400 hover:bg-red-500/30'
                : 'bg-xcord-bg-secondary text-xcord-text-primary hover:bg-xcord-bg-primary'
            }`}
            onClick={() => voice.toggleDeafen()}
          >
            Deafen
          </button>
          <button
            aria-label="Leave"
            class="px-3 py-1.5 text-xs font-medium rounded bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors"
            onClick={() => voice.leaveVoice()}
          >
            Leave
          </button>
        </div>
      </div>
    </Show>
  );
}
