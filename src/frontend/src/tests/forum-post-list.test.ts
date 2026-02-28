import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { formatRelativeTime, addTag, removeTag } from '../components/ForumPostList';
import type { ForumPost } from '../types/forum';

// ---- Test data ----

const makePost = (overrides: Partial<ForumPost> = {}): ForumPost => ({
  id: 'post-1',
  channelId: 'ch-1',
  conversationId: 'conv-1',
  authorId: 'user-1',
  authorUsername: 'Alice',
  title: 'Hello World',
  tags: [],
  isPinned: false,
  isLocked: false,
  messageCount: 3,
  createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
  ...overrides,
});

// ---- Tests ----

describe('ForumPostList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Post card rendering ----


  // ---- Tag display ----

  describe('tag display', () => {
    it('adding a new tag appends it to selectedTags', () => {
      // Arrange
      const existing: string[] = ['bug'];

      // Act
      const updated = addTag(existing, 'feature');

      // Assert
      expect(updated).toContain('feature');
      expect(updated).toHaveLength(2);
    });

    it('adding a duplicate tag is a no-op', () => {
      // Arrange
      const existing = ['bug', 'feature'];

      // Act
      const updated = addTag(existing, 'bug');

      // Assert
      expect(updated).toEqual(['bug', 'feature']);
    });

    it('removing a tag filters it out from selectedTags', () => {
      // Arrange
      const existing = ['bug', 'feature', 'help'];

      // Act
      const updated = removeTag(existing, 'feature');

      // Assert
      expect(updated).toEqual(['bug', 'help']);
      expect(updated).not.toContain('feature');
    });
  });

  // ---- Relative timestamp ----

  describe('relative timestamp formatting', () => {
    it('returns "just now" for timestamps within the last minute', () => {
      // Arrange
      const recentDate = new Date(Date.now() - 30 * 1000).toISOString();

      // Act
      const result = formatRelativeTime(recentDate);

      // Assert
      expect(result).toBe('just now');
    });

    it('returns "Xm ago" for timestamps within the last hour', () => {
      // Arrange
      const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000).toISOString();

      // Act
      const result = formatRelativeTime(fiveMinutesAgo);

      // Assert
      expect(result).toBe('5m ago');
    });

    it('returns "Xh ago" for timestamps within the last day', () => {
      // Arrange
      const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();

      // Act
      const result = formatRelativeTime(twoHoursAgo);

      // Assert
      expect(result).toBe('2h ago');
    });

    it('returns "Xd ago" for timestamps more than a day old', () => {
      // Arrange
      const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString();

      // Act
      const result = formatRelativeTime(threeDaysAgo);

      // Assert
      expect(result).toBe('3d ago');
    });

    it('returns "No activity" when dateString is undefined', () => {
      // Act
      const result = formatRelativeTime(undefined);

      // Assert
      expect(result).toBe('No activity');
    });
  });

  // ---- Create post form validation ----


  // ---- API calls ----

  describe('API integration', () => {
    it('createPost sends POST to correct URL with title, content, and tags', async () => {
      // Arrange
      const channelId = 'ch-abc';
      const newPost = makePost({ id: 'post-new', title: 'API Test', tags: ['api'] });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => newPost,
      });

      // Act
      const result = await api.post<ForumPost>(`/api/v1/channels/${channelId}/posts`, {
        title: 'API Test',
        content: 'Content here',
        tagIds: ['api'],
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/channels/${channelId}/posts`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ title: 'API Test', content: 'Content here', tagIds: ['api'] }),
        }),
      );
      expect(result.title).toBe('API Test');
    });

    it('loadPosts calls GET /api/v1/channels/{channelId}/posts', async () => {
      // Arrange
      const channelId = 'ch-xyz';
      const posts: ForumPost[] = [makePost({ id: 'p1' }), makePost({ id: 'p2' })];

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => posts,
      });

      // Act
      const result = await api.get<ForumPost[]>(`/api/v1/channels/${channelId}/posts`);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/channels/${channelId}/posts`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result).toHaveLength(2);
    });
  });
});
