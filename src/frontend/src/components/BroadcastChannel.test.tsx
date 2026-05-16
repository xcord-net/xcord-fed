import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, waitFor } from '@solidjs/testing-library';

/**
 * Scope: orchestrator smoke test.
 *
 * `BroadcastChannel` is a pure router that picks one of four sub-panels based
 * on broadcast status + caller permissions. The sub-panels (HostPanel,
 * GuestPanel, Viewer, GreenRoom) load `livekit-client`, which is impossible
 * to drive under jsdom and has its own dedicated tests. So this file
 * deliberately mocks each child with a marker `<div data-testid="mock-*">`
 * and asserts only the dispatch decision: given some broadcast/auth state,
 * which child was chosen. No behavior is asserted on the mocks beyond
 * presence (and, for the host panel, a single `mode` prop forwarded so we
 * can verify idle vs live transitions).
 */
vi.mock('./BroadcastHostPanel', () => ({
  default: (props: { mode: string }) => (
    <div data-testid="mock-host-panel">host:{props.mode}</div>
  ),
}));
vi.mock('./BroadcastGuestPanel', () => ({
  default: () => <div data-testid="mock-guest-panel">guest</div>,
}));
vi.mock('./BroadcastViewer', () => ({
  default: () => <div data-testid="mock-viewer">viewer</div>,
}));
vi.mock('./BroadcastGreenRoom', () => ({
  default: () => <div data-testid="mock-green-room">green-room</div>,
}));

const mockAuthState = { user: { id: 'me-1', username: 'me', email: '' } as { id: string; username: string; email: string } | null };
vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuthState,
}));

import BroadcastChannel from './BroadcastChannel';
import { useBroadcast } from '../stores/broadcast.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('BroadcastChannel', () => {
  beforeEach(() => {
    useBroadcast().reset();
    mockAuthState.user = { id: 'me-1', username: 'me', email: '' };
  });

  it('renders idle message for non-managers when no broadcast is active', async () => {
    mockFetch({ 'GET /api/v1/channels/ch-1/broadcasts': () => ({ status: 200, body: [] }) });
    const { findByTestId } = render(() => (
      <BroadcastChannel channelId="ch-1" canManageBroadcasts={false} />
    ));
    expect(await findByTestId('broadcast-idle')).toHaveTextContent('No broadcast is live.');
  });

  it('renders host panel in idle mode for managers when no broadcast is live', async () => {
    mockFetch({ 'GET /api/v1/channels/ch-1/broadcasts': () => ({ status: 200, body: [] }) });
    const { findByTestId } = render(() => (
      <BroadcastChannel channelId="ch-1" canManageBroadcasts={true} />
    ));
    expect(await findByTestId('mock-host-panel')).toHaveTextContent('host:idle');
  });

  it('renders host panel + green room when current user is the host', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/broadcasts': () => ({
        status: 200,
        body: [{
          id: 'b-1', channelId: 'ch-1', hostUserId: 'me-1', layoutPreset: 'Grid', status: 'Live',
          hlsUrl: '', roomName: 'r', startedAt: '2025-01-01T00:00:00Z', stageSlots: [], streambots: [],
        }],
      }),
    });
    const { getByTestId, queryByTestId } = render(() => (
      <BroadcastChannel channelId="ch-1" canManageBroadcasts={true} />
    ));
    // The component renders the idle host panel first (sync) and switches to
    // the live host panel after the broadcast fetch resolves; waitFor on the
    // updated text rather than the test id (which never goes away).
    await waitFor(() => expect(getByTestId('mock-host-panel')).toHaveTextContent('host:live'));
    await waitFor(() => expect(queryByTestId('mock-green-room')).not.toBeNull());
    expect(queryByTestId('mock-viewer')).toBeNull();
  });

  it('renders viewer when user is neither host nor guest', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/broadcasts': () => ({
        status: 200,
        body: [{
          id: 'b-2', channelId: 'ch-1', hostUserId: 'someone-else', layoutPreset: 'Grid', status: 'Live',
          hlsUrl: '', roomName: 'r', startedAt: '2025-01-01T00:00:00Z', stageSlots: [], streambots: [],
        }],
      }),
    });
    const { findByTestId } = render(() => (
      <BroadcastChannel channelId="ch-1" canManageBroadcasts={false} />
    ));
    expect(await findByTestId('mock-viewer')).toBeInTheDocument();
  });

  it('renders the top-level container test-id', async () => {
    mockFetch({ 'GET /api/v1/channels/ch-1/broadcasts': () => ({ status: 200, body: [] }) });
    const { getByTestId } = render(() => (
      <BroadcastChannel channelId="ch-1" canManageBroadcasts={false} />
    ));
    expect(getByTestId('broadcast-channel')).toBeInTheDocument();
  });
});
