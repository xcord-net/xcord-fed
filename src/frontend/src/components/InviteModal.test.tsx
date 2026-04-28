import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import InviteModal from './InviteModal';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleInvite = {
  code: 'ABC123',
  serverId: 's-1',
  maxUses: null,
  uses: 0,
  expiresAt: null,
  createdAt: '2025-01-01T00:00:00Z',
};

describe('InviteModal', () => {
  it('renders the invite dialog with title and Done button', async () => {
    mockFetch({
      'POST /api/v1/servers/s-1/invites': () => ({ status: 200, body: sampleInvite }),
    });
    const { getByTestId, findByText } = render(() => (
      <InviteModal serverId="s-1" onClose={() => {}} />
    ));
    expect(getByTestId('invite-dialog')).toBeInTheDocument();
    expect(await findByText('Invite People')).toBeInTheDocument();
    expect(getByTestId('invite-close-button')).toHaveTextContent('Done');
  });

  it('populates the invite link input after the initial create call', async () => {
    mockFetch({
      'POST /api/v1/servers/s-1/invites': () => ({ status: 200, body: sampleInvite }),
    });
    const { getByTestId } = render(() => (
      <InviteModal serverId="s-1" onClose={() => {}} />
    ));
    await waitFor(() => {
      const input = getByTestId('invite-link-input') as HTMLInputElement;
      expect(input.value).toContain('/invite/ABC123');
    });
  });

  it('calls onClose when the Done button is clicked', async () => {
    mockFetch({
      'POST /api/v1/servers/s-1/invites': () => ({ status: 200, body: sampleInvite }),
    });
    const onClose = vi.fn();
    const { getByTestId } = render(() => (
      <InviteModal serverId="s-1" onClose={onClose} />
    ));
    fireEvent.click(getByTestId('invite-close-button'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('issues a new POST when Generate New Link is clicked', async () => {
    const calls = mockFetch({
      'POST /api/v1/servers/s-1/invites': () => ({ status: 200, body: sampleInvite }),
    });
    const { getByTestId } = render(() => (
      <InviteModal serverId="s-1" onClose={() => {}} />
    ));
    // Wait for the initial POST to complete so the Generate button is no longer
    // disabled (button.disabled while loading()=true).
    const btn = getByTestId('invite-generate-button') as HTMLButtonElement;
    await waitFor(() => expect(btn.disabled).toBe(false));
    const initialCount = calls.calls.length;
    fireEvent.click(btn);
    await waitFor(() => expect(calls.calls.length).toBeGreaterThan(initialCount));
  });

  it('shows an error alert when invite creation fails', async () => {
    mockFetch({
      'POST /api/v1/servers/s-1/invites': () => ({ status: 500, body: { message: 'Boom' } }),
    });
    const { findByRole } = render(() => (
      <InviteModal serverId="s-1" onClose={() => {}} />
    ));
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Boom|Failed to create invite/);
  });
});
