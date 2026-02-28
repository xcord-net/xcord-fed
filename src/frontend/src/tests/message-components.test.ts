import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type {
  ButtonStyle,
} from '../components/MessageComponents';
import { buttonStyleClasses, isValidButtonStyle } from '../components/MessageComponents';

// ---- Tests ----

describe('MessageComponents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- ButtonStyle classes ----

  describe('buttonStyleClasses', () => {
    it('Primary style includes brand background', () => {
      // Act
      const classes = buttonStyleClasses('Primary');

      // Assert
      expect(classes).toContain('xcord-brand');
    });

    it('Danger style includes red background', () => {
      // Act
      const classes = buttonStyleClasses('Danger');

      // Assert
      expect(classes).toContain('red');
    });

    it('Success style includes green background', () => {
      // Act
      const classes = buttonStyleClasses('Success');

      // Assert
      expect(classes).toContain('green');
    });

    it('Secondary style does not include brand or destructive colors', () => {
      // Act
      const classes = buttonStyleClasses('Secondary');

      // Assert
      expect(classes).not.toContain('red');
      expect(classes).not.toContain('green');
    });

    it('Link style includes underline styling', () => {
      // Act
      const classes = buttonStyleClasses('Link');

      // Assert
      expect(classes).toContain('underline');
    });

    it('all valid styles return a non-empty class string', () => {
      // Arrange
      const styles: ButtonStyle[] = ['Primary', 'Secondary', 'Success', 'Danger', 'Link'];

      // Assert
      for (const style of styles) {
        expect(buttonStyleClasses(style).length).toBeGreaterThan(0);
      }
    });
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
