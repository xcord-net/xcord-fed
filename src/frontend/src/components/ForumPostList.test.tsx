import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ForumPostList, { addTag, removeTag, formatRelativeTime } from './ForumPostList';
import { useForums } from '../stores/forum.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('ForumPostList pure helpers', () => {
  it('addTag appends a new tag and ignores duplicates / blanks', () => {
    expect(addTag(['bug'], 'feature')).toEqual(['bug', 'feature']);
    expect(addTag(['bug'], 'bug')).toEqual(['bug']);
    expect(addTag(['bug'], '   ')).toEqual(['bug']);
  });

  it('removeTag filters the named tag out of the list', () => {
    expect(removeTag(['a', 'b', 'c'], 'b')).toEqual(['a', 'c']);
  });

  it('formatRelativeTime returns "No activity" when undefined', () => {
    expect(formatRelativeTime(undefined)).toBe('No activity');
  });

  it('formatRelativeTime formats hours-ago timestamps', () => {
    const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
    expect(formatRelativeTime(twoHoursAgo)).toBe('2h ago');
  });
});

describe('ForumPostList', () => {
  beforeEach(() => {
    useForums().reset();
  });

  it('renders the Forum Posts header and New Post button', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({ status: 200, body: { posts: [] } }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    expect(await findByText('Forum Posts')).toBeInTheDocument();
    expect(await findByTestId('forum-new-post-button')).toBeInTheDocument();
  });

  it('shows the empty state when no posts are returned', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({ status: 200, body: { posts: [] } }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    expect(await findByTestId('forum-empty-state')).toBeInTheDocument();
  });

  it('renders a post item when posts are returned', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({
        status: 200,
        body: {
          posts: [
            {
              threadId: 't-1',
              conversationId: 'conv-1',
              channelId: 'c-1',
              title: 'My First Post',
              authorUsername: 'alice',
              firstMessagePreview: 'Hi',
              tags: ['intro'],
              messageCount: 3,
              isArchived: false,
              isLocked: false,
              lastActivityAt: new Date().toISOString(),
              createdAt: new Date().toISOString(),
            },
          ],
        },
      }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
    });
    const { findAllByTestId, findByText } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    const items = await findAllByTestId('forum-post-item');
    expect(items.length).toBe(1);
    expect(await findByText('My First Post')).toBeInTheDocument();
  });

  it('clicking New Post opens the create form', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({ status: 200, body: { posts: [] } }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    fireEvent.click(await findByTestId('forum-new-post-button'));
    expect(await findByTestId('forum-create-post-form')).toBeInTheDocument();
  });

  it('disables the Create Post submit button when title or content is empty', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({ status: 200, body: { posts: [] } }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    fireEvent.click(await findByTestId('forum-new-post-button'));
    const submit = (await findByTestId('forum-create-post-submit')) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
    fireEvent.input(await findByTestId('forum-post-title-input'), {
      target: { value: 'Hello' },
    });
    fireEvent.input(await findByTestId('forum-post-content-input'), {
      target: { value: 'Body' },
    });
    await waitFor(() => expect(submit.disabled).toBe(false));
  });

  it('calls POST /posts when Create Post is clicked with valid input', async () => {
    const calls = mockFetch({
      'GET /api/v1/channels/c-1/posts': () => ({ status: 200, body: { posts: [] } }),
      'GET /api/v1/channels/c-1/tags': () => ({ status: 200, body: [] }),
      'POST /api/v1/channels/c-1/posts': () => ({
        status: 200,
        body: {
          threadId: 't-1',
          conversationId: 'conv-1',
          channelId: 'c-1',
          title: 'Hello',
          authorUsername: 'me',
          firstMessageId: 'm-1',
          tags: [],
          createdAt: new Date().toISOString(),
        },
      }),
    });
    const { findByTestId } = render(() => (
      <ForumPostList serverId="s-1" channelId="c-1" />
    ));
    fireEvent.click(await findByTestId('forum-new-post-button'));
    fireEvent.input(await findByTestId('forum-post-title-input'), {
      target: { value: 'Hello' },
    });
    fireEvent.input(await findByTestId('forum-post-content-input'), {
      target: { value: 'Body' },
    });
    const submit = (await findByTestId('forum-create-post-submit')) as HTMLButtonElement;
    await waitFor(() => expect(submit.disabled).toBe(false));
    fireEvent.click(submit);
    await waitFor(() => {
      expect(
        calls.calls.some(
          c => c.method === 'POST' && c.url === '/api/v1/channels/c-1/posts',
        ),
      ).toBe(true);
    });
  });
});
