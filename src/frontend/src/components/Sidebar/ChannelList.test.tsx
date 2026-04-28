import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ChannelList from './ChannelList';
import { Capability } from '../../types/channel';
import type { Channel } from '../../types/channel';

function makeChannel(id: string, name: string, overrides: Partial<Channel> = {}): Channel {
  return {
    id,
    serverId: 's-1',
    name,
    type: 'Text',
    capabilities: Capability.Chat,
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: new Date(0).toISOString(),
    conversationId: `conv-${id}`,
    ...overrides,
  };
}

function makeProps(overrides: Partial<Parameters<typeof ChannelList>[0]> = {}) {
  return {
    isLoading: false,
    hasAnyChannels: true,
    allChannels: [makeChannel('c-1', 'general'), makeChannel('c-2', 'random')],
    favoriteChannels: [] as Channel[],
    selectedChannelId: null as string | null,
    focusedChannelId: null as string | null,
    getUnreadCount: vi.fn(() => 0),
    isFavorite: vi.fn(() => false),
    onKeyDown: vi.fn(),
    onChannelClick: vi.fn(),
    onChannelFocus: vi.fn(),
    onChannelContextMenu: vi.fn(),
    ...overrides,
  };
}

describe('ChannelList', () => {
  it('renders without crashing with a listbox role', () => {
    const props = makeProps();
    const { getByRole } = render(() => <ChannelList {...props} />);
    const list = getByRole('listbox', { name: 'Channels' });
    expect(list).toBeInTheDocument();
  });

  it('renders each channel in allChannels', () => {
    const props = makeProps();
    const { getByTestId } = render(() => <ChannelList {...props} />);
    expect(getByTestId('channel-item-c-1')).toBeInTheDocument();
    expect(getByTestId('channel-item-c-2')).toBeInTheDocument();
  });

  it('renders a favorites section heading and items when favoriteChannels is non-empty', () => {
    const fav = makeChannel('c-fav', 'starred');
    const props = makeProps({ favoriteChannels: [fav] });
    const { getByTestId } = render(() => <ChannelList {...props} />);
    expect(getByTestId('favorites-section-label')).toHaveTextContent('Favorites');
    expect(getByTestId('channel-item-c-fav')).toBeInTheDocument();
  });

  it('omits the favorites section when there are no favorites', () => {
    const props = makeProps({ favoriteChannels: [] });
    const { queryByTestId } = render(() => <ChannelList {...props} />);
    expect(queryByTestId('favorites-section-label')).toBeNull();
  });

  it('renders skeleton rows only when loading and no channels yet', () => {
    const props = makeProps({
      isLoading: true,
      hasAnyChannels: false,
      allChannels: [],
    });
    const { container } = render(() => <ChannelList {...props} />);
    const skeletons = container.querySelectorAll('[aria-hidden="true"]');
    // 5 skeleton rows are rendered when loading
    expect(skeletons.length).toBeGreaterThanOrEqual(5);
  });

  it('forwards channel clicks to onChannelClick with the channel object', () => {
    const onChannelClick = vi.fn();
    const props = makeProps({ onChannelClick });
    const { getByTestId } = render(() => <ChannelList {...props} />);
    fireEvent.click(getByTestId('channel-item-c-1'));
    expect(onChannelClick).toHaveBeenCalledTimes(1);
    expect(onChannelClick.mock.calls[0][0]).toMatchObject({ id: 'c-1', name: 'general' });
  });
});
