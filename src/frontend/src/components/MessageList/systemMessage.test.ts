import { describe, it, expect } from 'vitest';
import type { Message } from '../../types/message';
import { isSystemMessage, systemMessageText } from './systemMessage';

function msg(type: string, metadata?: unknown): Message {
  return {
    id: 'm-1',
    conversationId: 'c-1',
    authorId: '',
    type,
    content: '',
    metadata: metadata === undefined
      ? undefined
      : (JSON.stringify(metadata) as unknown as Record<string, unknown>),
    isPinned: false,
    createdAt: '2026-01-01T00:00:00Z',
  };
}

describe('isSystemMessage', () => {
  it('recognises the types the backend posts into a system channel', () => {
    expect(isSystemMessage(msg('MemberJoin'))).toBe(true);
    expect(isSystemMessage(msg('MemberBan'))).toBe(true);
  });

  it('leaves ordinary and poll messages alone', () => {
    expect(isSystemMessage(msg('Default'))).toBe(false);
    expect(isSystemMessage(msg('PollCreated'))).toBe(false);
  });
});

describe('systemMessageText', () => {
  it('names the member who joined', () => {
    expect(systemMessageText(msg('MemberJoin', { Username: 'alice' })))
      .toBe('alice joined the server');
  });

  it('prefers a display name over a username', () => {
    expect(systemMessageText(msg('MemberJoin', { Username: 'alice', DisplayName: 'Alice A' })))
      .toBe('Alice A joined the server');
  });

  it('reads camelCase metadata too', () => {
    expect(systemMessageText(msg('MemberLeave', { username: 'bob' })))
      .toBe('bob left the server');
  });

  it('includes the moderation reason when there is one', () => {
    expect(systemMessageText(msg('MemberBan', { Username: 'carol', Reason: 'spam' })))
      .toBe('carol was banned from the server (spam)');
  });

  it('omits the parenthetical when no reason was given', () => {
    expect(systemMessageText(msg('MemberKick', { Username: 'dave' })))
      .toBe('dave was kicked from the server');
  });

  it('falls back to a neutral subject when metadata is missing', () => {
    expect(systemMessageText(msg('MemberJoin'))).toBe('Someone joined the server');
  });

  it('survives metadata that is not valid JSON', () => {
    const broken = msg('MemberJoin');
    broken.metadata = '{not json' as unknown as Record<string, unknown>;

    expect(systemMessageText(broken)).toBe('Someone joined the server');
  });

  it('never returns an empty string, whatever the type', () => {
    for (const type of [
      'MemberJoin', 'MemberLeave', 'MemberKick', 'MemberBan', 'ChannelNameChange',
      'ChannelTopicChange', 'PinnedMessage', 'RoleCreated', 'RoleDeleted',
      'ServerUpdated', 'InviteCreated', 'ThreadCreated', 'ThreadArchived',
      'ScheduledEventCreated', 'CallStarted', 'SomeFutureBackendType',
    ]) {
      expect(systemMessageText(msg(type)).length).toBeGreaterThan(0);
    }
  });
});
