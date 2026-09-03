import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import BlockList from './BlockList';
import { useBlocks } from '../stores/block.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleBlocked = {
  blockerId: 'me',
  blockedId: 'u-1',
  blockedUsername: 'spammer',
  blockedDisplayName: 'Spammer',
  createdAt: new Date('2025-01-01').toISOString(),
};

describe('BlockList', () => {
  beforeEach(() => {
    useBlocks().reset();
  });

  it('renders heading and input controls', async () => {
    mockFetch({ 'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [] }) });
    const { getByTestId, getByText } = render(() => <BlockList />);
    expect(getByText('Blocked Users')).toBeInTheDocument();
    expect(getByTestId('block-user-input')).toBeInTheDocument();
    expect(getByTestId('block-user-submit-button')).toBeInTheDocument();
  });

  it('shows empty state when there are no blocked users', async () => {
    mockFetch({ 'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [] }) });
    const { findByTestId } = render(() => <BlockList />);
    expect(await findByTestId('block-list-empty-state')).toBeInTheDocument();
  });

  it('disables Block button when input is empty', async () => {
    mockFetch({ 'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [] }) });
    const { getByTestId } = render(() => <BlockList />);
    expect(getByTestId('block-user-submit-button')).toBeDisabled();
  });

  it('renders a row per blocked user', async () => {
    mockFetch({ 'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [sampleBlocked] }) });
    const { findByTestId } = render(() => <BlockList />);
    expect(await findByTestId('blocked-user-item-spammer')).toBeInTheDocument();
  });

  it('blocks a user by username and shows success message', async () => {
    mockFetch({
      'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [] }),
      'POST /api/v1/users/@me/blocks': () => ({ status: 200, body: sampleBlocked }),
    });
    const { getByTestId, findByTestId } = render(() => <BlockList />);
    fireEvent.input(getByTestId('block-user-input'), { target: { value: 'spammer' } });
    fireEvent.click(getByTestId('block-user-submit-button'));
    const status = await findByTestId('block-user-status');
    expect(status).toHaveTextContent('User blocked.');
  });

  it('calls unblock endpoint when Unblock clicked', async () => {
    const state = mockFetch({
      'GET /api/v1/users/@me/blocks': () => ({ status: 200, body: [sampleBlocked] }),
      'DELETE /api/v1/users/@me/blocks/u-1': () => ({ status: 204, body: null }),
    });
    const { findByTestId } = render(() => <BlockList />);
    fireEvent.click(await findByTestId('unblock-user-button-spammer'));
    await waitFor(() => {
      expect(state.calls.some(c => c.method === 'DELETE' && c.url === '/api/v1/users/@me/blocks/u-1')).toBe(true);
    });
  });
});
