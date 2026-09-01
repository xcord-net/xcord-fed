import { describe, it, expect } from 'vitest';
import type { Message } from '../../types/message';
import { shouldGroupWithPrevious } from './helpers';

function msg(overrides: Partial<Message> = {}): Message {
  return {
    id: 'm-1',
    conversationId: 'c-1',
    authorId: 'u-1',
    type: 'Default',
    content: 'hello',
    isPinned: false,
    createdAt: '2026-01-01T12:00:00Z',
    ...overrides,
  };
}

describe('shouldGroupWithPrevious', () => {
  it('never groups the first message', () => {
    const list = [msg()];

    expect(shouldGroupWithPrevious(list, list[0], 0)).toBe(false);
  });

  it('groups consecutive messages from the same author within five minutes', () => {
    const list = [
      msg({ id: 'a', createdAt: '2026-01-01T12:00:00Z' }),
      msg({ id: 'b', createdAt: '2026-01-01T12:04:00Z' }),
    ];

    expect(shouldGroupWithPrevious(list, list[1], 1)).toBe(true);
  });

  it('does not group across authors', () => {
    const list = [
      msg({ id: 'a', authorId: 'u-1' }),
      msg({ id: 'b', authorId: 'u-2' }),
    ];

    expect(shouldGroupWithPrevious(list, list[1], 1)).toBe(false);
  });

  it('does not group once more than five minutes have passed', () => {
    const list = [
      msg({ id: 'a', createdAt: '2026-01-01T12:00:00Z' }),
      msg({ id: 'b', createdAt: '2026-01-01T12:06:00Z' }),
    ];

    expect(shouldGroupWithPrevious(list, list[1], 1)).toBe(false);
  });

  it('never groups a reply, so its quote line stays visible', () => {
    const list = [
      msg({ id: 'a', createdAt: '2026-01-01T12:00:00Z' }),
      msg({ id: 'b', createdAt: '2026-01-01T12:01:00Z', replyToId: 'a' }),
    ];

    expect(shouldGroupWithPrevious(list, list[1], 1)).toBe(false);
  });
});
