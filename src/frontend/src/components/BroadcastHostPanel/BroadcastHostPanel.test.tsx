import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, waitFor } from '@solidjs/testing-library';

// ---- Hoisted mocks ----
const hoisted = vi.hoisted(() => ({
  startBroadcast: vi.fn(),
  endBroadcast: vi.fn(),
  updateLayout: vi.fn(),
  setActiveStreambots: vi.fn(),
  addToStage: vi.fn(),
  removeFromStage: vi.fn(),
  load: vi.fn(),
  getForChannel: vi.fn(() => []),
  // useHostRoom mock state
  isMuted: vi.fn(() => false),
  isCameraOff: vi.fn(() => false),
  setPreviewRef: vi.fn(),
  toggleMute: vi.fn(),
  toggleCamera: vi.fn(),
  connect: vi.fn().mockResolvedValue(undefined),
  disconnect: vi.fn().mockResolvedValue(undefined),
}));

// Stub the host-room hook (touches livekit-client which is hard in jsdom).
vi.mock('./useHostRoom', () => ({
  useHostRoom: () => ({
    isMuted: hoisted.isMuted,
    isCameraOff: hoisted.isCameraOff,
    setPreviewRef: hoisted.setPreviewRef,
    toggleMute: hoisted.toggleMute,
    toggleCamera: hoisted.toggleCamera,
    connect: hoisted.connect,
    disconnect: hoisted.disconnect,
  }),
}));

// Stub the broadcast and streambot stores -- BroadcastHostPanel only reads
// store actions / channel-scoped lists, never raw signals.
vi.mock('../../stores/broadcast.store', () => ({
  useBroadcast: () => ({
    startBroadcast: hoisted.startBroadcast,
    endBroadcast: hoisted.endBroadcast,
    updateLayout: hoisted.updateLayout,
    setActiveStreambots: hoisted.setActiveStreambots,
    addToStage: hoisted.addToStage,
    removeFromStage: hoisted.removeFromStage,
  }),
}));
vi.mock('../../stores/streambot.store', () => ({
  useStreambot: () => ({
    load: hoisted.load,
    getForChannel: hoisted.getForChannel,
  }),
}));
vi.mock('../../stores/member.store', () => ({
  useMembers: () => ({ members: [] as unknown[] }),
}));

// Stub heavy children -- IdlePanel/LivePanel have their own tests; here we
// only verify the shell wires the right child for each mode.
vi.mock('./IdlePanel', () => ({
  default: (p: { onStart: () => void; starting: boolean; error: string | null }) => (
    <div data-testid="mock-idle-panel">
      <span data-testid="mock-idle-starting">{String(p.starting)}</span>
      <span data-testid="mock-idle-error">{p.error ?? ''}</span>
      <button data-testid="mock-idle-start" onClick={p.onStart}>start</button>
    </div>
  ),
}));
vi.mock('./LivePanel', () => ({
  default: (p: { onEnd: () => void; ending: boolean; error: string | null }) => (
    <div data-testid="mock-live-panel">
      <span data-testid="mock-live-ending">{String(p.ending)}</span>
      <span data-testid="mock-live-error">{p.error ?? ''}</span>
      <button data-testid="mock-live-end" onClick={p.onEnd}>end</button>
    </div>
  ),
}));

import BroadcastHostPanel from './BroadcastHostPanel';
import type { Broadcast } from '../../stores/broadcast.store';

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

describe('BroadcastHostPanel', () => {
  beforeEach(() => {
    Object.values(hoisted).forEach(v => {
      if (typeof v === 'function' && 'mockReset' in v) (v as ReturnType<typeof vi.fn>).mockReset();
    });
    hoisted.load.mockResolvedValue([]);
    hoisted.getForChannel.mockReturnValue([]);
    hoisted.isMuted.mockReturnValue(false);
    hoisted.isCameraOff.mockReturnValue(false);
    hoisted.connect.mockResolvedValue(undefined);
    hoisted.disconnect.mockResolvedValue(undefined);
  });

  it('renders without crashing in idle mode', () => {
    const { getByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="idle" />
    ));
    expect(getByTestId('broadcast-host-panel')).toBeInTheDocument();
  });

  it('renders the IdlePanel when mode is idle', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="idle" />
    ));
    expect(getByTestId('mock-idle-panel')).toBeInTheDocument();
    expect(queryByTestId('mock-live-panel')).toBeNull();
  });

  it('renders the LivePanel when mode is live and a broadcast is provided', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="live" broadcast={makeBroadcast()} />
    ));
    expect(getByTestId('mock-live-panel')).toBeInTheDocument();
    expect(queryByTestId('mock-idle-panel')).toBeNull();
  });

  it('exposes the current mode via data-mode for diagnostics', () => {
    const { getByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="live" broadcast={makeBroadcast()} />
    ));
    expect(getByTestId('broadcast-host-panel').getAttribute('data-mode')).toBe('live');
  });

  it('loads streambots for the channel on mount', async () => {
    render(() => <BroadcastHostPanel channelId="ch-42" mode="idle" />);
    await waitFor(() => expect(hoisted.load).toHaveBeenCalledWith('ch-42'));
  });

  it('starts a broadcast and connects to LiveKit on Start click', async () => {
    hoisted.startBroadcast.mockResolvedValue({
      broadcastId: 'b-new',
      publishToken: 'pub-token',
      roomName: 'room-1',
      livekitUrl: 'wss://lk.test',
      hlsUrl: 'https://hls.test/stream.m3u8',
    });
    const { getByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="idle" />
    ));
    getByTestId('mock-idle-start').click();
    await waitFor(() => expect(hoisted.startBroadcast).toHaveBeenCalled());
    await waitFor(() =>
      expect(hoisted.connect).toHaveBeenCalledWith({
        livekitUrl: 'wss://lk.test',
        publishToken: 'pub-token',
        // Passed so the hook can keep the token current; without it a broadcast
        // that outlives its token cannot reconnect.
        broadcastId: 'b-new',
      }),
    );
  });

  it('ends a broadcast and disconnects LiveKit on End click', async () => {
    hoisted.endBroadcast.mockResolvedValue(undefined);
    const bcast = makeBroadcast({ id: 'b-99' });
    const { getByTestId } = render(() => (
      <BroadcastHostPanel channelId="ch-1" mode="live" broadcast={bcast} />
    ));
    getByTestId('mock-live-end').click();
    await waitFor(() => expect(hoisted.disconnect).toHaveBeenCalled());
    await waitFor(() => expect(hoisted.endBroadcast).toHaveBeenCalledWith('b-99'));
  });
});
