import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

const voiceState = {
  currentChannelId: null as string | null,
  isMuted: false,
  isDeafened: false,
  isConnecting: false,
  isScreenSharing: false,
  screenShareParticipantId: null as string | null,
  toggleMute: vi.fn(),
  toggleDeafen: vi.fn(),
  toggleScreenShare: vi.fn(),
  leaveVoice: vi.fn(),
};

vi.mock('../stores/voice.store', () => ({
  useVoice: () => voiceState,
}));

const navigate = vi.fn();
vi.mock('@solidjs/router', () => ({
  useNavigate: () => navigate,
}));

import VoicePanel from './VoicePanel';
import { useChannels } from '../stores/channel.store';
import { Capability, type Channel } from '../types/channel';

function makeChannel(over: Partial<Channel> = {}): Channel {
  return {
    id: 'voice-1', serverId: 's-1', name: 'General Voice', type: 'Voice',
    capabilities: Capability.Voice, position: 0, isNsfw: false, slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z', conversationId: 'conv-1', ...over,
  };
}

describe('VoicePanel', () => {
  beforeEach(() => {
    useChannels().reset();
    navigate.mockClear();
    voiceState.currentChannelId = null;
    voiceState.isMuted = false;
    voiceState.isDeafened = false;
    voiceState.isConnecting = false;
    voiceState.isScreenSharing = false;
    voiceState.screenShareParticipantId = null;
    voiceState.toggleMute = vi.fn();
    voiceState.toggleDeafen = vi.fn();
    voiceState.toggleScreenShare = vi.fn();
    voiceState.leaveVoice = vi.fn();
  });

  it('renders nothing when not connected to a voice channel', () => {
    const { container } = render(() => <VoicePanel />);
    expect(container.textContent).toBe('');
  });

  it('renders panel with channel name when connected', () => {
    useChannels().addChannel(makeChannel({ id: 'voice-1', name: 'General Voice' }));
    voiceState.currentChannelId = 'voice-1';
    const { getByText, getByTestId } = render(() => <VoicePanel />);
    expect(getByText('General Voice')).toBeInTheDocument();
    expect(getByTestId('voice-connection-status')).toHaveTextContent('Connected');
  });

  it('shows "Connecting..." status when isConnecting', () => {
    useChannels().addChannel(makeChannel({ id: 'voice-1' }));
    voiceState.currentChannelId = 'voice-1';
    voiceState.isConnecting = true;
    const { getByTestId } = render(() => <VoicePanel />);
    expect(getByTestId('voice-connection-status')).toHaveTextContent('Connecting...');
  });

  it('toggles mute when Mute button clicked', () => {
    useChannels().addChannel(makeChannel({ id: 'voice-1' }));
    voiceState.currentChannelId = 'voice-1';
    const { getByLabelText } = render(() => <VoicePanel />);
    fireEvent.click(getByLabelText('Mute'));
    expect(voiceState.toggleMute).toHaveBeenCalledOnce();
  });

  it('calls leaveVoice when Leave clicked', () => {
    useChannels().addChannel(makeChannel({ id: 'voice-1' }));
    voiceState.currentChannelId = 'voice-1';
    const { getByLabelText } = render(() => <VoicePanel />);
    fireEvent.click(getByLabelText('Leave'));
    expect(voiceState.leaveVoice).toHaveBeenCalledOnce();
  });

  it('shows screen share badge when someone is sharing', () => {
    useChannels().addChannel(makeChannel({ id: 'voice-1' }));
    voiceState.currentChannelId = 'voice-1';
    voiceState.screenShareParticipantId = 'remote-user';
    const { getByText } = render(() => <VoicePanel />);
    expect(getByText('Someone is sharing their screen')).toBeInTheDocument();
  });
});

describe('VoicePanel return-to-room', () => {
  beforeEach(() => {
    useChannels().reset();
    navigate.mockClear();
    voiceState.currentChannelId = null;
  });

  // Voice survives navigating away, so the pill is the only route back to the
  // room you are actually connected to.
  it('routes back to the room it is connected to', () => {
    useChannels().addChannel(makeChannel());
    voiceState.currentChannelId = 'voice-1';

    const { getByTestId } = render(() => <VoicePanel />);
    fireEvent.click(getByTestId('voice-return-to-room'));

    expect(navigate).toHaveBeenCalledWith('/channels/s-1/voice-1');
  });

  it('names the room it goes back to', () => {
    useChannels().addChannel(makeChannel({ name: 'Forge Floor' }));
    voiceState.currentChannelId = 'voice-1';

    const { getByTestId } = render(() => <VoicePanel />);
    expect(getByTestId('voice-return-to-room').getAttribute('aria-label'))
      .toBe('Back to Forge Floor');
  });

  it('offers nothing to go back to when not connected', () => {
    const { queryByTestId } = render(() => <VoicePanel />);
    expect(queryByTestId('voice-return-to-room')).toBeNull();
  });
});
