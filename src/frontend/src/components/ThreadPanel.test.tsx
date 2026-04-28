import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

// MessageList renders nothing here - it pulls from message store and would issue API calls
vi.mock('./MessageList', () => ({
  default: () => <div data-testid="message-list-stub" />,
}));

import ThreadPanel from './ThreadPanel';
import { useThreads } from '../stores/thread.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('ThreadPanel', () => {
  beforeEach(() => {
    useThreads().reset();
  });

  it('renders the panel header', async () => {
    mockFetch({ 'GET /api/v1/channels/ch-1/threads': () => ({ status: 200, body: { threads: [] } }) });
    const { findByText, getByTestId } = render(() => <ThreadPanel channelId="ch-1" />);
    expect(getByTestId('thread-panel')).toBeInTheDocument();
    expect(await findByText('Threads')).toBeInTheDocument();
  });

  it('shows the empty state when there are no threads', async () => {
    mockFetch({ 'GET /api/v1/channels/ch-1/threads': () => ({ status: 200, body: { threads: [] } }) });
    const { findByTestId } = render(() => <ThreadPanel channelId="ch-1" />);
    expect(await findByTestId('thread-list-empty')).toHaveTextContent('No active threads');
  });

  it('renders a thread list item with name and message count', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/threads': () => ({
        status: 200,
        body: {
          threads: [{
            id: 't-1', conversationId: 'conv-1', channelId: 'ch-1',
            title: 'Loud Thread', isArchived: false, isLocked: false,
            messageCount: 3, memberCount: 1, createdAt: '2025-01-01T00:00:00Z',
          }],
        },
      }),
    });
    const { findByTestId, findByText } = render(() => <ThreadPanel channelId="ch-1" />);
    expect(await findByTestId('thread-list-item')).toBeInTheDocument();
    expect(await findByText('Loud Thread')).toBeInTheDocument();
  });

  it('shows active thread compose textarea when a thread is active', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/threads': () => ({
        status: 200,
        body: {
          threads: [{
            id: 't-1', conversationId: 'conv-1', channelId: 'ch-1',
            title: 'My Thread', isArchived: false, isLocked: false,
            messageCount: 5, memberCount: 2, createdAt: '2025-01-01T00:00:00Z',
          }],
        },
      }),
    });
    const { findByTestId } = render(() => <ThreadPanel channelId="ch-1" />);
    // Wait for threads to load, then activate one
    await findByTestId('thread-panel');
    // Give load a microtask tick
    await Promise.resolve();
    await Promise.resolve();
    useThreads().setActiveThread('t-1');
    expect(await findByTestId('thread-compose-input')).toBeInTheDocument();
    expect(await findByTestId('thread-message-list')).toBeInTheDocument();
  });

  it('updates thread compose value on input', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/threads': () => ({
        status: 200,
        body: {
          threads: [{
            id: 't-1', conversationId: 'conv-1', channelId: 'ch-1',
            title: 'My Thread', isArchived: false, isLocked: false,
            messageCount: 0, memberCount: 1, createdAt: '2025-01-01T00:00:00Z',
          }],
        },
      }),
    });
    const { findByTestId } = render(() => <ThreadPanel channelId="ch-1" />);
    await findByTestId('thread-panel');
    await Promise.resolve();
    await Promise.resolve();
    useThreads().setActiveThread('t-1');
    const input = await findByTestId('thread-compose-input') as HTMLTextAreaElement;
    fireEvent.input(input, { target: { value: 'hello thread' } });
    expect(input.value).toBe('hello thread');
  });
});
