/**
 * Slow-mode countdown controller.
 *
 * Extracted from MessageCompose.tsx so that unit tests can import and
 * exercise the production logic directly rather than maintaining a
 * duplicate copy inside the test file.
 */

export interface SlowModeState {
  countdown: number;
  timerId: ReturnType<typeof setInterval> | undefined;
}

export interface SlowModeController {
  startCountdown(onTick?: (remaining: number) => void): void;
  resetCountdown(): void;
  isActive(): boolean;
  getCountdown(): number;
  state: SlowModeState;
}

/**
 * Create a standalone slow-mode countdown controller.
 *
 * @param slowModeSeconds - The channel's slowmode interval in seconds.
 *   Pass 0 to disable slowmode (startCountdown will be a no-op).
 */
export function createSlowModeController(slowModeSeconds: number): SlowModeController {
  const state: SlowModeState = { countdown: 0, timerId: undefined };

  function startCountdown(onTick?: (remaining: number) => void): void {
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

  function resetCountdown(): void {
    if (state.timerId !== undefined) {
      clearInterval(state.timerId);
      state.timerId = undefined;
    }
    state.countdown = 0;
  }

  function isActive(): boolean {
    return state.countdown > 0;
  }

  function getCountdown(): number {
    return state.countdown;
  }

  return { startCountdown, resetCountdown, isActive, getCountdown, state };
}

/**
 * Format a slow-mode countdown value for display.
 *
 * @param seconds - Remaining seconds in the cooldown.
 * @returns A label string, e.g. "Slowmode: 7s".
 */
export function formatSlowModeLabel(seconds: number): string {
  return `Slowmode: ${seconds}s`;
}
