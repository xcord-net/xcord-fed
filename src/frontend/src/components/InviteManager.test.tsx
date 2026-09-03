import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import InviteManager from './InviteManager';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleInvite = {
  code: 'ABC123',
  serverId: 's-1',
  createdByUserId: 'u-1',
  maxUses: 10,
  uses: 2,
  expiresAt: null,
  createdAt: '2025-01-01T00:00:00Z',
};

describe('InviteManager', () => {
  it('renders the header', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/invites': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => <InviteManager serverId="s-1" />);
    expect(await findByText('Active Invites')).toBeInTheDocument();
  });

  it('shows empty state when no invites', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/invites': () => ({ status: 200, body: [] }) });
    const { findByText, findByTestId } = render(() => <InviteManager serverId="s-1" />);
    expect(await findByTestId('invite-manager-empty')).toBeInTheDocument();
  });

  it('renders invite code with usage info', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/invites': () => ({ status: 200, body: [sampleInvite] }) });
    const { findByTestId, container } = render(() => <InviteManager serverId="s-1" />);
    expect(await findByTestId('invite-code-ABC123')).toHaveTextContent('ABC123');
    await waitFor(() => expect(container.textContent).toContain('Uses: 2 / 10'));
  });

  it('shows error banner when load fails', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/invites': () => ({ status: 500, body: { message: 'Server error' } }) });
    const { findByText } = render(() => <InviteManager serverId="s-1" />);
    expect(await findByText(/Failed to load invites|Server error/)).toBeInTheDocument();
  });

  it('revokes invite when confirmed', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/invites': () => ({ status: 200, body: [sampleInvite] }),
      'DELETE /api/v1/servers/s-1/invites/ABC123': () => ({ status: 204, body: null }),
    });
    const { findByTestId, queryByTestId } = render(() => <InviteManager serverId="s-1" />);
    fireEvent.click(await findByTestId('invite-revoke-ABC123'));
    fireEvent.click(await findByTestId('invite-revoke-ABC123-confirm'));
    await waitFor(() => expect(queryByTestId('invite-code-ABC123')).toBeNull());
    expect(calls.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/invites/ABC123'))).toBe(true);
  });
});
