import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useMessages } from '../stores/message.store';
import type { Message } from '../types/message';

describe('message.store', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset singleton store state
    const messages = useMessages();
    messages.clearMessages();
  });

  describe('sendMessage', () => {
    it('should add optimistic message and replace with server response', async () => {
      // Arrange
      const conversationId = '123';
      const content = 'Hello world';
      const serverMessage = {
        id: 'real-id-456',
        conversationId,
        authorId: 'user-1',
        type: 'Default',
        content,
        isPinned: false,
        createdAt: new Date().toISOString(),
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => serverMessage,
      });

      const messages = useMessages();

      // Act
      const sendPromise = messages.sendMessage(conversationId, content);

      // Assert - optimistic message should be added immediately
      expect(messages.messages.length).toBe(1);
      expect(messages.messages[0].content).toBe(content);
      expect(messages.messages[0].id).toContain('pending-');

      // Wait for server response
      await sendPromise;

      // Assert - optimistic message should be replaced
      expect(messages.messages.length).toBe(1);
      expect(messages.messages[0].id).toBe('real-id-456');
      expect(messages.messages[0].content).toBe(content);
    });

    it('should rollback optimistic message on error', async () => {
      // Arrange
      const conversationId = '123';
      const content = 'Hello world';

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Rate limited' }),
      });

      const messages = useMessages();

      // Act
      const sendPromise = messages.sendMessage(conversationId, content);

      // Assert - optimistic message added
      expect(messages.messages.length).toBe(1);

      // Wait for error
      await expect(sendPromise).rejects.toThrow();

      // Assert - optimistic message removed
      expect(messages.messages.length).toBe(0);
    });

    it('should handle concurrent optimistic sends', async () => {
      // Arrange
      const conversationId = '123';
      const message1 = { id: 'msg-1', content: 'First', conversationId, authorId: 'user-1', type: 'Default', isPinned: false, createdAt: new Date().toISOString() };
      const message2 = { id: 'msg-2', content: 'Second', conversationId, authorId: 'user-1', type: 'Default', isPinned: false, createdAt: new Date().toISOString() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({ ok: true, json: async () => message1 })
        .mockResolvedValueOnce({ ok: true, json: async () => message2 });

      const messages = useMessages();

      // Act
      const promise1 = messages.sendMessage(conversationId, 'First');
      const promise2 = messages.sendMessage(conversationId, 'Second');

      // Assert - both optimistic messages present
      expect(messages.messages.length).toBe(2);

      await Promise.all([promise1, promise2]);

      // Assert - both real messages present
      expect(messages.messages.length).toBe(2);
      const ids = messages.messages.map((m: Message) => m.id).sort();
      expect(ids).toEqual(['msg-1', 'msg-2']);
    });
  });

  describe('deleteMessage', () => {
    it('should remove message from store', async () => {
      // Arrange
      const conversationId = '123';
      const messageId = 'msg-1';

      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      const messages = useMessages();
      messages.addMessage({
        id: messageId,
        conversationId,
        authorId: 'user-1',
        type: 'Default',
        content: 'Test message',
        isPinned: false,
        createdAt: new Date().toISOString(),
      });

      // Act
      await messages.deleteMessage(conversationId, messageId);

      // Assert
      expect(messages.messages.length).toBe(0);
    });
  });

  describe('editMessage', () => {
    it('should update message content in store', async () => {
      // Arrange
      const conversationId = '123';
      const messageId = 'msg-1';
      const updatedMessage = {
        id: messageId,
        conversationId,
        authorId: 'user-1',
        type: 'Default',
        content: 'Updated content',
        isPinned: false,
        createdAt: new Date().toISOString(),
        editedAt: new Date().toISOString(),
      };

      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => updatedMessage,
      });

      const messages = useMessages();
      messages.addMessage({
        id: messageId,
        conversationId,
        authorId: 'user-1',
        type: 'Default',
        content: 'Original content',
        isPinned: false,
        createdAt: new Date().toISOString(),
      });

      // Act
      await messages.editMessage(conversationId, messageId, 'Updated content');

      // Assert
      expect(messages.messages[0].content).toBe('Updated content');
      expect(messages.messages[0].editedAt).toEqual(expect.any(String));
      expect(messages.messages[0].editedAt!.length).toBeGreaterThan(0);
    });
  });

  describe('reply references', () => {
    function reply(overrides: Partial<Message> = {}): Message {
      return {
        id: 'msg-reply',
        conversationId: '123',
        authorId: 'user-1',
        type: 'Default',
        content: 'a reply',
        replyToId: 'msg-parent',
        replyTo: {
          id: 'msg-parent',
          authorUsername: 'bob',
          authorGroupColor: '#FF5733',
          preview: 'the original',
          isDeleted: false,
        },
        isPinned: false,
        createdAt: new Date().toISOString(),
        ...overrides,
      };
    }

    it('normalizes the reply-target id to a string', () => {
      const messages = useMessages();
      // SignalR can deliver snowflakes as raw numbers before the converter runs,
      // and a number id would never match the string ids lane building compares.
      messages.addMessage(reply({
        replyToId: 9007199254740993 as unknown as string,
        replyTo: {
          id: 9007199254740993 as unknown as string,
          authorUsername: 'bob',
          preview: 'the original',
          isDeleted: false,
        },
      }));

      expect(typeof messages.messages[0].replyTo!.id).toBe('string');
      expect(messages.messages[0].replyTo!.id).toBe(messages.messages[0].replyToId);
    });

    it('keeps the reply reference when an update payload omits it', () => {
      const messages = useMessages();
      messages.addMessage(reply());

      // An edit broadcast that lost its replyTo must not dissolve the lane.
      messages.updateMessage(reply({ replyTo: undefined, content: 'edited reply' }));

      expect(messages.messages[0].content).toBe('edited reply');
      expect(messages.messages[0].replyTo?.authorUsername).toBe('bob');
    });

    it('keeps attachments and reactions when a partial update omits them', () => {
      // Pin and unpin broadcast a message without its attachments or reactions.
      const messages = useMessages();
      messages.addMessage(reply({
        attachments: [{
          id: 'a-1', fileName: 'plan.pdf', contentType: 'application/pdf',
          fileSize: 10, downloadUrl: 'https://example.test/plan.pdf',
        }],
        reactions: [{ emoji: '👍', count: 1, userIds: ['user-2'] }],
      }));

      messages.updateMessage(reply({
        attachments: undefined,
        reactions: undefined,
        isPinned: true,
      }));

      expect(messages.messages[0].isPinned).toBe(true);
      expect(messages.messages[0].attachments).toHaveLength(1);
      expect(messages.messages[0].reactions).toHaveLength(1);
    });

    it('still lets an explicit empty collection clear the previous one', () => {
      const messages = useMessages();
      messages.addMessage(reply({
        reactions: [{ emoji: '👍', count: 1, userIds: ['user-2'] }],
      }));

      messages.updateMessage(reply({ reactions: [] }));

      expect(messages.messages[0].reactions).toHaveLength(0);
    });

    it('takes the incoming reply reference when the update payload carries one', () => {
      const messages = useMessages();
      messages.addMessage(reply());

      messages.updateMessage(reply({
        replyTo: { id: 'msg-parent', preview: '', isDeleted: true },
      }));

      expect(messages.messages[0].replyTo?.isDeleted).toBe(true);
    });
  });
});
