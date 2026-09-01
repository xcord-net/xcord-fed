import { describe, it, expect } from 'vitest';
import { createLaneHeat, HEAT_DURATION_MS } from './lane-heat';

describe('lane heat', () => {
  it('reads cold for a lane that has never seen a reply', () => {
    const heat = createLaneHeat({ enabled: true });

    expect(heat.heatFor('lane-1', 0)).toBe(0);
  });

  it('is fully hot the instant a reply lands', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 1_000);

    expect(heat.heatFor('lane-1', 1_000)).toBe(1);
  });

  it('decays monotonically over the falloff window', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 0);

    const quarter = heat.heatFor('lane-1', HEAT_DURATION_MS * 0.25);
    const half = heat.heatFor('lane-1', HEAT_DURATION_MS * 0.5);
    const threeQuarters = heat.heatFor('lane-1', HEAT_DURATION_MS * 0.75);

    expect(quarter).toBeLessThan(1);
    expect(half).toBeLessThan(quarter);
    expect(threeQuarters).toBeLessThan(half);
    expect(threeQuarters).toBeGreaterThan(0);
  });

  it('is exactly cold once the falloff window has elapsed', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 0);

    expect(heat.heatFor('lane-1', HEAT_DURATION_MS)).toBe(0);
    expect(heat.heatFor('lane-1', HEAT_DURATION_MS + 5_000)).toBe(0);
  });

  it('falls off between 30 and 45 seconds', () => {
    expect(HEAT_DURATION_MS).toBeGreaterThanOrEqual(30_000);
    expect(HEAT_DURATION_MS).toBeLessThanOrEqual(45_000);
  });

  it('reheats a lane when a further reply lands mid-decay', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 0);
    heat.noteReply('lane-1', HEAT_DURATION_MS * 0.5);

    expect(heat.heatFor('lane-1', HEAT_DURATION_MS * 0.5)).toBe(1);
  });

  it('tracks lanes independently', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 0);

    expect(heat.heatFor('lane-1', 0)).toBe(1);
    expect(heat.heatFor('lane-2', 0)).toBe(0);
  });

  it('stays cold entirely when disabled for reduced motion', () => {
    const heat = createLaneHeat({ enabled: false });
    heat.noteReply('lane-1', 0);

    expect(heat.heatFor('lane-1', 0)).toBe(0);
    expect(heat.hasHotLanes(0)).toBe(false);
  });

  it('reports whether any lane is still hot, so the ticker can stop', () => {
    const heat = createLaneHeat({ enabled: true });
    expect(heat.hasHotLanes(0)).toBe(false);

    heat.noteReply('lane-1', 0);
    expect(heat.hasHotLanes(0)).toBe(true);
    expect(heat.hasHotLanes(HEAT_DURATION_MS)).toBe(false);
  });

  it('forgets cooled lanes so the map cannot grow without bound', () => {
    const heat = createLaneHeat({ enabled: true });
    heat.noteReply('lane-1', 0);

    heat.hasHotLanes(HEAT_DURATION_MS);

    expect(heat.trackedLaneCount()).toBe(0);
  });
});
