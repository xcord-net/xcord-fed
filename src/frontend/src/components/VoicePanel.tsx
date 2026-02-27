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

        {/* Screen share indicator */}
        <Show when={voice.screenShareParticipantId}>
          <div class="mb-2 px-2 py-1.5 rounded bg-xcord-brand/20 text-xcord-brand text-xs font-medium flex items-center gap-1.5">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="w-3.5 h-3.5 flex-shrink-0" aria-hidden="true">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
            <span class="truncate">
              {voice.isScreenSharing ? 'You are sharing your screen' : 'Someone is sharing their screen'}
            </span>
          </div>
        </Show>

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
            aria-label={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
            class={`px-3 py-1.5 text-xs font-medium rounded transition-colors ${
              voice.isScreenSharing
                ? 'bg-xcord-brand/20 text-xcord-brand hover:bg-xcord-brand/30'
                : 'bg-xcord-bg-secondary text-xcord-text-primary hover:bg-xcord-bg-primary'
            }`}
            onClick={() => voice.toggleScreenShare()}
            title={voice.isScreenSharing ? 'Stop sharing' : 'Share screen'}
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="w-3.5 h-3.5 inline-block" aria-hidden="true">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
              <line x1="8" y1="21" x2="16" y2="21" />
              <line x1="12" y1="17" x2="12" y2="21" />
            </svg>
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
