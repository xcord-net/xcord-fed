import { describe, it, expect, beforeEach } from 'vitest';
import { fireEvent } from '@solidjs/testing-library';
import ChannelDirectory from './ChannelDirectory';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import { Capability, type Channel } from '../types/channel';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

function makeChannel(over: Partial<Channel> = {}): Channel {
  return {
    id: 'c-1',
    serverId: 's-1',
    name: 'general',
    type: 'Text',
    capabilities: Capability.Chat,
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z',
    conversationId: 'conv-1',
    ...over,
  };
}

describe('ChannelDirectory', () => {
  beforeEach(() => {
    useChannels().reset();
    useServers().reset();
  });

  it('falls back to "Channels" when server not loaded', () => {
    const { getByText } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    expect(getByText('Channels')).toBeInTheDocument();
  });

  it('renders empty state when no channels', () => {
    const { getByText } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    expect(getByText('No channels yet')).toBeInTheDocument();
  });

  it('renders uncategorized channel cards sorted by position', () => {
    const channels = useChannels();
    channels.addChannel(makeChannel({ id: 'c-2', name: 'random', position: 1 }));
    channels.addChannel(makeChannel({ id: 'c-1', name: 'general', position: 0 }));
    const { getByTestId, getByText } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    expect(getByTestId('channel-directory-card-c-1')).toBeInTheDocument();
    expect(getByText('general')).toBeInTheDocument();
    expect(getByText('random')).toBeInTheDocument();
  });

  it('clicking a channel card does not throw (navigation handled by router)', () => {
    useChannels().addChannel(makeChannel({ id: 'c-9', name: 'go-here' }));
    const { getByTestId } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    fireEvent.click(getByTestId('channel-directory-card-c-9'));
  });

  it('renders channel topic when present', () => {
    useChannels().addChannel(makeChannel({ id: 'c-5', name: 'help', topic: 'Ask questions here' }));
    const { getByText } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    expect(getByText('Ask questions here')).toBeInTheDocument();
  });

  it('renders server name once servers are loaded', async () => {
    mockFetch({
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [{ id: 's-1', name: 'My Server', ownerId: 'u-1' }] } }),
    });
    await useServers().fetchServers();
    const { getByText } = renderWithRouter(() => <ChannelDirectory serverId="s-1" />);
    expect(getByText('My Server')).toBeInTheDocument();
  });
});
