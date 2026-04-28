import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import FriendList from './FriendList';
import { useFriends } from '../stores/friend.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('FriendList', () => {
  beforeEach(() => {
    useFriends().reset();
  });

  it('renders the Friends heading and tabs', async () => {
    mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
    });
    const { getByTestId } = render(() => <FriendList />);
    expect(getByTestId('friends-heading')).toHaveTextContent('Friends');
    expect(getByTestId('friends-tab-all')).toBeInTheDocument();
    expect(getByTestId('friends-tab-pending')).toBeInTheDocument();
  });

  it('shows the empty state when there are no friends', async () => {
    mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
    });
    const { findByTestId } = render(() => <FriendList />);
    expect(await findByTestId('friends-empty-state')).toHaveTextContent('No friends yet');
  });

  it('disables the Send Request button when the input is empty', async () => {
    mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
    });
    const { getByTestId } = render(() => <FriendList />);
    expect(getByTestId('friend-request-submit-button')).toBeDisabled();
    fireEvent.input(getByTestId('friend-request-input'), { target: { value: 'bob' } });
    expect(getByTestId('friend-request-submit-button')).not.toBeDisabled();
  });

  it('switches to the Pending tab when clicked', async () => {
    mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
    });
    const { getByTestId, getByText } = render(() => <FriendList />);
    fireEvent.click(getByTestId('friends-tab-pending'));
    expect(getByText('Incoming Requests')).toBeInTheDocument();
    expect(getByText('Outgoing Requests')).toBeInTheDocument();
  });

  it('sends a friend request when Send Request is clicked', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
      'POST /api/v1/friends/request': () => ({
        status: 200,
        body: {
          id: 'fr-1',
          senderId: 'me',
          senderUsername: 'me',
          senderDisplayName: 'me',
          receiverId: 'them',
          receiverUsername: 'bob',
          receiverDisplayName: 'bob',
          status: 'Pending',
          createdAt: '2025-01-01T00:00:00Z',
        },
      }),
    });
    const { getByTestId, findByText } = render(() => <FriendList />);
    fireEvent.input(getByTestId('friend-request-input'), { target: { value: 'bob' } });
    fireEvent.click(getByTestId('friend-request-submit-button'));
    expect(await findByText('Friend request sent!')).toBeInTheDocument();
    expect(calls.calls.some(c => c.method === 'POST' && c.url.includes('/friends/request'))).toBe(true);
  });

  it('shows an error message when sending a friend request fails', async () => {
    mockFetch({
      'GET /api/v1/users/@me/friends': () => ({ status: 200, body: { friendships: [] } }),
      'POST /api/v1/friends/request': () => ({ status: 404, body: { message: 'User not found' } }),
    });
    const { getByTestId, findByText } = render(() => <FriendList />);
    fireEvent.input(getByTestId('friend-request-input'), { target: { value: 'ghost' } });
    fireEvent.click(getByTestId('friend-request-submit-button'));
    expect(await findByText(/User not found|Failed to send request/)).toBeInTheDocument();
  });
});
