import { describe, it, expect, beforeEach } from 'vitest';
import { useUnread } from '../stores/unread.store';

describe('unread.store', () => {
  beforeEach(() => {
    const unread = useUnread();
    unread.clearUnreads();
  });

  describe('updateUnread', () => {
    it('should set unread count for conversation', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');

      expect(unread.getUnreadCount('conv-1')).toBe(5);
    });

    it('should update existing unread info', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 3, 'msg-50');
      unread.updateUnread('conv-1', 7, 'msg-100');

      expect(unread.getUnreadCount('conv-1')).toBe(7);
    });

    it('keeps a zeroed entry rather than forgetting the conversation', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-1', 0, 'msg-100');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
      // Consumers fall back to a server-side aggregate for conversations this
      // store knows nothing about, so "read" has to be distinguishable from
      // "unknown" - dropping the entry made a read conversation reappear with
      // whatever the last aggregate said.
      expect(unread.isTracked('conv-1')).toBe(true);
    });

    it('does not claim to track a conversation it has never seen', () => {
      const unread = useUnread();

      expect(unread.isTracked('never-heard-of-it')).toBe(false);
    });
  });

  describe('markRead', () => {
    it('zeroes the count and keeps tracking the conversation', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.markRead('conv-1');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
      expect(unread.isTracked('conv-1')).toBe(true);
    });

    it('should not affect other conversations', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-2', 3, 'msg-200');

      unread.markRead('conv-1');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
      expect(unread.getUnreadCount('conv-2')).toBe(3);
    });
  });

  describe('getUnreadCount', () => {
    it('should return 0 for unknown conversation', () => {
      const unread = useUnread();
      expect(unread.getUnreadCount('unknown')).toBe(0);
    });
  });

  describe('getTotalUnreadCount', () => {
    it('should sum all unread counts', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-2', 3, 'msg-200');
      unread.updateUnread('conv-3', 2, 'msg-300');

      expect(unread.getTotalUnreadCount()).toBe(10);
    });

    it('should return 0 when no unreads', () => {
      const unread = useUnread();
      expect(unread.getTotalUnreadCount()).toBe(0);
    });

    it('should update total after marking read', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-2', 3, 'msg-200');

      unread.markRead('conv-1');

      expect(unread.getTotalUnreadCount()).toBe(3);
    });
  });

  describe('clearUnreads', () => {
    it('should remove all unread data', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-2', 3, 'msg-200');

      unread.clearUnreads();

      expect(unread.getTotalUnreadCount()).toBe(0);
      expect(unread.unreadMap.size).toBe(0);
    });
  });
});
