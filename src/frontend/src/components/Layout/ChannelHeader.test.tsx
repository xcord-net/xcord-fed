import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

// Mock the auth store so we can flip isAdmin without going through the real
// login flow (which needs the network + profile fetch).
const mockAuthState: { user: { id: string; username: string; email: string; isAdmin?: boolean } | null } = {
  user: null,
};
vi.mock('../../stores/auth.store', () => ({
  useAuth: () => mockAuthState,
}));

import ChannelHeader from './ChannelHeader';
import { useModals } from '../../stores/modal.store';

describe('ChannelHeader', () => {
  beforeEach(() => {
    useModals().reset();
    mockAuthState.user = { id: 'me-1', username: 'me', email: '', isAdmin: false };
  });

  it('renders the channel name and topic when provided', () => {
    const { getByText } = render(() => (
      <ChannelHeader channelName="general" channelTopic="say hi" />
    ));
    expect(getByText('general')).toBeInTheDocument();
    expect(getByText('say hi')).toBeInTheDocument();
  });

  it('omits the topic span when channelTopic is null', () => {
    const { container } = render(() => (
      <ChannelHeader channelName="general" channelTopic={null} />
    ));
    // Only one h2 + the action buttons - no topic span sibling.
    expect(container.querySelectorAll('span').length).toBe(0);
  });

  it('toggles the pins panel via the modal store when the pin button is clicked', () => {
    const { getByTestId } = render(() => (
      <ChannelHeader channelName="general" channelTopic={null} />
    ));
    expect(useModals().showPins).toBe(false);
    fireEvent.click(getByTestId('pins-button'));
    expect(useModals().showPins).toBe(true);
  });

  it('opens server settings via openServerSettings when admin clicks the shield', () => {
    mockAuthState.user = { id: 'admin-1', username: 'admin', email: '', isAdmin: true };
    const { getByTestId } = render(() => (
      <ChannelHeader channelName="general" channelTopic={null} />
    ));
    expect(useModals().showServerSettings).toBe(false);
    fireEvent.click(getByTestId('server-settings-button'));
    expect(useModals().showServerSettings).toBe(true);
  });

  it('hides the server-settings button for non-admin users', () => {
    const { queryByTestId } = render(() => (
      <ChannelHeader channelName="general" channelTopic={null} />
    ));
    expect(queryByTestId('server-settings-button')).toBeNull();
  });

  it('renders all standard action buttons (pins, threads, search, channel settings)', () => {
    const { getByTestId } = render(() => (
      <ChannelHeader channelName="general" channelTopic={null} />
    ));
    expect(getByTestId('pins-button')).toBeInTheDocument();
    expect(getByTestId('threads-button')).toBeInTheDocument();
    expect(getByTestId('search-button')).toBeInTheDocument();
    expect(getByTestId('channel-settings-button')).toBeInTheDocument();
  });
});
