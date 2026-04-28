import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import BroadcastGreenRoom from './BroadcastGreenRoom';
import { useBroadcast, type Broadcast } from '../stores/broadcast.store';
import { mockFetch } from '../tests/helpers/mockFetch';

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

describe('BroadcastGreenRoom', () => {
  beforeEach(() => {
    useBroadcast().reset();
  });

  it('renders header and zero-on-stage subtitle when empty', () => {
    const { getByTestId, getByText } = render(() => (
      <BroadcastGreenRoom broadcast={makeBroadcast()} />
    ));
    expect(getByTestId('broadcast-green-room')).toBeInTheDocument();
    expect(getByText('Green Room')).toBeInTheDocument();
    expect(getByText('0 on stage')).toBeInTheDocument();
  });

  it('shows empty placeholder when no slots', () => {
    const { getByText } = render(() => (
      <BroadcastGreenRoom broadcast={makeBroadcast()} />
    ));
    expect(getByText('No one is on stage yet.')).toBeInTheDocument();
  });

  it('renders one row per stage slot using userId fallback', () => {
    const broadcast = makeBroadcast({
      stageSlots: [
        { userId: 'user-1', slotIndex: 0 },
        { userId: 'user-2', slotIndex: 1 },
      ],
    });
    const { getByTestId, getByText } = render(() => (
      <BroadcastGreenRoom broadcast={broadcast} />
    ));
    expect(getByTestId('green-room-slot-0')).toBeInTheDocument();
    expect(getByTestId('green-room-slot-1')).toBeInTheDocument();
    expect(getByText('2 on stage')).toBeInTheDocument();
  });

  it('calls remove API when Remove clicked', async () => {
    const calls = mockFetch({
      'DELETE /api/v1/broadcasts/b-1/stage/user-1': () => ({ status: 204, body: null }),
    });
    const broadcast = makeBroadcast({
      stageSlots: [{ userId: 'user-1', slotIndex: 0 }],
    });
    const { getByTestId } = render(() => (
      <BroadcastGreenRoom broadcast={broadcast} />
    ));
    fireEvent.click(getByTestId('green-room-remove-user-1'));
    await waitFor(() => expect(calls.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/stage/user-1'))).toBe(true));
  });

  it('renders slot badge with 1-based index', () => {
    const broadcast = makeBroadcast({
      stageSlots: [{ userId: 'u-x', slotIndex: 4 }],
    });
    const { getByText } = render(() => (
      <BroadcastGreenRoom broadcast={broadcast} />
    ));
    expect(getByText('#5')).toBeInTheDocument();
  });
});
