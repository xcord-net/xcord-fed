import { describe, it, expect, beforeEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import DmList from './DmList';
import { useDms } from '../stores/dm.store';
import { useAuth } from '../stores/auth.store';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('DmList', () => {
  beforeEach(() => {
    useDms().reset();
    useAuth().reset();
  });

  it('renders the Direct Messages header', async () => {
    mockFetch({
      'GET /api/v1/users/@me/dms': () => ({ status: 200, body: { dmChannels: [] } }),
    });
    const { findByTestId } = renderWithRouter(() => <DmList />);
    expect(await findByTestId('dm-heading')).toHaveTextContent('Direct Messages');
  });

  it('renders New Message and New Group buttons', async () => {
    mockFetch({
      'GET /api/v1/users/@me/dms': () => ({ status: 200, body: { dmChannels: [] } }),
    });
    const { findByTestId } = renderWithRouter(() => <DmList />);
    expect(await findByTestId('dm-new-message-button')).toHaveTextContent('New Message');
    expect(await findByTestId('dm-new-group-button')).toHaveTextContent('New Group');
  });

  it('reveals the username input when New Message is clicked', async () => {
    mockFetch({
      'GET /api/v1/users/@me/dms': () => ({ status: 200, body: { dmChannels: [] } }),
    });
    const { findByTestId, queryByTestId } = renderWithRouter(() => <DmList />);
    expect(queryByTestId('dm-username-input')).toBeNull();
    fireEvent.click(await findByTestId('dm-new-message-button'));
    expect(await findByTestId('dm-username-input')).toBeInTheDocument();
    expect(await findByTestId('dm-start-button')).toBeInTheDocument();
  });

  it('disables Start button when username is empty and enables when typed', async () => {
    mockFetch({
      'GET /api/v1/users/@me/dms': () => ({ status: 200, body: { dmChannels: [] } }),
    });
    const { findByTestId } = renderWithRouter(() => <DmList />);
    fireEvent.click(await findByTestId('dm-new-message-button'));
    const startBtn = (await findByTestId('dm-start-button')) as HTMLButtonElement;
    expect(startBtn.disabled).toBe(true);
    const input = (await findByTestId('dm-username-input')) as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'alice' } });
    await waitFor(() => expect(startBtn.disabled).toBe(false));
  });

  it('renders an existing DM channel from the store', async () => {
    mockFetch({
      'GET /api/v1/users/@me/dms': () => ({
        status: 200,
        body: {
          dmChannels: [
            {
              id: 'dm-1',
              conversationId: 'conv-1',
              isGroup: false,
              members: [
                { userId: 'u-2', username: 'bob', displayName: 'Bob', joinedAt: '2025-01-01T00:00:00Z' },
              ],
            },
          ],
        },
      }),
    });
    const { findByTestId } = renderWithRouter(() => <DmList />);
    expect(await findByTestId('dm-channel-item-bob')).toBeInTheDocument();
  });

  it('issues POST /api/v1/dms when Start is clicked with a username', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me/dms': () => ({ status: 200, body: { dmChannels: [] } }),
      'POST /api/v1/dms': () => ({
        status: 200,
        body: {
          id: 'dm-new',
          conversationId: 'conv-new',
          recipientId: 'u-9',
          recipientUsername: 'newuser',
          createdAt: '2025-01-01T00:00:00Z',
        },
      }),
    });
    const { findByTestId } = renderWithRouter(() => <DmList />);
    fireEvent.click(await findByTestId('dm-new-message-button'));
    const input = (await findByTestId('dm-username-input')) as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'newuser' } });
    const startBtn = (await findByTestId('dm-start-button')) as HTMLButtonElement;
    await waitFor(() => expect(startBtn.disabled).toBe(false));
    fireEvent.click(startBtn);
    await waitFor(() =>
      expect(
        calls.calls.some((c) => c.method === 'POST' && c.url === '/api/v1/dms'),
      ).toBe(true),
    );
  });
});
