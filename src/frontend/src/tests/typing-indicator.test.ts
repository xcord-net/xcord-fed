import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { formatTypingText } from '../components/TypingIndicator';
import { useTyping } from '../stores/typing.store';

describe('TypingIndicator', () => {
  describe('formatTypingText', () => {
    it('should return empty string when no users are typing', () => {
      expect(formatTypingText([])).toBe('');
    });

    it('should show singular form for 1 user typing', () => {
      expect(formatTypingText(['Alice'])).toBe('Alice is typing...');
    });

    it('should show both names for 2 users typing', () => {
      expect(formatTypingText(['Alice', 'Bob'])).toBe('Alice and Bob are typing...');
    });

    it('should show "and N others" for 3 users typing', () => {
      expect(formatTypingText(['Alice', 'Bob', 'Carol'])).toBe(
        'Alice, Bob, and 1 other are typing...'
      );
    });

    it('should show "and N others" for 4 users typing', () => {
      expect(formatTypingText(['Alice', 'Bob', 'Carol', 'Dave'])).toBe(
        'Alice, Bob, and 2 others are typing...'
      );
    });

    it('should use plural "others" for more than 1 extra user', () => {
      expect(formatTypingText(['Alice', 'Bob', 'Carol', 'Dave', 'Eve'])).toBe(
        'Alice, Bob, and 3 others are typing...'
      );
    });
  });

  describe('typing store filtering by conversationId', () => {
    beforeEach(() => {
      vi.useFakeTimers();
      const typing = useTyping();
      typing.clearTyping();
    });

    afterEach(() => {
      const typing = useTyping();
      typing.clearTyping();
      vi.useRealTimers();
    });

    it('should return no users when nobody is typing in conversation', () => {
      const typing = useTyping();

      expect(typing.getTypingUsers('conv-1')).toEqual([]);
    });

    it('should return typing users for the correct conversation', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'Alice');
      typing.startTyping('conv-2', 'Bob');

      expect(typing.getTypingUsers('conv-1')).toEqual(['Alice']);
      expect(typing.getTypingUsers('conv-2')).toEqual(['Bob']);
    });

    it('should not include users from other conversations', () => {
      const typing = useTyping();

      typing.startTyping('conv-2', 'Bob');

      expect(typing.getTypingUsers('conv-1')).toEqual([]);
    });

    it('should produce correct text when multiple users type in same conversation', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'Alice');
      typing.startTyping('conv-1', 'Bob');
      typing.startTyping('conv-1', 'Carol');

      const users = typing.getTypingUsers('conv-1');
      expect(users).toHaveLength(3);
      expect(formatTypingText(users)).toContain('and 1 other are typing...');
    });

    it('should produce empty text after users stop typing', () => {
      const typing = useTyping();

      typing.startTyping('conv-1', 'Alice');
      typing.stopTyping('conv-1', 'Alice');

      const users = typing.getTypingUsers('conv-1');
      expect(users).toHaveLength(0);
      expect(formatTypingText(users)).toBe('');
    });
  });
});
