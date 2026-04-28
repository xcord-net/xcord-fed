import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import type { Message, MessageEmbed, MessageReaction, MessageAttachment } from '../../types/message';

// Stub heavy children so this test focuses on MessageContent's composition logic.
vi.mock('./AttachmentList', () => ({
  default: (p: { attachments: MessageAttachment[] }) => (
    <div data-testid="mock-attachment-list" data-count={p.attachments.length} />
  ),
}));
vi.mock('./PollContainer', () => ({
  default: (p: { pollId: string }) => (
    <div data-testid="mock-poll-container" data-poll-id={p.pollId} />
  ),
}));
vi.mock('../EmbedDisplay', () => ({
  default: (p: { embed: MessageEmbed }) => (
    <div data-testid="mock-embed">{p.embed.title ?? p.embed.url ?? ''}</div>
  ),
}));
vi.mock('../ReactionDisplay', () => ({
  default: (p: { reactions: MessageReaction[] }) => (
    <div data-testid="mock-reactions" data-count={p.reactions.length} />
  ),
}));

// Drive thread-indicator branches deterministically.
let threadFixtures: Array<{ id: string; parentMessageId: string; name: string; messageCount: number }> = [];
const setActiveThread = vi.fn();
vi.mock('../../stores/thread.store', () => ({
  useThreads: () => ({
    get threads() { return threadFixtures; },
    setActiveThread,
  }),
}));

import MessageContent from './MessageContent';

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

describe('MessageContent', () => {
  beforeEach(() => {
    threadFixtures = [];
    setActiveThread.mockReset();
  });

  it('renders without crashing for a minimal message', () => {
    const { container } = render(() => (
      <MessageContent message={makeMessage()} conversationId="c-1" isAuthor={false} />
    ));
    // No badges, attachments, embeds, polls, reactions, or thread indicators.
    expect(container.querySelector('[data-testid]')).toBeNull();
  });

  it('renders the edited indicator only when editedAt is set', () => {
    const first = render(() => (
      <MessageContent message={makeMessage()} conversationId="c-1" isAuthor={false} />
    ));
    expect(first.queryByTestId('message-edited-indicator')).toBeNull();

    const second = render(() => (
      <MessageContent
        message={makeMessage({ editedAt: '2026-01-02T00:00:00Z' })}
        conversationId="c-1"
        isAuthor={false}
      />
    ));
    expect(second.getByTestId('message-edited-indicator')).toBeInTheDocument();
  });

  it('renders AttachmentList when attachments are present', () => {
    const message = makeMessage({
      attachments: [{
        id: 'a-1',
        fileName: 'pic.png',
        contentType: 'image/png',
        fileSize: 100,
        downloadUrl: 'https://x/a-1',
      }],
    });
    const { getByTestId } = render(() => (
      <MessageContent message={message} conversationId="c-1" isAuthor={false} />
    ));
    expect(getByTestId('mock-attachment-list').getAttribute('data-count')).toBe('1');
  });

  it('renders PollContainer for PollCreated messages with a pollId', () => {
    const message = makeMessage({ type: 'PollCreated', pollId: 'p-42' });
    const { getByTestId } = render(() => (
      <MessageContent message={message} conversationId="c-1" isAuthor={true} />
    ));
    expect(getByTestId('mock-poll-container').getAttribute('data-poll-id')).toBe('p-42');
  });

  it('renders one EmbedDisplay per embed', () => {
    const message = makeMessage({
      embeds: [
        { url: 'https://a', title: 'A' },
        { url: 'https://b', title: 'B' },
      ],
    });
    const { getAllByTestId } = render(() => (
      <MessageContent message={message} conversationId="c-1" isAuthor={false} />
    ));
    expect(getAllByTestId('mock-embed')).toHaveLength(2);
  });

  it('renders the thread indicator and triggers setActiveThread when clicked', () => {
    threadFixtures = [
      { id: 't-1', parentMessageId: 'm-1', name: 'design-review', messageCount: 3 },
    ];
    const { getByTestId } = render(() => (
      <MessageContent message={makeMessage()} conversationId="c-1" isAuthor={false} />
    ));
    const indicator = getByTestId('message-thread-indicator');
    expect(indicator.textContent).toContain('design-review');
    expect(indicator.textContent).toContain('3 replies');
    fireEvent.click(indicator);
    expect(setActiveThread).toHaveBeenCalledWith('t-1');
  });
});
