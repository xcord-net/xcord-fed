import type { Message } from '../../types/message';

/** Number of lane hues defined as --color-xcord-lane-N in index.css. */
export const LANE_PALETTE_SIZE = 4;

export interface LaneAssignment {
  /** Id of the oldest message in the exchange. Stable while the exchange grows forward. */
  laneId: string;
  /** 0-based index into the lane palette. */
  colorIndex: number;
  /** True when the immediately preceding row belongs to the same lane. Together with
   *  `continuesNext` this decides where the rail gets a rounded cap, so a run of
   *  adjacent rows reads as one unbroken line. */
  continuesPrevious: boolean;
  /** True when the immediately following row belongs to the same lane. */
  continuesNext: boolean;
}

/**
 * Groups reply-connected messages into lanes so a back-and-forth reads as one unit
 * inside the channel.
 *
 * `messages` must be in chronological order (oldest first), the order the message
 * store holds them in. Array position — not id ordering — decides which message is
 * the oldest, so this stays correct for optimistic `pending-*` ids.
 *
 * Reply edges whose parent is outside the loaded window are ignored: that message
 * stays unlaned (it still renders its quote line). Components of a single message
 * get no lane, so an ordinary message is never railed.
 */
export function buildLanes(messages: readonly Message[]): Map<string, LaneAssignment> {
  const assignments = new Map<string, LaneAssignment>();
  if (messages.length === 0) return assignments;

  const indexById = new Map<string, number>();
  messages.forEach((m, i) => indexById.set(m.id, i));

  // Union-find over message indices, keeping the lowest index (oldest message) as
  // each component's representative.
  const parent = messages.map((_, i) => i);
  const find = (i: number): number => {
    let root = i;
    while (parent[root] !== root) root = parent[root];
    // Path compression keeps repeated lookups flat over a 50-message window.
    let walk = i;
    while (parent[walk] !== walk) {
      const next = parent[walk];
      parent[walk] = root;
      walk = next;
    }
    return root;
  };
  const union = (a: number, b: number) => {
    const rootA = find(a);
    const rootB = find(b);
    if (rootA === rootB) return;
    if (rootA < rootB) parent[rootB] = rootA;
    else parent[rootA] = rootB;
  };

  messages.forEach((m, i) => {
    if (!m.replyToId) return;
    const parentIndex = indexById.get(m.replyToId);
    if (parentIndex === undefined || parentIndex === i) return;
    union(i, parentIndex);
  });

  // Collect components, dropping singletons.
  const membersByRoot = new Map<number, number[]>();
  messages.forEach((_, i) => {
    const root = find(i);
    const members = membersByRoot.get(root);
    if (members) members.push(i);
    else membersByRoot.set(root, [i]);
  });

  // Colour by appearance order rather than by hashing the id: deterministic, and
  // lanes visible at the same time never collide until a fifth one appears.
  const roots = [...membersByRoot.keys()]
    .filter((root) => (membersByRoot.get(root)?.length ?? 0) > 1)
    .sort((a, b) => a - b);

  roots.forEach((root, ordinal) => {
    const laneId = messages[root].id;
    const colorIndex = ordinal % LANE_PALETTE_SIZE;
    for (const memberIndex of membersByRoot.get(root)!) {
      assignments.set(messages[memberIndex].id, {
        laneId,
        colorIndex,
        continuesPrevious: memberIndex > 0 && find(memberIndex - 1) === root,
        continuesNext: memberIndex < messages.length - 1 && find(memberIndex + 1) === root,
      });
    }
  });

  return assignments;
}
