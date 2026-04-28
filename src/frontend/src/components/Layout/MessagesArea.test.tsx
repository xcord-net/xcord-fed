import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

// Stub heavy children to keep the test focused on MessagesArea's branching
// logic (capability-driven layout choices and edit-bar replacement).
vi.mock('../BroadcastChannel', () => ({
  default: (p: { channelId: string }) => (
    <div data-testid="mock-broadcast">broadcast:{p.channelId}</div>
  ),
}));
vi.mock('../ForumPostList', () => ({
  default: (p: { serverId: string; channelId: string }) => (
    <div data-testid="mock-forum-post-list">forum:{p.serverId}/{p.channelId}</div>
  ),
}));
vi.mock('../MessageCompose', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-message-compose">compose:{p.conversationId}</div>
  ),
}));
vi.mock('../MessageList', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-message-list">list:{p.conversationId}</div>
  ),
}));
vi.mock('../ScreenShareViewer', () => ({
  default: () => <div data-testid="mock-screenshare" />,
}));
vi.mock('../TypingIndicator', () => ({
  default: () => <div data-testid="mock-typing" />,
}));

import MessagesArea from './MessagesArea';
import { useChannels } from '../../stores/channel.store';
import { useMessages } from '../../stores/message.store';
import { useModals } from '../../stores/modal.store';
import { Capability } from '../../types/channel';
import type { Channel } from '../../types/channel';
import type { ForumPost } from '../../types/forum';

function makePost(overrides: Partial<ForumPost> = {}): ForumPost {
  return {
    id: 'p-1',
    channelId: 'ch-1',
    conversationId: 'conv-thread-1',
    title: 'Welcome thread',
    tags: [],
    createdAt: '2025-01-01T00:00:00Z',
    ...overrides,
  };
}

function makeChannel(overrides: Partial<Channel> = {}): Channel {
  return {
    id: 'ch-1',
    serverId: 's-1',
    name: 'general',
    type: 'Text',
    capabilities: Capability.Chat,
    position: 0,
    isNsfw: false,
    slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z',
    conversationId: 'conv-1',
    ...overrides,
  };
}

describe('MessagesArea', () => {
  beforeEach(() => {
    useChannels().reset();
    useMessages().reset();
    useModals().reset();
  });

  it('renders the chat layout (message list + compose) for a normal text channel', () => {
    useChannels().addChannel(makeChannel());
    useChannels().selectChannel('ch-1');
    const { getByTestId, queryByTestId } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-message-list')).toHaveTextContent('list:conv-1');
    expect(getByTestId('mock-message-compose')).toHaveTextContent('compose:conv-1');
    expect(queryByTestId('mock-broadcast')).toBeNull();
    expect(queryByTestId('mock-forum-post-list')).toBeNull();
  });

  it('renders BroadcastChannel when the channel has the Streaming capability', () => {
    useChannels().addChannel(makeChannel({ capabilities: Capability.Chat | Capability.Streaming }));
    useChannels().selectChannel('ch-1');
    const { getByTestId } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-broadcast')).toHaveTextContent('broadcast:ch-1');
  });

  it('renders ForumPostList for a forum channel without a selected post', () => {
    useChannels().addChannel(makeChannel({ capabilities: Capability.Forum, type: 'Forum' }));
    useChannels().selectChannel('ch-1');
    const { getByTestId, queryByTestId } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    expect(getByTestId('mock-forum-post-list')).toHaveTextContent('forum:s-1/ch-1');
    // The non-forum chat layout must NOT also render.
    expect(queryByTestId('mock-screenshare')).toBeNull();
  });

  it('renders the forum thread view when a forum post is selected', () => {
    useChannels().addChannel(makeChannel({ capabilities: Capability.Forum, type: 'Forum' }));
    useChannels().selectChannel('ch-1');
    useModals().selectForumPost(makePost());
    const { getByTestId, getByText } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    expect(getByTestId('forum-thread-view')).toBeInTheDocument();
    expect(getByText('Welcome thread')).toBeInTheDocument();
    expect(getByTestId('mock-message-list')).toHaveTextContent('list:conv-thread-1');
  });

  it('clears the selected forum post when the back button is clicked', () => {
    useChannels().addChannel(makeChannel({ capabilities: Capability.Forum, type: 'Forum' }));
    useChannels().selectChannel('ch-1');
    useModals().selectForumPost(makePost());
    const { getByTestId } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    fireEvent.click(getByTestId('forum-back-button'));
    expect(useModals().selectedForumPost).toBeNull();
  });

  it('replaces the compose bar with the edit textarea when a message is being edited', () => {
    useChannels().addChannel(makeChannel());
    useChannels().selectChannel('ch-1');
    useMessages().startEditing('msg-1', 'old content');
    const { getByTestId, queryByTestId } = render(() => (
      <MessagesArea conversationId="conv-1" serverId="s-1" channelId="ch-1" />
    ));
    expect(getByTestId('message-edit-textarea')).toBeInTheDocument();
    expect(queryByTestId('mock-message-compose')).toBeNull();
  });
});
