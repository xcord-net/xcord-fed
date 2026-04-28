import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import TypingIndicator, { formatTypingText } from './TypingIndicator';
import { useTyping } from '../stores/typing.store';

describe('formatTypingText', () => {
  it('returns empty string for no users', () => {
    expect(formatTypingText([])).toBe('');
  });

  it('formats one user', () => {
    expect(formatTypingText(['alice'])).toBe('alice is typing...');
  });

  it('formats two users', () => {
    expect(formatTypingText(['alice', 'bob'])).toBe('alice and bob are typing...');
  });

  it('formats three or more users with other count', () => {
    expect(formatTypingText(['a', 'b', 'c'])).toBe('a, b, and 1 other are typing...');
    expect(formatTypingText(['a', 'b', 'c', 'd'])).toBe('a, b, and 2 others are typing...');
  });
});

describe('TypingIndicator', () => {
  beforeEach(() => {
    useTyping().reset();
  });

  it('renders nothing when no users are typing', () => {
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    expect(container.querySelector('#typing-indicator')).toBeNull();
  });

  it('renders typing text when a user starts typing', () => {
    useTyping().startTyping('conv-1', 'alice');
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    const indicator = container.querySelector('#typing-indicator');
    expect(indicator).not.toBeNull();
    expect(indicator!.textContent).toContain('alice is typing...');
  });

  it('isolates typing per conversation', () => {
    useTyping().startTyping('conv-other', 'alice');
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    expect(container.querySelector('#typing-indicator')).toBeNull();
  });

  it('updates when typing state changes', () => {
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    expect(container.querySelector('#typing-indicator')).toBeNull();
    useTyping().startTyping('conv-1', 'alice');
    expect(container.querySelector('#typing-indicator')!.textContent).toContain('alice is typing...');
  });
});
