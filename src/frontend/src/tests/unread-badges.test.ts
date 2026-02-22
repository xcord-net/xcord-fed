import { describe, it, expect, beforeEach } from 'vitest';
import { useUnread } from '../stores/unread.store';

// Tests focused on the badge display contract: counts that drive
// the unread dot/badge rendering in ChannelSidebar.

describe('unread badges', () => {
  beforeEach(() => {
    useUnread().clearUnreads();
  });

  describe('getUnreadCount', () => {
    it('returns 0 for an unknown conversation', () => {
      const unread = useUnread();
      expect(unread.getUnreadCount('conv-unknown')).toBe(0);
    });

    it('returns the correct count after updateUnread', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 7, 'msg-7');

      expect(unread.getUnreadCount('conv-1')).toBe(7);
    });

    it('returns 0 after markRead clears the conversation', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 3, 'msg-3');
      unread.markRead('conv-1');

      expect(unread.getUnreadCount('conv-1')).toBe(0);
    });

    it('does not affect sibling conversations when one is marked read', () => {
      const unread = useUnread();

      unread.updateUnread('conv-a', 5, 'msg-5');
      unread.updateUnread('conv-b', 2, 'msg-2');

      unread.markRead('conv-a');

      expect(unread.getUnreadCount('conv-a')).toBe(0);
      expect(unread.getUnreadCount('conv-b')).toBe(2);
    });
  });

  describe('badge visibility threshold', () => {
    it('has unread when count is greater than 0', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 1, 'msg-1');

      expect(unread.getUnreadCount('conv-1') > 0).toBe(true);
    });

    it('has no unread when count is 0', () => {
      const unread = useUnread();

      expect(unread.getUnreadCount('conv-never-set') > 0).toBe(false);
    });

    it('badge disappears after count drops to 0 via updateUnread', () => {
      const unread = useUnread();

      unread.updateUnread('conv-1', 5, 'msg-5');
      unread.updateUnread('conv-1', 0, 'msg-5');

      expect(unread.getUnreadCount('conv-1') > 0).toBe(false);
    });
  });

  describe('high unread counts', () => {
    it('tracks counts above 99 without truncating in the store', () => {
      const unread = useUnread();

      unread.updateUnread('conv-busy', 150, 'msg-150');

      // The store itself holds the full count; capping at "99+" is a display concern.
      expect(unread.getUnreadCount('conv-busy')).toBe(150);
    });
  });
});
