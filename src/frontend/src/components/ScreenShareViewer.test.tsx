import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

const voiceState: {
  screenShareParticipantId: string | null;
  isScreenSharing: boolean;
  stopScreenShare: ReturnType<typeof vi.fn>;
} = {
  screenShareParticipantId: null,
  isScreenSharing: false,
  stopScreenShare: vi.fn(),
};

vi.mock('../stores/voice.store', () => ({
  useVoice: () => voiceState,
  getLivekitRoom: () => null,
}));

// livekit-client only needs Track.Source export
vi.mock('livekit-client', () => ({
  Track: { Source: { ScreenShare: 'screen_share' } },
}));

import ScreenShareViewer from './ScreenShareViewer';

describe('ScreenShareViewer', () => {
  beforeEach(() => {
    voiceState.screenShareParticipantId = null;
    voiceState.isScreenSharing = false;
    voiceState.stopScreenShare = vi.fn();
  });

  it('renders nothing when no one is screen sharing', () => {
    const { queryByTestId } = render(() => <ScreenShareViewer />);
    expect(queryByTestId('screen-share-viewer')).toBeNull();
  });

  it('renders the viewer container when someone is screen sharing', () => {
    voiceState.screenShareParticipantId = 'user-1';
    const { getByTestId } = render(() => <ScreenShareViewer />);
    expect(getByTestId('screen-share-viewer')).toBeInTheDocument();
  });

  it('shows "Screen Share" label when remote participant is sharing', () => {
    voiceState.screenShareParticipantId = 'remote-user';
    const { getByText } = render(() => <ScreenShareViewer />);
    expect(getByText('Screen Share')).toBeInTheDocument();
  });

  it('shows "You are sharing" label and Stop button when local user is sharing', () => {
    voiceState.screenShareParticipantId = 'me';
    voiceState.isScreenSharing = true;
    const { getByText } = render(() => <ScreenShareViewer />);
    expect(getByText('You are sharing')).toBeInTheDocument();
    expect(getByText('Stop Sharing')).toBeInTheDocument();
  });

  it('calls stopScreenShare when Stop button clicked', () => {
    voiceState.screenShareParticipantId = 'me';
    voiceState.isScreenSharing = true;
    const { getByText } = render(() => <ScreenShareViewer />);
    fireEvent.click(getByText('Stop Sharing'));
    expect(voiceState.stopScreenShare).toHaveBeenCalledOnce();
  });
});
