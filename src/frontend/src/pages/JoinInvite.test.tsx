import { describe, it, expect, beforeEach, vi } from 'vitest';
import { waitFor } from '@solidjs/testing-library';

let mockAuth = { isLoading: false, isAuthenticated: true };

vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuth,
}));

import JoinInvite from './JoinInvite';
import { useServers } from '../stores/server.store';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('JoinInvite', () => {
  beforeEach(() => {
    mockAuth = { isLoading: false, isAuthenticated: true };
    useServers().reset();
  });

  it('renders the loading message while joining', async () => {
    mockFetch({
      'POST /api/v1/invites/ABC/accept': () => new Promise(() => {}),
    });
    const { findByTestId } = renderWithRouter(() => <JoinInvite />, {
      path: '/invite/ABC',
      routePath: '/invite/:code',
    });
    expect(await findByTestId('invite-join-loading')).toHaveTextContent('Joining server...');
  });

  it('calls invite accept API when authenticated and code is valid', async () => {
    const calls = mockFetch({
      'POST /api/v1/invites/ABC/accept': () => ({ status: 200, body: { id: 's-1', name: 'X', ownerId: 'u-1', createdAt: '2025-01-01T00:00:00Z' } }),
    });
    renderWithRouter(() => <JoinInvite />, {
      path: '/invite/ABC',
      routePath: '/invite/:code',
    });
    // The success branch navigates away so the success testid is transient;
    // assert the underlying side effect (the accept POST) instead.
    await waitFor(() =>
      expect(calls.calls.some(c => c.method === 'POST' && c.url.endsWith('/invites/ABC/accept'))).toBe(true),
    );
  });

  it('shows error message when both invite and vanity join fail', async () => {
    mockFetch({
      'POST /api/v1/invites/ABC/accept': () => ({ status: 404, body: { message: 'Invite not found' } }),
      'POST /api/v1/invite/ABC/join': () => ({ status: 404, body: { message: 'Slug missing' } }),
    });
    const { findByTestId } = renderWithRouter(() => <JoinInvite />, {
      path: '/invite/ABC',
      routePath: '/invite/:code',
    });
    const err = await findByTestId('invite-join-error');
    expect(err.textContent).toMatch(/Slug missing|Invite not found|Failed to join server/);
  });

  it('falls back to vanity join when invite accept fails', async () => {
    const calls = mockFetch({
      'POST /api/v1/invites/ABC/accept': () => ({ status: 404, body: { message: 'no' } }),
      'POST /api/v1/invite/ABC/join': () => ({ status: 200, body: { serverId: 's-1', serverName: 'Cool' } }),
    });
    renderWithRouter(() => <JoinInvite />, {
      path: '/invite/ABC',
      routePath: '/invite/:code',
    });
    await waitFor(() =>
      expect(calls.calls.some(c => c.method === 'POST' && c.url.endsWith('/invite/ABC/join'))).toBe(true),
    );
  });

  it('does not call API when user is unauthenticated (redirects to login)', async () => {
    mockAuth = { isLoading: false, isAuthenticated: false };
    const calls = mockFetch({});
    renderWithRouter(() => <JoinInvite />, {
      path: '/invite/ABC',
      routePath: '/invite/:code',
    });
    // Allow effect to run
    await Promise.resolve();
    await Promise.resolve();
    expect(calls.calls.some(c => c.url.includes('/invites/ABC/accept'))).toBe(false);
  });
});
