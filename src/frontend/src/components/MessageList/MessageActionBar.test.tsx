import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import type { Message } from '../../types/message';

// EmojiPicker reaches into stores/network when mounted; stub it out so this
// test stays focused on the action-bar rendering logic.
vi.mock('../EmojiPicker', () => ({
  default: (p: { onSelect: (emoji: string) => void; onClose: () => void }) => (
    <div data-testid="mock-emoji-picker">
      <button data-testid="mock-emoji-pick" onClick={() => p.onSelect(':smile:')}>pick</button>
      <button data-testid="mock-emoji-close" onClick={() => p.onClose()}>close</button>
    </div>
  ),
}));

import MessageActionBar from './MessageActionBar';

function makeMessage(overrides: Partial<Message> = {}): Message {
  return {
    id: 'm-1',
    conversationId: 'c-1',
    authorId: 'u-1',
    authorUsername: 'alice',
    type: 'Default',
    content: 'hello',
    isPinned: false,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

function baseProps(overrides: Partial<Parameters<typeof MessageActionBar>[0]> = {}) {
  return {
    message: makeMessage(),
    isReactionPickerOpen: false,
    canEdit: false,
    canDelete: false,
    showThreadButton: false,
    onReply: vi.fn(),
    onEdit: vi.fn(),
    onDelete: vi.fn(),
    onToggleReactionPicker: vi.fn(),
    onCloseReactionPicker: vi.fn(),
    onSelectReaction: vi.fn(),
    onTogglePin: vi.fn(),
    onStartThread: vi.fn(),
    ...overrides,
  };
}

describe('MessageActionBar', () => {
  it('renders without crashing given minimal valid props', () => {
    const { getByRole } = render(() => <MessageActionBar {...baseProps()} />);
    expect(getByRole('toolbar', { name: 'Message actions' })).toBeInTheDocument();
  });

  it('always renders reply, react, and pin buttons', () => {
    const { getByTestId } = render(() => <MessageActionBar {...baseProps()} />);
    expect(getByTestId('message-action-reply')).toBeInTheDocument();
    expect(getByTestId('message-action-react')).toBeInTheDocument();
    expect(getByTestId('message-action-pin')).toBeInTheDocument();
  });

  it('fires onReply when the reply button is clicked', () => {
    const onReply = vi.fn();
    const { getByTestId } = render(() => <MessageActionBar {...baseProps({ onReply })} />);
    fireEvent.click(getByTestId('message-action-reply'));
    expect(onReply).toHaveBeenCalledTimes(1);
  });

  it('hides edit/delete/thread buttons by default', () => {
    const { queryByTestId } = render(() => <MessageActionBar {...baseProps()} />);
    expect(queryByTestId('message-action-edit')).toBeNull();
    expect(queryByTestId('message-action-delete')).toBeNull();
    expect(queryByTestId('message-action-thread')).toBeNull();
  });

  it('shows edit and delete when canEdit/canDelete are true and fires callbacks', () => {
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    const { getByTestId } = render(() => (
      <MessageActionBar {...baseProps({ canEdit: true, canDelete: true, onEdit, onDelete })} />
    ));
    fireEvent.click(getByTestId('message-action-edit'));
    fireEvent.click(getByTestId('message-action-delete'));
    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(onDelete).toHaveBeenCalledTimes(1);
  });

  it('shows thread button when showThreadButton and fires onStartThread', () => {
    const onStartThread = vi.fn();
    const { getByTestId } = render(() => (
      <MessageActionBar {...baseProps({ showThreadButton: true, onStartThread })} />
    ));
    fireEvent.click(getByTestId('message-action-thread'));
    expect(onStartThread).toHaveBeenCalledTimes(1);
  });

  it('toggles the reaction picker via onToggleReactionPicker', () => {
    const onToggleReactionPicker = vi.fn();
    const { getByTestId } = render(() => (
      <MessageActionBar {...baseProps({ onToggleReactionPicker })} />
    ));
    fireEvent.click(getByTestId('message-action-react'));
    expect(onToggleReactionPicker).toHaveBeenCalledTimes(1);
  });

  it('renders the emoji picker when open and forwards selection/close', () => {
    const onSelectReaction = vi.fn();
    const onCloseReactionPicker = vi.fn();
    const { getByTestId } = render(() => (
      <MessageActionBar
        {...baseProps({
          isReactionPickerOpen: true,
          onSelectReaction,
          onCloseReactionPicker,
        })}
      />
    ));
    expect(getByTestId('mock-emoji-picker')).toBeInTheDocument();
    fireEvent.click(getByTestId('mock-emoji-pick'));
    expect(onSelectReaction).toHaveBeenCalledWith(':smile:');
    fireEvent.click(getByTestId('mock-emoji-close'));
    expect(onCloseReactionPicker).toHaveBeenCalled();
  });
});
