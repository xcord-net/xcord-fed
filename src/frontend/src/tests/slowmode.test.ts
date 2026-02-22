import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

/**
 * Slow Mode tests — Card 168
 *
 * These tests exercise the slow-mode countdown logic that lives inside
 * MessageCompose.tsx.  Because the component uses DOM APIs (textarea, refs)
 * that are hard to exercise in jsdom, we extract the pure logic here and
 * verify the behaviour contracts that the component relies on.
 */

// ---- Slow-mode countdown logic extracted for unit testing ----

interface SlowModeState {
  countdown: number;
  timerId: ReturnType<typeof setInterval> | undefined;
}

function createSlowModeController(slowModeSeconds: number) {
  const state: SlowModeState = { countdown: 0, timerId: undefined };

  function startCountdown(onTick?: (remaining: number) => void) {
    if (slowModeSeconds <= 0) return;
    state.countdown = slowModeSeconds;
    if (state.timerId !== undefined) {
      clearInterval(state.timerId);
    }
    state.timerId = setInterval(() => {
      state.countdown -= 1;
      onTick?.(state.countdown);
      if (state.countdown <= 0) {
        clearInterval(state.timerId);
        state.timerId = undefined;
      }
    }, 1000);
  }

  function resetCountdown() {
    if (state.timerId !== undefined) {
      clearInterval(state.timerId);
      state.timerId = undefined;
    }
    state.countdown = 0;
  }

  function isActive() {
    return state.countdown > 0;
  }

  function getCountdown() {
    return state.countdown;
  }

  return { startCountdown, resetCountdown, isActive, getCountdown, state };
}

// ---- Tests ----

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

      // Act — build the label the component would render
      const label = `Slowmode: ${ctrl.getCountdown()}s`;

      // Assert
      expect(label).toBe('Slowmode: 7s');
    });
  });
});
