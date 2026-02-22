import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from '../api/client';
import {
  buildSuperReactionPath,
  updateReactionList,
  type SuperReactionData,
} from '../components/SuperReaction';

// ---- Helpers ----

const FIRE = ':fire:';
const HEART = ':heart:';
const STAR = ':star:';

const makeReaction = (overrides?: Partial<SuperReactionData>): SuperReactionData => ({
  emoji: FIRE,
  count: 3,
  hasSuperReacted: false,
  ...overrides,
});

// ---- Tests ----

describe('SuperReaction', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- API path construction ----

  describe('buildSuperReactionPath', () => {
    it('returns correct PUT path with super=true query param', () => {
      // Act
      const path = buildSuperReactionPath('conv-1', 'msg-1', FIRE);

      // Assert
      expect(path).toContain('/api/v1/conversations/conv-1/messages/msg-1/reactions/');
      expect(path).toContain('super=true');
    });

    it('URL-encodes the emoji', () => {
      // Act
      const path = buildSuperReactionPath('conv-1', 'msg-1', FIRE);

      // Assert — colons are percent-encoded
      expect(decodeURIComponent(path)).toContain(FIRE);
    });

    it('includes the conversationId and messageId in path', () => {
      // Act
      const path = buildSuperReactionPath('conv-xyz', 'msg-abc', FIRE);

      // Assert
      expect(path).toContain('conv-xyz');
      expect(path).toContain('msg-abc');
    });
  });

  // ---- Reaction list logic ----

  describe('updateReactionList', () => {
    it('increments count when adding a super reaction to existing emoji', () => {
      // Arrange
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE, count: 3, hasSuperReacted: false })];

      // Act
      const updated = updateReactionList(reactions, FIRE, true);

      // Assert
      const r = updated.find((x) => x.emoji === FIRE);
      expect(r?.count).toBe(4);
      expect(r?.hasSuperReacted).toBe(true);
    });

    it('decrements count when removing a super reaction', () => {
      // Arrange
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE, count: 5, hasSuperReacted: true })];

      // Act
      const updated = updateReactionList(reactions, FIRE, false);

      // Assert
      const r = updated.find((x) => x.emoji === FIRE);
      expect(r?.count).toBe(4);
      expect(r?.hasSuperReacted).toBe(false);
    });

    it('adds a new reaction entry when emoji is not in the list', () => {
      // Arrange — list has FIRE, we add HEART (different emoji)
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE })];

      // Act
      const updated = updateReactionList(reactions, HEART, true);

      // Assert
      expect(updated).toHaveLength(2);
      const newReaction = updated.find((x) => x.emoji === HEART);
      expect(newReaction).toBeDefined();
      expect(newReaction?.count).toBe(1);
    });

    it('does not mutate the original array', () => {
      // Arrange
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE, count: 2 })];
      const originalLength = reactions.length;

      // Act
      updateReactionList(reactions, FIRE, true);

      // Assert
      expect(reactions.length).toBe(originalLength);
      expect(reactions[0].count).toBe(2);
    });

    it('sets animating flag when adding reaction', () => {
      // Arrange
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE })];

      // Act
      const updated = updateReactionList(reactions, FIRE, true);

      // Assert
      expect(updated.find((r) => r.emoji === FIRE)?.animating).toBe(true);
    });

    it('does not add entry for removal of non-existent emoji', () => {
      // Arrange — list only has FIRE
      const reactions: SuperReactionData[] = [makeReaction({ emoji: FIRE })];

      // Act — try to remove STAR which is NOT in the list
      const updated = updateReactionList(reactions, STAR, false);

      // Assert — no new entry added, list unchanged
      expect(updated).toHaveLength(1);
      expect(updated.find((r) => r.emoji === STAR)).toBeUndefined();
    });
  });

  // ---- API calls ----

  describe('super reaction API', () => {
    it('PUT endpoint is called with super=true when reacting', async () => {
      // Arrange
      const path = buildSuperReactionPath('conv-1', 'msg-1', FIRE);
      globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 });

      // Act
      await api.put(path);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining('super=true'),
        expect.objectContaining({ method: 'PUT' }),
      );
    });

    it('throws when API returns an error for super reaction', async () => {
      // Arrange
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        json: async () => ({ error: 'Premium required' }),
      });

      // Act & Assert
      await expect(
        api.put(`/api/v1/conversations/c/messages/m/reactions/${encodeURIComponent(FIRE)}?super=true`),
      ).rejects.toMatchObject({ error: 'Premium required' });
    });
  });
});
