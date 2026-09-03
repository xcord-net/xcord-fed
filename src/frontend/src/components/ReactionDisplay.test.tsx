import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, waitFor, render } from '@solidjs/testing-library';

const mockAuthState = { user: { id: 'me-1', username: 'me', email: '' } as { id: string; username: string; email: string } | null };
vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuthState,
}));

import ReactionDisplay from './ReactionDisplay';
import { useMessages } from '../stores/message.store';
import { mockFetch } from '../tests/helpers/mockFetch';
import type { MessageReaction } from '../types/message';

describe('ReactionDisplay', () => {
  beforeEach(() => {
    useMessages().reset();
    mockAuthState.user = { id: 'me-1', username: 'me', email: '' };
  });

  function reactions(): MessageReaction[] {
    return [
      { emoji: 'thumbsup', count: 3, userIds: ['u-1', 'u-2', 'u-3'] },
      { emoji: 'heart', count: 1, userIds: ['me-1'] },
    ];
  }

  it('renders one badge per reaction with counts', () => {
    const { getByTestId } = render(() => (
      <ReactionDisplay reactions={reactions()} messageId="m-1" conversationId="c-1" />
    ));
    expect(getByTestId('reaction-badge-thumbsup')).toBeInTheDocument();
    expect(getByTestId('reaction-count-thumbsup')).toHaveTextContent('3');
    expect(getByTestId('reaction-badge-heart')).toBeInTheDocument();
  });

  it('renders the add-reaction button', () => {
    const { getByTestId } = render(() => (
      <ReactionDisplay reactions={[]} messageId="m-1" conversationId="c-1" />
    ));
    expect(getByTestId('reaction-add-button')).toBeInTheDocument();
  });

  it('opens emoji picker when add button clicked', async () => {
    const { getByTestId, queryByTestId } = render(() => (
      <ReactionDisplay reactions={[]} messageId="m-1" conversationId="c-1" />
    ));
    fireEvent.click(getByTestId('reaction-add-button'));
    await waitFor(() => expect(queryByTestId('emoji-picker')).not.toBeNull());
  });

  it('calls PUT to add reaction when clicking a non-active badge', async () => {
    const calls = mockFetch({
      'PUT /api/v1/conversations/c-1/messages/m-1/reactions/thumbsup': () => ({ status: 204, body: null }),
    });
    const { getByTestId } = render(() => (
      <ReactionDisplay reactions={reactions()} messageId="m-1" conversationId="c-1" />
    ));
    fireEvent.click(getByTestId('reaction-badge-thumbsup'));
    // The PUT is the whole interaction: the new reaction set arrives by
    // announcement to the conversation, so nothing is refetched here.
    await waitFor(() => {
      expect(calls.calls.some(c => c.method === 'PUT' && c.url.includes('/reactions/thumbsup'))).toBe(true);
    });
    expect(calls.calls.some(c => c.method === 'GET')).toBe(false);
  });

  it('calls DELETE to remove reaction when current user is in userIds', async () => {
    const calls = mockFetch({
      'DELETE /api/v1/conversations/c-1/messages/m-1/reactions/heart': () => ({ status: 204, body: null }),
    });
    const { getByTestId } = render(() => (
      <ReactionDisplay reactions={reactions()} messageId="m-1" conversationId="c-1" />
    ));
    fireEvent.click(getByTestId('reaction-badge-heart'));
    await waitFor(() => {
      expect(calls.calls.some(c => c.method === 'DELETE' && c.url.includes('/reactions/heart'))).toBe(true);
    });
    expect(calls.calls.some(c => c.method === 'GET')).toBe(false);
  });
});
