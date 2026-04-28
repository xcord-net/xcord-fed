import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ConnectedAccounts from './ConnectedAccounts';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleAccount = {
  id: 'acc-1',
  provider: 'GitHub',
  providerUsername: 'octocat',
  connectedAt: '2025-01-01T00:00:00Z',
};

describe('ConnectedAccounts', () => {
  it('renders heading on mount', () => {
    mockFetch({ 'GET /api/v1/users/@me/connected-accounts': () => ({ status: 200, body: [] }) });
    const { getByText } = render(() => <ConnectedAccounts />);
    expect(getByText('Connected Accounts')).toBeInTheDocument();
  });

  it('renders connected account row when API returns one', async () => {
    mockFetch({ 'GET /api/v1/users/@me/connected-accounts': () => ({ status: 200, body: [sampleAccount] }) });
    const { findByText } = render(() => <ConnectedAccounts />);
    expect(await findByText('GitHub')).toBeInTheDocument();
    expect(await findByText('octocat')).toBeInTheDocument();
  });

  it('shows Add Connection list with providers not yet connected', async () => {
    mockFetch({ 'GET /api/v1/users/@me/connected-accounts': () => ({ status: 200, body: [sampleAccount] }) });
    const { findByLabelText, queryByLabelText } = render(() => <ConnectedAccounts />);
    expect(await findByLabelText('Connect Twitter')).toBeInTheDocument();
    expect(await findByLabelText('Connect Spotify')).toBeInTheDocument();
    // GitHub already connected, so its connect button should not appear
    await waitFor(() => expect(queryByLabelText('Connect GitHub')).toBeNull());
  });

  it('disconnects account and removes the row on success', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me/connected-accounts': () => ({ status: 200, body: [sampleAccount] }),
      'DELETE /api/v1/users/@me/connected-accounts/acc-1': () => ({ status: 204, body: null }),
    });
    const { findByText, queryByText } = render(() => <ConnectedAccounts />);
    fireEvent.click(await findByText('Disconnect'));
    await waitFor(() => expect(queryByText('octocat')).toBeNull());
    expect(calls.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/connected-accounts/acc-1'))).toBe(true);
  });

  it('renders empty list gracefully when API returns []', async () => {
    mockFetch({ 'GET /api/v1/users/@me/connected-accounts': () => ({ status: 200, body: [] }) });
    const { findByLabelText } = render(() => <ConnectedAccounts />);
    // All three providers should appear as "Connect" actions
    expect(await findByLabelText('Connect GitHub')).toBeInTheDocument();
  });
});
