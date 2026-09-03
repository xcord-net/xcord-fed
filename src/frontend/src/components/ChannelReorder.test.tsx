import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import ChannelReorder from './ChannelReorder';
import { mockFetch } from '../tests/helpers/mockFetch';
import { useChannels } from '../stores/channel.store';

const sampleChannels = [
  {
    id: 'c-1',
    serverId: 's-1',
    name: 'general',
    type: 'Text',
    capabilities: 'Chat',
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z',
    conversationId: 'conv-1',
  },
  {
    id: 'c-2',
    serverId: 's-1',
    categoryId: 'cat-1',
    name: 'voice-1',
    type: 'Voice',
    capabilities: 'Voice',
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z',
    conversationId: 'conv-2',
  },
];

const sampleCategories = [
  { id: 'cat-1', serverId: 's-1', name: 'Voice Channels', position: 0 },
];

async function seedChannels() {
  mockFetch({
    'GET /api/v1/servers/s-1/channels': () => ({
      status: 200,
      body: { channels: sampleChannels, categories: sampleCategories },
    }),
  });
  await useChannels().fetchChannels('s-1');
}

describe('ChannelReorder', () => {
  beforeEach(() => {
    useChannels().reset();
  });

  it('renders empty state when there are no channels or categories', () => {
    const { getByText, getByTestId } = render(() => <ChannelReorder serverId="s-1" />);
    expect(getByTestId('channel-reorder-empty')).toBeInTheDocument();
  });

  it('renders an uncategorized channel row', async () => {
    await seedChannels();
    const { getByTestId } = render(() => <ChannelReorder serverId="s-1" />);
    expect(getByTestId('channel-row-c-1')).toBeInTheDocument();
    expect(getByTestId('drag-handle-c-1')).toBeInTheDocument();
  });

  it('renders a category row with its drag handle', async () => {
    await seedChannels();
    const { getByTestId, getByText } = render(() => <ChannelReorder serverId="s-1" />);
    expect(getByTestId('category-row-cat-1')).toBeInTheDocument();
    expect(getByTestId('drag-handle-cat-cat-1')).toBeInTheDocument();
    expect(getByText('Voice Channels')).toBeInTheDocument();
  });

  it('renders channels nested inside their category', async () => {
    await seedChannels();
    const { getByTestId } = render(() => <ChannelReorder serverId="s-1" />);
    expect(getByTestId('channel-row-c-2')).toBeInTheDocument();
  });

  it('marks channel rows as draggable', async () => {
    await seedChannels();
    const { getByTestId } = render(() => <ChannelReorder serverId="s-1" />);
    const row = getByTestId('channel-row-c-1') as HTMLElement;
    expect(row.draggable).toBe(true);
  });

  it('reactively renders newly added channels in the store', async () => {
    await seedChannels();
    const { findByTestId, queryByTestId } = render(() => <ChannelReorder serverId="s-1" />);
    expect(queryByTestId('channel-row-c-3')).toBeNull();
    useChannels().addChannel({
      id: 'c-3',
      serverId: 's-1',
      name: 'new-channel',
      type: 'Text',
      capabilities: 1,
      position: 1,
      isNsfw: false,
      slowModeSeconds: 0,
      createdAt: '2025-01-01T00:00:00Z',
      conversationId: 'conv-3',
    });
    expect(await findByTestId('channel-row-c-3')).toBeInTheDocument();
  });
});
