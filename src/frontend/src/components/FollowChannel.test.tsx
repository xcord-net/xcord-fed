import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import FollowChannel, { fetchFollows, fetchAvailableChannels, followChannel, unfollowChannel } from './FollowChannel';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleFollow = {
  id: 'sub-1',
  targetChannelId: 'ch-2',
  targetServerId: 's-1',
  targetChannelName: 'general',
  createdAt: '2025-01-01T00:00:00Z',
};

describe('fetchFollows', () => {
  it('returns follows on success', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: [sampleFollow] }) });
    const result = await fetchFollows('s-1', 'ch-1');
    expect(result.follows).toHaveLength(1);
    expect(result.error).toBe('');
  });

  it('returns error message on failure', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 500, body: { message: 'Boom' } }) });
    const result = await fetchFollows('s-1', 'ch-1');
    expect(result.follows).toHaveLength(0);
    expect(result.error).toMatch(/Boom|Failed to load follows/);
  });
});

describe('fetchAvailableChannels', () => {
  it('filters source channel and non-text types', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels': () => ({
        status: 200,
        body: [
          { id: 'ch-1', name: 'self', type: 'Text', serverId: 's-1' },
          { id: 'ch-2', name: 'general', type: 'Text', serverId: 's-1' },
          { id: 'ch-3', name: 'voice', type: 'Voice', serverId: 's-1' },
        ],
      }),
    });
    const result = await fetchAvailableChannels('s-1', 'ch-1');
    expect(result.channels).toHaveLength(1);
    expect(result.channels[0].id).toBe('ch-2');
  });
});

describe('followChannel', () => {
  it('returns validation error when no target channel selected', async () => {
    const result = await followChannel('s-1', 'ch-1', '');
    expect(result.follow).toBeNull();
    expect(result.error).toBe('Please select a channel');
  });

  it('returns follow on success', async () => {
    mockFetch({ 'POST /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: sampleFollow }) });
    const result = await followChannel('s-1', 'ch-1', 'ch-2');
    expect(result.follow?.id).toBe('sub-1');
  });
});

describe('unfollowChannel', () => {
  it('returns success on 204', async () => {
    mockFetch({ 'DELETE /api/v1/servers/s-1/channels/ch-1/followers/sub-1': () => ({ status: 204, body: null }) });
    const result = await unfollowChannel('s-1', 'ch-1', 'sub-1');
    expect(result.success).toBe(true);
  });
});

describe('FollowChannel', () => {
  it('renders nothing for non-Announcement channels', () => {
    const { container } = render(() => (
      <FollowChannel serverId="s-1" channelId="ch-1" channelType="Text" channelName="general" />
    ));
    expect(container.textContent).toBe('');
  });

  it('renders header for Announcement channels', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => (
      <FollowChannel serverId="s-1" channelId="ch-1" channelType="Announcement" channelName="news" />
    ));
    expect(await findByText('Channel Followers')).toBeInTheDocument();
  });

  it('shows empty state when no followers exist', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => (
      <FollowChannel serverId="s-1" channelId="ch-1" channelType="Announcement" channelName="news" />
    ));
    expect(await findByText(/No channels are following news yet/)).toBeInTheDocument();
  });

  it('renders existing follower entries', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: [sampleFollow] }) });
    const { findByText } = render(() => (
      <FollowChannel serverId="s-1" channelId="ch-1" channelType="Announcement" channelName="news" />
    ));
    expect(await findByText('#general')).toBeInTheDocument();
  });

  it('opens follow dialog when "Follow in another channel" clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels/ch-1/followers': () => ({ status: 200, body: [] }),
      'GET /api/v1/servers/s-1/channels': () => ({ status: 200, body: [] }),
    });
    const { findByText, container } = render(() => (
      <FollowChannel serverId="s-1" channelId="ch-1" channelType="Announcement" channelName="news" />
    ));
    fireEvent.click(await findByText('Follow in another channel'));
    await waitFor(() => expect(container.textContent).toContain('Follow #news'));
  });
});
