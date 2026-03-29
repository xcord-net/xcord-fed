import { describe, it, expect, beforeEach } from 'vitest';
import { usePresence } from '../stores/presence.store';
import { getStatusClass } from '../components/PresenceDot';
import type { PresenceStatus } from '../types/presence';

describe('presence-indicator', () => {
  beforeEach(() => {
    const presence = usePresence();
    presence.clearPresence();
  });

  describe('presence store getStatus', () => {
    it('should return online status for an online user', () => {
      const presence = usePresence();
      presence.updatePresence('user-1', 'online');

      expect(presence.getPresence('user-1')).toBe('online');
    });

    it('should return idle status for an idle user', () => {
      const presence = usePresence();
      presence.updatePresence('user-1', 'idle');

      expect(presence.getPresence('user-1')).toBe('idle');
    });

    it('should return dnd status for a dnd user', () => {
      const presence = usePresence();
      presence.updatePresence('user-1', 'dnd');

      expect(presence.getPresence('user-1')).toBe('dnd');
    });

    it('should return offline status for an offline user', () => {
      const presence = usePresence();
      presence.updatePresence('user-1', 'offline');

      expect(presence.getPresence('user-1')).toBe('offline');
    });
  });

  describe('status colors mapping', () => {
    it('should map online status to green', () => {
      expect(getStatusClass('online')).toContain('statusOnline');
    });

    it('should map idle status to yellow', () => {
      expect(getStatusClass('idle')).toContain('statusIdle');
    });

    it('should map dnd status to red', () => {
      expect(getStatusClass('dnd')).toContain('statusDnd');
    });

    it('should map offline status to gray', () => {
      expect(getStatusClass('offline')).toContain('statusOffline');
    });
  });

  describe('offline/unknown users', () => {
    it('should default unknown user to offline', () => {
      const presence = usePresence();

      expect(presence.getPresence('unknown-user-abc')).toBe('offline');
    });

    it('should treat cleared presence as offline', () => {
      const presence = usePresence();
      presence.updatePresence('user-1', 'online');
      presence.clearPresence();

      expect(presence.getPresence('user-1')).toBe('offline');
    });

    it('unknown user should map to gray dot color', () => {
      const presence = usePresence();
      const status = presence.getPresence('never-seen-user') as PresenceStatus;

      expect(getStatusClass(status)).toContain('statusOffline');
    });
  });
});
