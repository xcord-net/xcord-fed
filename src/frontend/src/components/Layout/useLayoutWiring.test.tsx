import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createSignal } from 'solid-js';
import { renderWithRouter } from '../../tests/helpers/renderWithRouter';

// ---- Mock every boundary the wiring hook touches ----
vi.mock('../../api/client', () => ({
  api: { get: vi.fn().mockResolvedValue({ hubUrl: null }) },
}));

vi.mock('../../services/notification.service', () => ({
  requestPermission: vi.fn().mockResolvedValue('granted'),
}));

const [user, setUser] = createSignal<{ id: string } | null>(null);
vi.mock('../../stores/auth.store', () => ({
  useAuth: () => ({ get user() { return user(); } }),
}));

const channelStoreMock = {
  channels: [] as Array<{ id: string; conversationId?: string }>,
  selectedChannelId: null as string | null,
  fetchChannels: vi.fn().mockResolvedValue(undefined),
  selectChannel: vi.fn(),
};
vi.mock('../../stores/channel.store', () => ({ useChannels: () => channelStoreMock }));

const dmStoreMock = { loadDms: vi.fn() };
vi.mock('../../stores/dm.store', () => ({ useDms: () => dmStoreMock }));

const memberStoreMock = { fetchMembers: vi.fn() };
vi.mock('../../stores/member.store', () => ({ useMembers: () => memberStoreMock }));

const messageStoreMock = { clearMessages: vi.fn() };
vi.mock('../../stores/message.store', () => ({ useMessages: () => messageStoreMock }));

const modalsMock = { selectForumPost: vi.fn() };
vi.mock('../../stores/modal.store', () => ({ useModals: () => modalsMock }));

const serverStoreMock = {
  servers: [] as Array<{ id: string }>,
  fetchServers: vi.fn().mockResolvedValue(undefined),
  selectServer: vi.fn(),
};
vi.mock('../../stores/server.store', () => ({ useServers: () => serverStoreMock }));

const signalRMock = {
  isConnected: false,
  connectSignalR: vi.fn().mockResolvedValue(undefined),
  joinConversation: vi.fn().mockResolvedValue(undefined),
  leaveConversation: vi.fn().mockResolvedValue(undefined),
  setActiveConversationId: vi.fn(),
  setCurrentUserId: vi.fn(),
};
vi.mock('../../stores/signalr.store', () => ({ useSignalR: () => signalRMock }));

const unreadStoreMock = { markRead: vi.fn() };
vi.mock('../../stores/unread.store', () => ({ useUnread: () => unreadStoreMock }));

import { useLayoutWiring } from './useLayoutWiring';

function renderWiring() {
  return renderWithRouter(() => {
    useLayoutWiring();
    return <div data-testid="wired" />;
  });
}

describe('useLayoutWiring', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setUser(null);
  });

  // Regression: the SignalR store suppresses the local user's own typing echo and
  // own-message notifications by comparing against currentUserId. Nothing ever set
  // it, so it stayed null and users saw their own "is typing" banner.
  it('publishes the authenticated user id to the SignalR store', () => {
    setUser({ id: '123456789' });
    renderWiring();
    expect(signalRMock.setCurrentUserId).toHaveBeenCalledWith('123456789');
  });

  it('republishes the user id when auth resolves after mount', () => {
    renderWiring();
    expect(signalRMock.setCurrentUserId).toHaveBeenCalledWith(null);

    setUser({ id: '987654321' });
    expect(signalRMock.setCurrentUserId).toHaveBeenCalledWith('987654321');
  });
});
