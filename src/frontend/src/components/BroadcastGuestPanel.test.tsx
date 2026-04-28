import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';

const mocks = vi.hoisted(() => ({
  joinAsGuest: vi.fn(),
  clearGuestCredentials: vi.fn(),
}));

// Stub livekit-client - no real WebRTC in jsdom.
vi.mock('livekit-client', () => {
  class RoomMock {
    state = 'disconnected';
    localParticipant = {
      getTrackPublications: () => [],
      setCameraEnabled: vi.fn().mockResolvedValue(undefined),
      setMicrophoneEnabled: vi.fn().mockResolvedValue(undefined),
    };
    connect = vi.fn().mockResolvedValue(undefined);
    disconnect = vi.fn().mockResolvedValue(undefined);
    on = vi.fn();
  }
  return {
    Room: RoomMock,
    RoomEvent: { LocalTrackPublished: 'localTrackPublished' },
    Track: { Source: { Camera: 'camera' } },
    ConnectionState: { Disconnected: 'disconnected' },
  };
});

// Stub broadcast and auth stores so we don't need real store internals.
vi.mock('../stores/broadcast.store', () => ({
  useBroadcast: () => ({
    joinAsGuest: mocks.joinAsGuest,
    clearGuestCredentials: mocks.clearGuestCredentials,
  }),
}));
vi.mock('../stores/auth.store', () => ({
  useAuth: () => ({
    user: { id: 'me-1', username: 'me', email: '' },
  }),
}));

import BroadcastGuestPanel from './BroadcastGuestPanel';
import type { Broadcast } from '../stores/broadcast.store';

function makeBroadcast(over: Partial<Broadcast> = {}): Broadcast {
  return {
    id: 'b-1',
    channelId: 'ch-1',
    hostUserId: 'host-1',
    layoutPreset: 'Grid',
    status: 'Live',
    hlsUrl: 'https://example.com/stream.m3u8',
    roomName: 'room-1',
    startedAt: '2025-01-01T00:00:00Z',
    stageSlots: [],
    streambots: [],
    ...over,
  };
}

describe('BroadcastGuestPanel', () => {
  beforeEach(() => {
    mocks.joinAsGuest.mockReset();
    mocks.clearGuestCredentials.mockReset();
  });

  it('renders the guest booth panel and Join button initially', () => {
    const { getByTestId } = render(() => (
      <BroadcastGuestPanel broadcast={makeBroadcast()} />
    ));
    expect(getByTestId('broadcast-guest-panel')).toBeInTheDocument();
    expect(getByTestId('broadcast-guest-join-button')).toHaveTextContent('Join as Guest');
  });

  it('shows the join hint overlay when not connected', () => {
    const { container } = render(() => (
      <BroadcastGuestPanel broadcast={makeBroadcast()} />
    ));
    expect(container.textContent).toContain('Join as a guest to publish your camera and mic.');
  });

  it('shows error message when join fails', async () => {
    mocks.joinAsGuest.mockRejectedValueOnce(new Error('No backstage pass'));
    const { getByTestId, findByRole } = render(() => (
      <BroadcastGuestPanel broadcast={makeBroadcast()} />
    ));
    fireEvent.click(getByTestId('broadcast-guest-join-button'));
    expect(await findByRole('alert')).toHaveTextContent('No backstage pass');
  });

  it('calls joinAsGuest with the broadcast id when Join clicked', async () => {
    mocks.joinAsGuest.mockResolvedValue({
      token: 't',
      roomName: 'r',
      livekitUrl: 'wss://lk.test',
    });
    const { getByTestId } = render(() => (
      <BroadcastGuestPanel broadcast={makeBroadcast({ id: 'b-99' })} />
    ));
    fireEvent.click(getByTestId('broadcast-guest-join-button'));
    await waitFor(() => expect(mocks.joinAsGuest).toHaveBeenCalledWith('b-99'));
  });

  it('renders mute/camera/leave controls after a successful join', async () => {
    mocks.joinAsGuest.mockResolvedValue({
      token: 't',
      roomName: 'r',
      livekitUrl: 'wss://lk.test',
    });
    const { getByTestId, findByTestId } = render(() => (
      <BroadcastGuestPanel broadcast={makeBroadcast()} />
    ));
    fireEvent.click(getByTestId('broadcast-guest-join-button'));
    expect(await findByTestId('broadcast-guest-mute-button')).toBeInTheDocument();
    expect(await findByTestId('broadcast-guest-camera-button')).toBeInTheDocument();
    expect(await findByTestId('broadcast-guest-leave-button')).toBeInTheDocument();
  });
});
