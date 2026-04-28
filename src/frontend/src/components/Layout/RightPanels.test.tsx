import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';

// Stub heavy panel children - this component is purely a router that picks
// which panel to mount based on the modal store.
vi.mock('../PinList', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-pin-list">pins:{p.conversationId}</div>
  ),
}));
vi.mock('../ScheduledEvents', () => ({
  default: (p: { serverId: string }) => (
    <div data-testid="mock-scheduled-events">events:{p.serverId}</div>
  ),
}));
vi.mock('../ScheduledMessages', () => ({
  default: (p: { channelId: string }) => (
    <div data-testid="mock-scheduled-messages">scheduled:{p.channelId}</div>
  ),
}));
vi.mock('../SearchPanel', () => ({
  default: () => <div data-testid="mock-search-panel" />,
}));
vi.mock('../ThreadPanel', () => ({
  default: (p: { channelId: string }) => (
    <div data-testid="mock-thread-panel">threads:{p.channelId}</div>
  ),
}));

import RightPanels from './RightPanels';
import { useChannels } from '../../stores/channel.store';
import { useModals } from '../../stores/modal.store';
import { useServers } from '../../stores/server.store';

describe('RightPanels', () => {
  beforeEach(() => {
    useChannels().reset();
    useModals().reset();
    useServers().reset();
  });

  it('renders nothing when no modal flag is set', () => {
    const { queryByTestId } = render(() => (
      <RightPanels conversationId="conv-1" channelId="ch-1" />
    ));
    expect(queryByTestId('mock-search-panel')).toBeNull();
    expect(queryByTestId('mock-pin-list')).toBeNull();
    expect(queryByTestId('mock-thread-panel')).toBeNull();
  });

  it('renders the SearchPanel when modals.showSearch is true', () => {
    useModals().toggleSearch();
    const { getByTestId } = render(() => (
      <RightPanels conversationId="conv-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-search-panel')).toBeInTheDocument();
  });

  it('renders the PinList only when both showPins is set AND a conversationId is provided', () => {
    useModals().togglePins();
    const { getByTestId } = render(() => (
      <RightPanels conversationId="conv-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-pin-list')).toHaveTextContent('pins:conv-1');
  });

  it('omits the PinList when showPins is set but conversationId is undefined', () => {
    useModals().togglePins();
    const { queryByTestId } = render(() => (
      <RightPanels conversationId={undefined} channelId="ch-1" />
    ));
    expect(queryByTestId('mock-pin-list')).toBeNull();
  });

  it('renders the ThreadPanel with the given channelId when showThreads is true', () => {
    useModals().toggleThreads();
    const { getByTestId } = render(() => (
      <RightPanels conversationId="conv-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-thread-panel')).toHaveTextContent('threads:ch-1');
  });

  it('renders the ScheduledMessages panel when a channel is selected and showScheduledMessages is true', () => {
    useChannels().selectChannel('ch-7');
    useModals().toggleScheduledMessages();
    const { getByTestId } = render(() => (
      <RightPanels conversationId="conv-1" channelId="ch-7" />
    ));
    expect(getByTestId('mock-scheduled-messages')).toHaveTextContent('scheduled:ch-7');
  });
});
