import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ChannelItem from './ChannelItem';
import { Capability } from '../../types/channel';
import type { Channel } from '../../types/channel';

function makeChannel(overrides: Partial<Channel> = {}): Channel {
  return {
    id: 'c-1',
    serverId: 's-1',
    name: 'general',
    type: 'Text',
    capabilities: Capability.Chat,
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: new Date(0).toISOString(),
    conversationId: 'conv-1',
    ...overrides,
  };
}

function makeProps(overrides: Partial<Parameters<typeof ChannelItem>[0]> = {}) {
  return {
    channel: makeChannel(),
    isSelected: false,
    isFocused: false,
    isFavorite: false,
    unreadCount: 0,
    noFocusedChannel: false,
    onClick: vi.fn(),
    onFocus: vi.fn(),
    onContextMenu: vi.fn(),
    ...overrides,
  };
}

describe('ChannelItem', () => {
  it('renders without crashing and shows the channel name', () => {
    const props = makeProps();
    const { getByTestId, getByText } = render(() => <ChannelItem {...props} />);
    expect(getByTestId('channel-item-c-1')).toBeInTheDocument();
    expect(getByText('general')).toBeInTheDocument();
  });

  it('uses the correct aria-label for a text channel', () => {
    const props = makeProps();
    const { getByTestId } = render(() => <ChannelItem {...props} />);
    expect(getByTestId('channel-item-c-1').getAttribute('aria-label')).toBe(
      'Text channel general',
    );
  });

  it('uses voice channel aria-label when capabilities include Voice', () => {
    const props = makeProps({
      channel: makeChannel({ capabilities: Capability.Voice, name: 'lobby' }),
    });
    const { getByTestId } = render(() => <ChannelItem {...props} />);
    expect(getByTestId('channel-item-c-1').getAttribute('aria-label')).toBe(
      'Voice channel lobby',
    );
  });

  it('renders an unread badge with the count when unreadCount > 0', () => {
    const props = makeProps({ unreadCount: 5 });
    const { getByLabelText } = render(() => <ChannelItem {...props} />);
    const badge = getByLabelText('5 unread messages');
    expect(badge).toHaveTextContent('5');
  });

  it('clamps unread badge text to 99+ when the count is large', () => {
    const props = makeProps({ unreadCount: 250 });
    const { getByLabelText } = render(() => <ChannelItem {...props} />);
    expect(getByLabelText('250 unread messages')).toHaveTextContent('99+');
  });

  it('marks selected channels with aria-selected and the favorited data attribute', () => {
    const props = makeProps({ isSelected: true, isFavorite: true });
    const { getByTestId } = render(() => <ChannelItem {...props} />);
    const button = getByTestId('channel-item-c-1');
    expect(button.getAttribute('aria-selected')).toBe('true');
    expect(button.getAttribute('data-favorited')).toBe('true');
  });

  it('invokes onClick when the row is clicked', () => {
    const onClick = vi.fn();
    const props = makeProps({ onClick });
    const { getByTestId } = render(() => <ChannelItem {...props} />);
    fireEvent.click(getByTestId('channel-item-c-1'));
    expect(onClick).toHaveBeenCalledOnce();
  });
});
