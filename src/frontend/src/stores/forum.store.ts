import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { ForumPost, ForumTag } from '../types/forum';

// Backend response types (as returned by the API)
interface ForumPostDto {
  threadId: string;
  conversationId: string;
  channelId: string;
  title: string;
  authorId?: string;
  authorUsername?: string;
  firstMessagePreview?: string;
  tags: string[];
  messageCount: number;
  isArchived: boolean;
  isLocked: boolean;
  lastActivityAt: string;
  createdAt: string;
}

interface ListForumPostsResponse {
  posts: ForumPostDto[];
}

interface CreateForumPostResponse {
  threadId: string;
  conversationId: string;
  channelId: string;
  title: string;
  authorUsername: string;
  firstMessageId: string;
  tags: string[];
  createdAt: string;
}

function mapDtoToPost(dto: ForumPostDto): ForumPost {
  return {
    id: dto.threadId,
    channelId: dto.channelId,
    conversationId: dto.conversationId,
    authorId: dto.authorId,
    authorUsername: dto.authorUsername,
    title: dto.title,
    tags: dto.tags ?? [],
    isPinned: false,
    isLocked: dto.isLocked ?? false,
    messageCount: dto.messageCount ?? 0,
    lastMessageAt: dto.lastActivityAt,
    createdAt: dto.createdAt,
  };
}

function mapCreateResponseToPost(resp: CreateForumPostResponse): ForumPost {
  return {
    id: resp.threadId,
    channelId: resp.channelId,
    conversationId: resp.conversationId,
    authorUsername: resp.authorUsername,
    title: resp.title,
    tags: resp.tags ?? [],
    isPinned: false,
    isLocked: false,
    messageCount: 1,
    createdAt: resp.createdAt,
  };
}

const store = createRoot(() => {
  const [posts, setPosts] = createSignal<ForumPost[]>([]);
  const [tags, setTags] = createSignal<ForumTag[]>([]);
  const [selectedPostId, setSelectedPostId] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    posts,
    setPosts,
    tags,
    setTags,
    selectedPostId,
    setSelectedPostId,
    isLoading,
    setIsLoading,
  };
});

export function useForums() {
  return {
    get posts() { return store.posts(); },
    get tags() { return store.tags(); },
    get selectedPostId() { return store.selectedPostId(); },
    get isLoading() { return store.isLoading(); },

    async loadPosts(channelId: string): Promise<void> {
      store.setIsLoading(true);
      try {
        const resp = await api.get<ListForumPostsResponse>(`/api/v1/channels/${channelId}/posts`);
        const rawPosts: ForumPostDto[] = Array.isArray(resp) ? (resp as unknown as ForumPostDto[]) : (resp?.posts ?? []);
        store.setPosts(rawPosts.map(mapDtoToPost));
      } catch {
        store.setPosts([]);
      } finally {
        store.setIsLoading(false);
      }
    },

    async loadTags(channelId: string): Promise<void> {
      try {
        const tags = await api.get<ForumTag[]>(`/api/v1/channels/${channelId}/tags`);
        store.setTags(Array.isArray(tags) ? tags : []);
      } catch {
        store.setTags([]);
      }
    },

    async createPost(channelId: string, title: string, content: string, tags: string[]): Promise<ForumPost> {
      const resp = await api.post<CreateForumPostResponse>(`/api/v1/channels/${channelId}/posts`, {
        title,
        content,
        tags,
      });
      const post = mapCreateResponseToPost(resp);
      store.setPosts([post, ...store.posts()]);
      return post;
    },

    async pinPost(channelId: string, postId: string): Promise<void> {
      const updated = await api.post<ForumPostDto>(`/api/v1/channels/${channelId}/posts/${postId}/pin`, {});
      store.setPosts(store.posts().map((p) => (p.id === postId ? mapDtoToPost(updated) : p)));
    },

    async lockPost(channelId: string, postId: string): Promise<void> {
      const updated = await api.post<ForumPostDto>(`/api/v1/channels/${channelId}/posts/${postId}/lock`, {});
      store.setPosts(store.posts().map((p) => (p.id === postId ? mapDtoToPost(updated) : p)));
    },

    async deletePost(channelId: string, postId: string): Promise<void> {
      await api.delete(`/api/v1/channels/${channelId}/posts/${postId}`);
      store.setPosts(store.posts().filter((p) => p.id !== postId));
    },

    selectPost(postId: string | null): void {
      store.setSelectedPostId(postId);
    },

    clearPosts(): void {
      store.setPosts([]);
    },

    reset(): void {
      store.setPosts([]);
      store.setTags([]);
      store.setSelectedPostId(null);
      store.setIsLoading(false);
    },
  };
}
