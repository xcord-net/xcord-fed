/**
 * Conversation heat: a reply brightens its whole lane, then falls off.
 *
 * The decay is computed in JS rather than run as a CSS animation on purpose.
 * index.css collapses every animation and transition to 0.01ms under
 * `prefers-reduced-motion`, which would turn a CSS-driven falloff into a broken
 * flash. Driving it from state means reduced motion degrades to "no heat at all",
 * leaving the static lane rails — which are a complete experience on their own.
 */

/** How long a lane stays lit after a reply. */
export const HEAT_DURATION_MS = 40_000;

export interface LaneHeatOptions {
  /** False disables heat entirely (reduced motion). */
  enabled: boolean;
}

/** Ease-out: bright and legible early, then a long quiet tail. */
function easeOutCubic(t: number): number {
  return 1 - Math.pow(1 - t, 3);
}

export function createLaneHeat(options: LaneHeatOptions) {
  const litAt = new Map<string, number>();

  /** Drops lanes that have fully cooled. Keeps the map bounded over a long session. */
  function prune(now: number): void {
    for (const [laneId, at] of litAt) {
      if (now - at >= HEAT_DURATION_MS) litAt.delete(laneId);
    }
  }

  return {
    /** Records that a reply just landed in this lane. */
    noteReply(laneId: string, now: number): void {
      if (!options.enabled) return;
      litAt.set(laneId, now);
    },

    /** Current heat for a lane, 1 at the moment of the reply down to 0 at the end. */
    heatFor(laneId: string, now: number): number {
      if (!options.enabled) return 0;
      const at = litAt.get(laneId);
      if (at === undefined) return 0;

      const elapsed = (now - at) / HEAT_DURATION_MS;
      if (elapsed >= 1 || elapsed < 0) return 0;
      return easeOutCubic(1 - elapsed);
    },

    /** True while any lane is still lit. The ticker runs only while this holds. */
    hasHotLanes(now: number): boolean {
      if (!options.enabled) return false;
      prune(now);
      return litAt.size > 0;
    },

    /** Test seam: how many lanes are currently being tracked. */
    trackedLaneCount(): number {
      return litAt.size;
    },
  };
}

export type LaneHeat = ReturnType<typeof createLaneHeat>;

/** True unless the user has asked for reduced motion. */
export function prefersMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return true;
  return !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}
