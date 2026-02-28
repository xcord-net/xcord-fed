import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createSlowModeController, formatSlowModeLabel } from '../utils/slowmode';

/**
 * Slow Mode tests — Card 168
 *
 * These tests exercise the slow-mode countdown logic exported from
 * utils/slowmode.ts, which is the same module used by MessageCompose.tsx.
 * Tests import the production code directly so that any regression in the
 * real implementation will cause these tests to fail.
 */

describe('slow-mode', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('shows countdown after message send', () => {
    it('sets countdown to the channel slowModeSeconds value after send', () => {
      // Arrange
      const ctrl = createSlowModeController(10);

      // Act — simulate post-send trigger
      ctrl.startCountdown();

      // Assert
      expect(ctrl.getCountdown()).toBe(10);
    });

    it('isActive returns true immediately after countdown starts', () => {
      // Arrange
      const ctrl = createSlowModeController(5);

      // Act
      ctrl.startCountdown();

      // Assert
      expect(ctrl.isActive()).toBe(true);
    });
  });

  describe('disables send button during cooldown', () => {
    it('isActive returns true while countdown is above zero', () => {
      // Arrange
      const ctrl = createSlowModeController(3);
      ctrl.startCountdown();

      // Assert — still active before any ticks
      expect(ctrl.isActive()).toBe(true);

      // Advance 2 s — still active
      vi.advanceTimersByTime(2000);
      expect(ctrl.isActive()).toBe(true);
    });

    it('isActive returns false once countdown reaches zero', () => {
      // Arrange
      const ctrl = createSlowModeController(3);
      ctrl.startCountdown();

      // Act — advance past the full countdown
      vi.advanceTimersByTime(3000);

      // Assert
      expect(ctrl.isActive()).toBe(false);
    });
  });

  describe('countdown decrements correctly', () => {
    it('countdown decreases by 1 for each second elapsed', () => {
      // Arrange
      const ctrl = createSlowModeController(5);
      ctrl.startCountdown();

      // Act — advance 1 second at a time and record values
      const recorded: number[] = [];
      for (let i = 0; i < 5; i++) {
        vi.advanceTimersByTime(1000);
        recorded.push(ctrl.getCountdown());
      }

      // Assert — [4, 3, 2, 1, 0]
      expect(recorded).toEqual([4, 3, 2, 1, 0]);
    });

    it('clears the interval when countdown reaches zero', () => {
      // Arrange
      const ctrl = createSlowModeController(2);
      ctrl.startCountdown();

      vi.advanceTimersByTime(2000);

      // Assert — internal timer cleared
      expect(ctrl.state.timerId).toBeUndefined();
    });
  });

  describe('no countdown when slowmode is 0', () => {
    it('does not start a countdown when slowModeSeconds is 0', () => {
      // Arrange
      const ctrl = createSlowModeController(0);

      // Act
      ctrl.startCountdown();

      // Assert
      expect(ctrl.isActive()).toBe(false);
      expect(ctrl.getCountdown()).toBe(0);
    });

    it('isActive returns false before any send when slowModeSeconds is 0', () => {
      // Arrange
      const ctrl = createSlowModeController(0);

      // Assert — no send yet, no countdown
      expect(ctrl.isActive()).toBe(false);
    });
  });

  describe('countdown resets on channel switch', () => {
    it('resetCountdown sets countdown to 0', () => {
      // Arrange
      const ctrl = createSlowModeController(30);
      ctrl.startCountdown();
      expect(ctrl.isActive()).toBe(true);

      // Act — simulate channel switch
      ctrl.resetCountdown();

      // Assert
      expect(ctrl.getCountdown()).toBe(0);
      expect(ctrl.isActive()).toBe(false);
    });

    it('resetCountdown clears the interval so no further ticks occur', () => {
      // Arrange
      const ctrl = createSlowModeController(10);
      ctrl.startCountdown();

      // Act — reset mid-countdown, then advance time
      ctrl.resetCountdown();
      vi.advanceTimersByTime(5000);

      // Assert — countdown stays at 0 (interval was cleared)
      expect(ctrl.getCountdown()).toBe(0);
    });

    it('starting a new countdown after reset uses the correct initial value', () => {
      // Arrange — simulate switching to a different channel (controller rebuilt)
      const ctrl1 = createSlowModeController(10);
      ctrl1.startCountdown();
      ctrl1.resetCountdown();

      const ctrl2 = createSlowModeController(20);
      ctrl2.startCountdown();

      // Assert
      expect(ctrl2.getCountdown()).toBe(20);
    });
  });

  describe('label format', () => {
    it('formats the label as "Slowmode: {n}s"', () => {
      // Arrange
      const ctrl = createSlowModeController(7);
      ctrl.startCountdown();

      // Act — build the label using the production formatSlowModeLabel function
      const label = formatSlowModeLabel(ctrl.getCountdown());

      // Assert
      expect(label).toBe('Slowmode: 7s');
    });
  });
});
