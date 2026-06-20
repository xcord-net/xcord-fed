import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import TypingIndicator, { formatTypingText, resolveTypingNames } from './TypingIndicator';
import { useTyping } from '../stores/typing.store';
import type { Member } from '../types/member';

const member = (userId: string, displayName: string, username = displayName): Member => ({
  userId,
  serverId: 'srv-1',
  username,
  displayName,
  groups: [],
  joinedAt: '2026-01-01T00:00:00Z',
});

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

describe('resolveTypingNames', () => {
  it('maps user ids to member display names', () => {
    const members = [member('59038321111773184', 'Alice'), member('72000000000000000', 'Bob')];
    expect(resolveTypingNames(['59038321111773184'], members)).toEqual(['Alice']);
  });

  it('never returns a raw user id for an unknown user', () => {
    const result = resolveTypingNames(['59038321111773184'], []);
    expect(result).toEqual(['Someone']);
    expect(result[0]).not.toContain('59038321111773184');
  });

  it('resolves a mix of known and unknown users', () => {
    const members = [member('1', 'Alice')];
    expect(resolveTypingNames(['1', '2'], members)).toEqual(['Alice', 'Someone']);
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

  it('renders a friendly name, never the raw user id, when a user types', () => {
    useTyping().startTyping('conv-1', '59038321111773184');
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    const indicator = container.querySelector('#typing-indicator');
    expect(indicator).not.toBeNull();
    expect(indicator!.textContent).toContain('Someone is typing...');
    expect(indicator!.textContent).not.toContain('59038321111773184');
  });

  it('isolates typing per conversation', () => {
    useTyping().startTyping('conv-other', 'alice');
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    expect(container.querySelector('#typing-indicator')).toBeNull();
  });

  it('updates when typing state changes', () => {
    const { container } = render(() => <TypingIndicator conversationId="conv-1" />);
    expect(container.querySelector('#typing-indicator')).toBeNull();
    useTyping().startTyping('conv-1', '59038321111773184');
    expect(container.querySelector('#typing-indicator')!.textContent).toContain('Someone is typing...');
  });
});
