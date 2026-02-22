import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { useTyping } from '../stores/typing.store';

describe('typing.store', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    const typing = useTyping();
    typing.clearTyping();
    vi.clearAllMocks();
  });

  afterEach(() => {
    const typing = useTyping();
    typing.clearTyping();
    vi.useRealTimers();
  });

  describe('startTyping', () => {
    it('should add user to typing set for conversation', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');

      expect(typing.getTypingUsers('conv-1')).toContain('user-1');
    });

    it('should support multiple users typing in same conversation', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.startTyping('conv-1', 'user-2');

      const users = typing.getTypingUsers('conv-1');
      expect(users).toContain('user-1');
      expect(users).toContain('user-2');
      expect(users).toHaveLength(2);
    });

    it('should track typing across different conversations', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.startTyping('conv-2', 'user-2');

      expect(typing.getTypingUsers('conv-1')).toEqual(['user-1']);
      expect(typing.getTypingUsers('conv-2')).toEqual(['user-2']);
    });

    it('should auto-remove user after 8 seconds', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      expect(typing.getTypingUsers('conv-1')).toContain('user-1');

      // Advance past the 8 second timeout
      vi.advanceTimersByTime(8000);

      expect(typing.getTypingUsers('conv-1')).not.toContain('user-1');
    });

    it('should reset timeout when same user types again', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');

      // Advance 6 seconds
      vi.advanceTimersByTime(6000);
      expect(typing.getTypingUsers('conv-1')).toContain('user-1');

      // Type again - resets the 8s timer
      typing.startTyping('conv-1', 'user-1');

      // Advance another 6 seconds (total 12s from start, but only 6s from reset)
      vi.advanceTimersByTime(6000);
      expect(typing.getTypingUsers('conv-1')).toContain('user-1');

      // Advance 2 more seconds (8s from reset)
      vi.advanceTimersByTime(2000);
      expect(typing.getTypingUsers('conv-1')).not.toContain('user-1');
    });
  });

  describe('stopTyping', () => {
    it('should remove user from typing set', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.stopTyping('conv-1', 'user-1');

      expect(typing.getTypingUsers('conv-1')).not.toContain('user-1');
    });

    it('should clean up conversation entry when last user stops', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.stopTyping('conv-1', 'user-1');

      expect(typing.getTypingUsers('conv-1')).toEqual([]);
    });

    it('should not throw when stopping typing for non-existent user', () => {
      const typing = useTyping();

      expect(() => typing.stopTyping('conv-1', 'unknown')).not.toThrow();
    });
  });

  describe('getTypingUsers', () => {
    it('should return empty array for unknown conversation', () => {
      const typing = useTyping();
      expect(typing.getTypingUsers('unknown')).toEqual([]);
    });
  });

  describe('clearTyping', () => {
    it('should clear specific conversation', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.startTyping('conv-2', 'user-2');

      typing.clearTyping('conv-1');

      expect(typing.getTypingUsers('conv-1')).toEqual([]);
      expect(typing.getTypingUsers('conv-2')).toContain('user-2');
    });

    it('should clear all conversations when called without argument', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'user-1');
      typing.startTyping('conv-2', 'user-2');

      typing.clearTyping();

      expect(typing.getTypingUsers('conv-1')).toEqual([]);
      expect(typing.getTypingUsers('conv-2')).toEqual([]);
    });
  });
});
