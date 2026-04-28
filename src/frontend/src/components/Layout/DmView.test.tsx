import { describe, it, expect, vi } from 'vitest';
import { render } from '@solidjs/testing-library';

// Stub heavy children. The fallback (no DM selected) renders DmList + FriendList;
// the active branch renders MessageList + TypingIndicator + MessageCompose.
vi.mock('../DmList', () => ({
  default: () => <div data-testid="mock-dm-list">dm-list</div>,
}));
vi.mock('../FriendList', () => ({
  default: () => <div data-testid="mock-friend-list">friend-list</div>,
}));
vi.mock('../MessageCompose', () => ({
  default: (p: { conversationId: string; channelId?: string }) => (
    <div data-testid="mock-message-compose">compose:{p.conversationId}/{p.channelId ?? '-'}</div>
  ),
}));
vi.mock('../MessageList', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-message-list">list:{p.conversationId}</div>
  ),
}));
vi.mock('../TypingIndicator', () => ({
  default: (p: { conversationId: string }) => (
    <div data-testid="mock-typing">typing:{p.conversationId}</div>
  ),
}));

import DmView from './DmView';

describe('DmView', () => {
  it('renders the FriendList + DmList fallback when no DM is selected', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <DmView channelId={undefined} dmConversationId={undefined} />
    ));
    expect(getByTestId('mock-friend-list')).toBeInTheDocument();
    expect(getByTestId('mock-dm-list')).toBeInTheDocument();
    expect(queryByTestId('mock-message-list')).toBeNull();
    expect(queryByTestId('mock-message-compose')).toBeNull();
  });

  it('renders the conversation view (list + typing + compose) when both ids are provided', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <DmView channelId="dm-ch-1" dmConversationId="dm-conv-1" />
    ));
    expect(getByTestId('mock-message-list')).toHaveTextContent('list:dm-conv-1');
    expect(getByTestId('mock-typing')).toHaveTextContent('typing:dm-conv-1');
    expect(getByTestId('mock-message-compose')).toHaveTextContent('compose:dm-conv-1/dm-ch-1');
    expect(queryByTestId('mock-friend-list')).toBeNull();
    expect(queryByTestId('mock-dm-list')).toBeNull();
  });

  it('falls back when only dmConversationId is set (channelId missing)', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <DmView channelId={undefined} dmConversationId="dm-conv-1" />
    ));
    expect(getByTestId('mock-friend-list')).toBeInTheDocument();
    expect(queryByTestId('mock-message-list')).toBeNull();
  });

  it('falls back when only channelId is set (dmConversationId missing)', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <DmView channelId="dm-ch-1" dmConversationId={undefined} />
    ));
    expect(getByTestId('mock-friend-list')).toBeInTheDocument();
    expect(queryByTestId('mock-message-list')).toBeNull();
  });

  it('passes the dmConversationId through to MessageList and TypingIndicator', () => {
    const { getByTestId } = render(() => (
      <DmView channelId="dm-ch-9" dmConversationId="dm-conv-9" />
    ));
    expect(getByTestId('mock-message-list')).toHaveTextContent('dm-conv-9');
    expect(getByTestId('mock-typing')).toHaveTextContent('dm-conv-9');
  });
});
