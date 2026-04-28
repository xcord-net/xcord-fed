import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import type { Message } from '../../types/message';

// Stub child components so tests focus on MessageRow composition.
vi.mock('../MarkdownRenderer', () => ({
  default: (p: { content: string }) => (
    <span data-testid="mock-markdown">{p.content}</span>
  ),
}));
vi.mock('./MessageActionBar', () => ({
  default: (p: { canEdit: boolean; canDelete: boolean; showThreadButton: boolean }) => (
    <div
      data-testid="mock-action-bar"
      data-can-edit={p.canEdit ? '1' : '0'}
      data-can-delete={p.canDelete ? '1' : '0'}
      data-show-thread={p.showThreadButton ? '1' : '0'}
    />
  ),
}));
vi.mock('./MessageContent', () => ({
  default: (p: { message: Message; isAuthor: boolean }) => (
    <div
      data-testid="mock-message-content"
      data-message-id={p.message.id}
      data-is-author={p.isAuthor ? '1' : '0'}
    />
  ),
}));
vi.mock('./ThreadCreateForm', () => ({
  default: (p: { value: string; onSubmit: () => void; onCancel: () => void; onInput: (v: string) => void }) => (
    <div data-testid="mock-thread-create-form">
      <span data-testid="mock-thread-name">{p.value}</span>
      <button data-testid="mock-thread-submit" onClick={() => p.onSubmit()}>submit</button>
      <button data-testid="mock-thread-cancel" onClick={() => p.onCancel()}>cancel</button>
      <button data-testid="mock-thread-input" onClick={() => p.onInput('new')}>input</button>
    </div>
  ),
}));

import MessageRow from './MessageRow';

function makeMessage(overrides: Partial<Message> = {}): Message {
  return {
    id: 'm-1',
    conversationId: 'c-1',
    authorId: 'u-1',
    authorUsername: 'alice',
    type: 'Default',
    content: 'hello world',
    isPinned: false,
    createdAt: '2026-01-01T12:34:00Z',
    ...overrides,
  };
}

function baseProps(overrides: Partial<Parameters<typeof MessageRow>[0]> = {}) {
  return {
    message: makeMessage(),
    conversationId: 'c-1',
    grouped: false,
    isAuthor: false,
    canDelete: false,
    isReactionPickerOpen: false,
    isThreadCreateOpen: false,
    threadNameValue: '',
    onEdit: vi.fn(),
    onDelete: vi.fn(),
    onToggleReactionPicker: vi.fn(),
    onCloseReactionPicker: vi.fn(),
    onSelectReaction: vi.fn(),
    onTogglePin: vi.fn(),
    onStartThread: vi.fn(),
    onThreadNameInput: vi.fn(),
    onThreadCreateSubmit: vi.fn(),
    onThreadCreateCancel: vi.fn(),
    ...overrides,
  };
}

describe('MessageRow', () => {
  it('renders without crashing and exposes a stable testid for the row', () => {
    const { getByTestId } = render(() => <MessageRow {...baseProps()} />);
    expect(getByTestId('message-row-m-1')).toBeInTheDocument();
  });

  it('renders the author header and content in the full (non-grouped) layout', () => {
    const { getByText, getByTestId } = render(() => <MessageRow {...baseProps()} />);
    expect(getByText('alice')).toBeInTheDocument();
    expect(getByTestId('mock-markdown').textContent).toBe('hello world');
    expect(getByTestId('mock-message-content')).toBeInTheDocument();
  });

  it('omits the author header in the grouped layout', () => {
    const { queryByText, getByTestId } = render(() => (
      <MessageRow {...baseProps({ grouped: true })} />
    ));
    expect(queryByText('alice')).toBeNull();
    // Grouped layout still renders content/markdown.
    expect(getByTestId('mock-markdown').textContent).toBe('hello world');
  });

  it('renders the reply indicator when replyToId is set', () => {
    const message = makeMessage({ replyToId: 'm-prev' });
    const { getByText } = render(() => (
      <MessageRow {...baseProps({ message })} />
    ));
    expect(getByText('Replying to a message')).toBeInTheDocument();
  });

  it('shows the thread button only when channelId is provided', () => {
    const first = render(() => <MessageRow {...baseProps()} />);
    expect(first.getByTestId('mock-action-bar').getAttribute('data-show-thread')).toBe('0');

    const second = render(() => (
      <MessageRow {...baseProps({ channelId: 'ch-1' })} />
    ));
    expect(second.getByTestId('mock-action-bar').getAttribute('data-show-thread')).toBe('1');
  });

  it('forwards canEdit (isAuthor) and canDelete to the action bar', () => {
    const { getByTestId } = render(() => (
      <MessageRow {...baseProps({ isAuthor: true, canDelete: true })} />
    ));
    const bar = getByTestId('mock-action-bar');
    expect(bar.getAttribute('data-can-edit')).toBe('1');
    expect(bar.getAttribute('data-can-delete')).toBe('1');
  });

  it('renders ThreadCreateForm when isThreadCreateOpen and forwards submit/cancel', () => {
    const onThreadCreateSubmit = vi.fn();
    const onThreadCreateCancel = vi.fn();
    const open = render(() => (
      <MessageRow
        {...baseProps({
          isThreadCreateOpen: true,
          threadNameValue: 'release-notes',
          onThreadCreateSubmit,
          onThreadCreateCancel,
        })}
      />
    ));
    expect(open.getByTestId('mock-thread-create-form')).toBeInTheDocument();
    expect(open.getByTestId('mock-thread-name').textContent).toBe('release-notes');
    fireEvent.click(open.getByTestId('mock-thread-submit'));
    expect(onThreadCreateSubmit).toHaveBeenCalledTimes(1);
    fireEvent.click(open.getByTestId('mock-thread-cancel'));
    expect(onThreadCreateCancel).toHaveBeenCalledTimes(1);

    // And not rendered when closed.
    const closed = render(() => <MessageRow {...baseProps({ isThreadCreateOpen: false })} />);
    expect(closed.queryByTestId('mock-thread-create-form')).toBeNull();
  });
});
