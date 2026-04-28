import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ChannelSettings from './ChannelSettings';
import { useChannels } from '../stores/channel.store';
import { Capability } from '../types/channel';
import { mockFetch } from '../tests/helpers/mockFetch';

vi.mock('./ChannelPermissions', () => ({
  default: () => <div data-testid="stub-channel-permissions" />,
}));
vi.mock('./StreambotManager', () => ({
  default: () => <div data-testid="stub-streambot-manager" />,
}));

function seedChannel() {
  useChannels().reset();
  useChannels().addChannel({
    id: 'c-1',
    serverId: 's-1',
    conversationId: 'conv-1',
    name: 'general',
    type: 'Text',
    capabilities: Capability.Chat,
    position: 0,
    topic: '',
    slowModeSeconds: 0,
    isNsfw: false,
  } as never);
}

describe('ChannelSettings', () => {
  beforeEach(() => {
    seedChannel();
  });

  it('renders the dialog with the Channel Settings header', () => {
    mockFetch({});
    const { getByText, getByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => {}} />
    ));
    expect(getByText('Channel Settings')).toBeInTheDocument();
    expect(getByTestId('channel-settings-close-button')).toBeInTheDocument();
  });

  it('renders all three core tabs (overview, permissions, access)', () => {
    mockFetch({});
    const { getByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => {}} />
    ));
    expect(getByTestId('channel-settings-tab-overview')).toBeInTheDocument();
    expect(getByTestId('channel-settings-tab-permissions')).toBeInTheDocument();
    expect(getByTestId('channel-settings-tab-access')).toBeInTheDocument();
  });

  it('switches to Permissions tab and renders the (mocked) panel', async () => {
    mockFetch({});
    const { getByTestId, findByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => {}} />
    ));
    fireEvent.click(getByTestId('channel-settings-tab-permissions'));
    expect(await findByTestId('stub-channel-permissions')).toBeInTheDocument();
  });

  it('loads groups when the Access tab is clicked', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/groups': () => ({
        status: 200,
        body: [{ id: 'g-1', name: 'Members' }],
      }),
    });
    const { getByTestId, findByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => {}} />
    ));
    fireEvent.click(getByTestId('channel-settings-tab-access'));
    await waitFor(() =>
      expect(
        calls.calls.some(
          (c) => c.method === 'GET' && c.url === '/api/v1/servers/s-1/groups',
        ),
      ).toBe(true),
    );
    expect(await findByTestId('channel-access-group-select')).toBeInTheDocument();
  });

  it('invokes onClose when the close button is clicked', () => {
    mockFetch({});
    let closed = false;
    const { getByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => { closed = true; }} />
    ));
    fireEvent.click(getByTestId('channel-settings-close-button'));
    expect(closed).toBe(true);
  });

  it('shows the delete-channel confirmation dialog when Delete Channel is clicked', async () => {
    mockFetch({});
    const { getByTestId, findByTestId } = render(() => (
      <ChannelSettings serverId="s-1" channelId="c-1" onClose={() => {}} />
    ));
    fireEvent.click(getByTestId('delete-channel-button'));
    expect(await findByTestId('delete-channel-confirm-button')).toBeInTheDocument();
    expect(await findByTestId('delete-channel-cancel-button')).toBeInTheDocument();
  });
});
