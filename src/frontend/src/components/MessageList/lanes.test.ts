import { describe, it, expect } from 'vitest';
import type { Message } from '../../types/message';
import { buildLanes, LANE_PALETTE_SIZE } from './lanes';

function msg(id: string, replyToId?: string, authorId = 'u-1'): Message {
  return {
    id,
    conversationId: 'c-1',
    authorId,
    type: 'Default',
    content: `content ${id}`,
    replyToId,
    isPinned: false,
    createdAt: '2026-01-01T00:00:00Z',
  };
}

describe('buildLanes', () => {
  it('assigns no lane to a message with no replies', () => {
    const lanes = buildLanes([msg('a'), msg('b'), msg('c')]);
    expect(lanes.size).toBe(0);
  });

  it('groups a reply with its parent into one lane', () => {
    const lanes = buildLanes([msg('a'), msg('b', 'a')]);

    expect(lanes.get('a')?.laneId).toBe('a');
    expect(lanes.get('b')?.laneId).toBe('a');
  });

  it('extends a lane across a chain of replies', () => {
    const lanes = buildLanes([msg('a'), msg('b', 'a'), msg('c', 'b')]);

    expect(lanes.get('a')?.laneId).toBe('a');
    expect(lanes.get('b')?.laneId).toBe('a');
    expect(lanes.get('c')?.laneId).toBe('a');
  });

  it('joins two replies to the same parent into one lane', () => {
    const lanes = buildLanes([msg('a'), msg('b', 'a'), msg('c', 'a')]);

    expect(lanes.get('b')?.laneId).toBe('a');
    expect(lanes.get('c')?.laneId).toBe('a');
  });

  it('leaves a reply unlaned when its parent is outside the loaded window', () => {
    // 'older' was never loaded, so no edge can form and 'b' stands alone.
    const lanes = buildLanes([msg('b', 'older'), msg('c')]);

    expect(lanes.size).toBe(0);
  });

  it('uses the oldest message in the component as the lane id', () => {
    // Chronological order is the array order, not id order.
    const lanes = buildLanes([msg('zzz'), msg('aaa', 'zzz')]);

    expect(lanes.get('aaa')?.laneId).toBe('zzz');
  });

  it('gives concurrent lanes distinct colours', () => {
    const lanes = buildLanes([
      msg('a'), msg('b', 'a'),
      msg('x'), msg('y', 'x'),
    ]);

    expect(lanes.get('a')?.colorIndex).not.toBe(lanes.get('x')?.colorIndex);
  });

  it('keeps a lane colour stable when a new reply is appended', () => {
    const before = buildLanes([msg('a'), msg('b', 'a'), msg('x'), msg('y', 'x')]);
    const after = buildLanes([msg('a'), msg('b', 'a'), msg('x'), msg('y', 'x'), msg('z', 'y')]);

    expect(after.get('x')?.colorIndex).toBe(before.get('x')?.colorIndex);
    expect(after.get('z')?.colorIndex).toBe(before.get('x')?.colorIndex);
  });

  it('wraps colours once more lanes are on screen than the palette holds', () => {
    const messages: Message[] = [];
    for (let i = 0; i <= LANE_PALETTE_SIZE; i++) {
      messages.push(msg(`root-${i}`), msg(`reply-${i}`, `root-${i}`));
    }

    const lanes = buildLanes(messages);

    expect(lanes.get('root-0')?.colorIndex).toBe(lanes.get(`root-${LANE_PALETTE_SIZE}`)?.colorIndex);
  });

  it('marks continuesPrevious only when the preceding row is in the same lane', () => {
    const lanes = buildLanes([msg('a'), msg('b', 'a'), msg('interloper'), msg('c', 'b')]);

    expect(lanes.get('a')?.continuesPrevious).toBe(false);
    expect(lanes.get('b')?.continuesPrevious).toBe(true);
    expect(lanes.get('c')?.continuesPrevious).toBe(false);
  });

  it('marks continuesNext only when the following row is in the same lane', () => {
    const lanes = buildLanes([msg('a'), msg('b', 'a'), msg('interloper'), msg('c', 'b')]);

    expect(lanes.get('a')?.continuesNext).toBe(true);
    expect(lanes.get('b')?.continuesNext).toBe(false);
    expect(lanes.get('c')?.continuesNext).toBe(false);
  });

  it('handles an optimistic pending reply before the server echoes it', () => {
    const lanes = buildLanes([msg('a'), msg('pending-123', 'a')]);

    expect(lanes.get('pending-123')?.laneId).toBe('a');
  });

  it('ignores a message that somehow replies to itself', () => {
    const lanes = buildLanes([msg('a', 'a')]);

    expect(lanes.size).toBe(0);
  });
});
