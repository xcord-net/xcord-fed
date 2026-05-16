import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { isValidButtonStyle } from '../components/MessageComponents';

// ---- Tests ----

describe('MessageComponents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- isValidButtonStyle ----

  describe('isValidButtonStyle', () => {
    it('returns true for all valid styles', () => {
      expect(isValidButtonStyle('Primary')).toBe(true);
      expect(isValidButtonStyle('Secondary')).toBe(true);
      expect(isValidButtonStyle('Success')).toBe(true);
      expect(isValidButtonStyle('Danger')).toBe(true);
      expect(isValidButtonStyle('Link')).toBe(true);
    });

    it('returns false for unknown style', () => {
      // Act
      const result = isValidButtonStyle('Ghost');

      // Assert
      expect(result).toBe(false);
    });

    it('is case-sensitive', () => {
      expect(isValidButtonStyle('primary')).toBe(false);
      expect(isValidButtonStyle('PRIMARY')).toBe(false);
    });
  });

  // ---- Interaction API ----

  describe('interaction API', () => {
    it('POST /api/v1/interactions/{id} sends customId and type', async () => {
      // Arrange
      const interactionId = 'interaction-abc';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/interactions/${interactionId}`, {
        customId: 'btn-confirm',
        type: 'button',
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/interactions/${interactionId}`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ customId: 'btn-confirm', type: 'button' }),
        }),
      );
    });

    it('select menu interaction includes values array', async () => {
      // Arrange
      const interactionId = 'interaction-xyz';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/interactions/${interactionId}`, {
        customId: 'menu-color',
        type: 'select_menu',
        values: ['red', 'blue'],
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/interactions/${interactionId}`,
        expect.objectContaining({
          body: JSON.stringify({ customId: 'menu-color', type: 'select_menu', values: ['red', 'blue'] }),
        }),
      );
    });
  });
});
