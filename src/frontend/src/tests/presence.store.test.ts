import { describe, it, expect, beforeEach } from 'vitest';
import { usePresence } from '../stores/presence.store';

describe('presence.store', () => {
  beforeEach(() => {
    const presence = usePresence();
    presence.clearPresence();
  });

  describe('updatePresence', () => {
    it('should set user presence status', () => {
      const presence = usePresence();

      presence.updatePresence('user-1', 'online');

      expect(presence.getPresence('user-1')).toBe('online');
    });

    it('should update existing presence', () => {
      const presence = usePresence();

      presence.updatePresence('user-1', 'online');
      presence.updatePresence('user-1', 'idle');

      expect(presence.getPresence('user-1')).toBe('idle');
    });

    it('should track multiple users independently', () => {
      const presence = usePresence();

      presence.updatePresence('user-1', 'online');
      presence.updatePresence('user-2', 'dnd');
      presence.updatePresence('user-3', 'idle');

      expect(presence.getPresence('user-1')).toBe('online');
      expect(presence.getPresence('user-2')).toBe('dnd');
      expect(presence.getPresence('user-3')).toBe('idle');
    });
  });

  describe('getPresence', () => {
    it('should return offline for unknown user', () => {
      const presence = usePresence();

      expect(presence.getPresence('unknown-user')).toBe('offline');
    });
  });

  describe('clearPresence', () => {
    it('should remove all presence data', () => {
      const presence = usePresence();

      presence.updatePresence('user-1', 'online');
      presence.updatePresence('user-2', 'dnd');

      presence.clearPresence();

      expect(presence.getPresence('user-1')).toBe('offline');
      expect(presence.getPresence('user-2')).toBe('offline');
      expect(presence.presenceMap.size).toBe(0);
    });
  });
});
