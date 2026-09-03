import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent } from '@solidjs/testing-library';
import { createSignal } from 'solid-js';
import { renderWithRouter } from '../../tests/helpers/renderWithRouter';

// ---- Mock the wiring hook so the test never invokes API/SignalR side effects ----
const [hubUrl, setHubUrl] = createSignal<string | null>(null);
vi.mock('./useLayoutWiring', () => ({
  useLayoutWiring: () => ({ hubUrl }),
}));

// ---- Mock store boundaries that Layout reads directly ----
const channelStoreMock = {
  channels: [] as Array<{ id: string; name?: string; conversationId?: string; topic?: string }>,
  selectedChannelId: null as string | null,
};
vi.mock('../../stores/channel.store', () => ({
  useChannels: () => channelStoreMock,
}));

const dmStoreMock = {
  dmChannels: [] as Array<{ id: string; conversationId?: string }>,
};
vi.mock('../../stores/dm.store', () => ({
  useDms: () => dmStoreMock,
}));

const modalsMock = {
  showChannelSettings: false,
  showGroupManager: false,
  mobileNavOpen: false,
  closeChannelSettings: vi.fn(),
  closeGroupManager: vi.fn(),
  toggleMobileNav: vi.fn(),
  closeMobileNav: vi.fn(),
  openSettings: vi.fn(),
  closeAll: vi.fn(),
};
vi.mock('../../stores/modal.store', () => ({
  useModals: () => modalsMock,
}));

const serverStoreMock = {
  selectedServerId: null as string | null,
};
vi.mock('../../stores/server.store', () => ({
  useServers: () => serverStoreMock,
}));

const signalRMock = {
  isConnected: false,
  currentConversations: new Set<string>(),
};
vi.mock('../../stores/signalr.store', () => ({
  useSignalR: () => signalRMock,
}));

// ---- Stub children so the test focuses on Layout's branching ----
vi.mock('../ChannelDirectory', () => ({
  default: (p: { serverId: string }) => (
    <div data-testid="mock-channel-directory">dir:{p.serverId}</div>
  ),
}));
vi.mock('../ChannelSettings', () => ({
  default: () => <div data-testid="mock-channel-settings" />,
}));
vi.mock('../GroupManager', () => ({
  default: (p: { serverId: string }) => (
    <div data-testid="mock-group-manager">{p.serverId}</div>
  ),
}));
vi.mock('../HubHeader', () => ({
  default: (p: { hubUrl: string; instanceUrl: string }) => (
    <div data-testid="mock-hub-header" data-hub-url={p.hubUrl} data-instance-url={p.instanceUrl} />
  ),
}));
vi.mock('../Deck/Deck', () => ({
  default: (p: { children?: unknown }) => (
    <div data-testid="mock-deck">{p.children as never}</div>
  ),
}));

// Layout now owns the version lookup that used to live in the sidebar.
vi.mock('../../api/client', () => ({
  api: { get: vi.fn().mockResolvedValue({ currentVersion: '1.2.3' }) },
}));
vi.mock('../../stores/auth.store', () => ({
  useAuth: () => ({ user: null, logout: vi.fn() }),
}));
vi.mock('./ChannelHeader', () => ({
  default: (p: { channelName?: string }) => (
    <div data-testid="mock-channel-header">{p.channelName}</div>
  ),
}));
vi.mock('./DmView', () => ({
  default: (p: { channelId?: string; dmConversationId?: string }) => (
    <div data-testid="mock-dm-view" data-channel-id={p.channelId} data-conv-id={p.dmConversationId} />
  ),
}));
vi.mock('./MessagesArea', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-messages-area">msgs:{p.conversationId}</div>
  ),
}));
vi.mock('./RightPanels', () => ({
  default: () => <div data-testid="mock-right-panels" />,
}));
vi.mock('./SettingsModal', () => ({
  default: () => <div data-testid="mock-settings-modal" />,
}));

import Layout from './Layout';

describe('Layout', () => {
  beforeEach(() => {
    setHubUrl(null);
    channelStoreMock.channels = [];
    channelStoreMock.selectedChannelId = null;
    dmStoreMock.dmChannels = [];
    modalsMock.showChannelSettings = false;
    modalsMock.showGroupManager = false;
    modalsMock.closeChannelSettings.mockClear();
    modalsMock.closeGroupManager.mockClear();
    serverStoreMock.selectedServerId = null;
    signalRMock.isConnected = false;
    signalRMock.currentConversations = new Set<string>();
  });

  it('renders the deck shell always', () => {
    const { getByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels', routePath: '/channels' },
    );
    expect(getByTestId('mock-deck')).toBeInTheDocument();
  });

  it('toggles the mobile nav drawer when the hamburger is clicked', () => {
    const { getByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels', routePath: '/channels' },
    );
    fireEvent.click(getByTestId('mobile-nav-toggle'));
    expect(modalsMock.toggleMobileNav).toHaveBeenCalled();
  });

  it('does not render the HubHeader when hubUrl is null', () => {
    const { queryByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels', routePath: '/channels' },
    );
    expect(queryByTestId('mock-hub-header')).toBeNull();
  });

  it('renders the HubHeader when hubUrl is set', () => {
    setHubUrl('https://hub.example.com');
    const { getByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels', routePath: '/channels' },
    );
    expect(getByTestId('mock-hub-header').getAttribute('data-hub-url')).toBe('https://hub.example.com');
  });

  it('renders DmView when the route serverId is "me"', () => {
    const { getByTestId, queryByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels/me', routePath: '/channels/:serverId' },
    );
    expect(getByTestId('mock-dm-view')).toBeInTheDocument();
    expect(queryByTestId('mock-channel-directory')).toBeNull();
  });

  it('renders ChannelDirectory when a serverId is present but no channelId', () => {
    const { getByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels/s-1', routePath: '/channels/:serverId' },
    );
    expect(getByTestId('mock-channel-directory')).toHaveTextContent('dir:s-1');
  });

  it('renders MessagesArea when a channelId is present and the channel has a conversationId', () => {
    channelStoreMock.channels = [{ id: 'ch-1', name: 'general', conversationId: 'conv-1' }];
    channelStoreMock.selectedChannelId = 'ch-1';
    const { getByTestId } = renderWithRouter(
      () => <Layout />,
      { path: '/channels/s-1/ch-1', routePath: '/channels/:serverId/:channelId' },
    );
    expect(getByTestId('mock-messages-area')).toHaveTextContent('msgs:conv-1');
    expect(getByTestId('mock-right-panels')).toBeInTheDocument();
  });

  it('exposes signalr-connected/joined data attributes on the root', () => {
    signalRMock.isConnected = true;
    signalRMock.currentConversations = new Set<string>(['conv-1']);
    channelStoreMock.channels = [{ id: 'ch-1', conversationId: 'conv-1' }];
    channelStoreMock.selectedChannelId = 'ch-1';
    const { container } = renderWithRouter(
      () => <Layout />,
      { path: '/channels/s-1/ch-1', routePath: '/channels/:serverId/:channelId' },
    );
    const root = container.querySelector('[data-signalr-connected]');
    expect(root?.getAttribute('data-signalr-connected')).toBe('true');
    expect(root?.getAttribute('data-signalr-conversation-joined')).toBe('true');
  });
});
