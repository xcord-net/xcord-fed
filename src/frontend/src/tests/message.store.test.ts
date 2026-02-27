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
});
