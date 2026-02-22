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

    it('should remove entry when count is 0', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.updateUnread('conv-1', 0, 'msg-100');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
      expect(unread.unreadMap.has('conv-1')).toBe(false);
    });
  });

  describe('markRead', () => {
    it('should remove unread entry for conversation', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-100');
      unread.markRead('conv-1');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
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
