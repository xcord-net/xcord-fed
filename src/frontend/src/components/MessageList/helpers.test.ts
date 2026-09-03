import { describe, it, expect } from 'vitest';
import type { Message } from '../../types/message';
import { shouldGroupWithPrevious, startsNewDay, formatDayLabel } from './helpers';

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

describe('startsNewDay', () => {
  const msg = (id: string, iso: string): Message =>
    ({ id, authorId: 'a', createdAt: iso, content: '' }) as Message;

  it('always starts a day at the top of the loaded page', () => {
    const messages = [msg('1', '2026-03-04T10:00:00Z')];
    expect(startsNewDay(messages, messages[0], 0)).toBe(true);
  });

  it('is false for a later message on the same day', () => {
    const messages = [
      msg('1', '2026-03-04T10:00:00Z'),
      msg('2', '2026-03-04T18:00:00Z'),
    ];
    expect(startsNewDay(messages, messages[1], 1)).toBe(false);
  });

  it('is true across a midnight boundary even minutes apart', () => {
    const before = new Date('2026-03-04T23:50:00');
    const after = new Date('2026-03-05T00:10:00');
    const messages = [
      msg('1', before.toISOString()),
      msg('2', after.toISOString()),
    ];
    expect(startsNewDay(messages, messages[1], 1)).toBe(true);
  });
});

describe('formatDayLabel', () => {
  const now = new Date('2026-03-04T12:00:00');

  it('says Today for today', () => {
    expect(formatDayLabel(new Date('2026-03-04T09:00:00').toISOString(), now)).toBe('Today');
  });

  it('says Yesterday for yesterday', () => {
    expect(formatDayLabel(new Date('2026-03-03T09:00:00').toISOString(), now)).toBe('Yesterday');
  });

  it('gives a real date for anything older', () => {
    const label = formatDayLabel(new Date('2026-02-20T09:00:00').toISOString(), now);
    expect(label).not.toBe('Today');
    expect(label).not.toBe('Yesterday');
    expect(label).toContain('February');
  });

  it('includes the year once it is a different one', () => {
    expect(formatDayLabel(new Date('2025-11-02T09:00:00').toISOString(), now)).toContain('2025');
  });

  it('returns an empty label rather than "Invalid Date" for junk', () => {
    expect(formatDayLabel('not-a-date', now)).toBe('');
  });
});
