import { For, Show, createSignal, createEffect } from 'solid-js';
import { useForums } from '../stores/forum.store';
import type { ForumPost } from '../types/forum';

interface ForumPostListProps {
  serverId: string;
  channelId: string;
  onSelectPost?: (post: ForumPost) => void;
}

export function formatRelativeTime(dateString?: string): string {
  if (!dateString) return 'No activity';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffDays > 0) return `${diffDays}d ago`;
  if (diffHours > 0) return `${diffHours}h ago`;
  if (diffMinutes > 0) return `${diffMinutes}m ago`;
  return 'just now';
}

export function addTag(existing: string[], newTag: string): string[] {
  const trimmed = newTag.trim();
  if (!trimmed || existing.includes(trimmed)) return existing;
  return [...existing, trimmed];
}

export function removeTag(existing: string[], tag: string): string[] {
  return existing.filter((t) => t !== tag);
}

export default function ForumPostList(props: ForumPostListProps) {
  const forumStore = useForums();
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [title, setTitle] = createSignal('');
  const [content, setContent] = createSignal('');
  const [tagInput, setTagInput] = createSignal('');
  const [selectedTags, setSelectedTags] = createSignal<string[]>([]);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);

  // Re-load posts whenever the channel changes
  createEffect(() => {
    const channelId = props.channelId;
    if (channelId) {
      forumStore.loadPosts(channelId);
      forumStore.loadTags(channelId);
    }
  });

  const handleAddTag = () => {
    const tag = tagInput().trim();
    if (tag && !selectedTags().includes(tag)) {
      setSelectedTags([...selectedTags(), tag]);
      setTagInput('');
    }
  };

  const handleRemoveTag = (tag: string) => {
    setSelectedTags(selectedTags().filter((t) => t !== tag));
  };

  const handleCreatePost = async () => {
    const titleVal = title().trim();
    const contentVal = content().trim();
    if (!titleVal || !contentVal) return;

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const post = await forumStore.createPost(props.channelId, titleVal, contentVal, selectedTags());
      setTitle('');
      setContent('');
      setSelectedTags([]);
      setTagInput('');
      setShowCreateForm(false);
    } catch {
      setSubmitError('Failed to create post. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancelCreate = () => {
    setShowCreateForm(false);
    setTitle('');
    setContent('');
    setSelectedTags([]);
    setTagInput('');
    setSubmitError(null);
  };

  const handlePostClick = (post: ForumPost) => {
    forumStore.selectPost(post.id);
    props.onSelectPost?.(post);
  };

  const formatTime = (dateString?: string) => formatRelativeTime(dateString);

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary" data-testid="forum-post-list">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Forum Posts</h2>
        <button
          data-testid="forum-new-post-button"
          class="bg-xcord-brand text-white px-3 py-1.5 rounded hover:bg-xcord-brand-hover transition-colors text-sm"
          onClick={() => setShowCreateForm(true)}
          aria-label="New Post"
        >
          + New Post
        </button>
      </div>

      {/* Create Post Form */}
      <Show when={showCreateForm()}>
        <div data-testid="forum-create-post-form" class="px-4 py-4 bg-xcord-bg-primary border-b border-xcord-bg-tertiary space-y-3 flex-shrink-0">
          <h3 class="text-xcord-text-primary font-semibold text-sm">Create New Post</h3>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Title
            </label>
            <input
              data-testid="forum-post-title-input"
              type="text"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
              placeholder="Post title..."
              value={title()}
              onInput={(e) => setTitle(e.currentTarget.value)}
            />
          </div>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Content
            </label>
            <textarea
              data-testid="forum-post-content-input"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand resize-none"
              placeholder="Write your post content..."
              rows={4}
              value={content()}
              onInput={(e) => setContent(e.currentTarget.value)}
            />
          </div>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Tags
            </label>
            <div class="flex gap-2 mb-2">
              <input
                type="text"
                class="flex-1 bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                placeholder="Add a tag and press Enter..."
                value={tagInput()}
                onInput={(e) => setTagInput(e.currentTarget.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    handleAddTag();
                  }
                }}
              />
              <button
                class="px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary text-sm rounded hover:bg-xcord-bg-primary transition-colors"
                aria-label="Add tag"
                onClick={handleAddTag}
              >
                Add
              </button>
            </div>
            <Show when={selectedTags().length > 0}>
              <div class="flex flex-wrap gap-1.5">
                <For each={selectedTags()}>
                  {(tag) => (
                    <span class="inline-flex items-center gap-1 px-2 py-0.5 bg-xcord-brand/20 text-xcord-brand text-xs rounded-full">
                      {tag}
                      <button
                        class="hover:text-white transition-colors leading-none"
                        onClick={() => handleRemoveTag(tag)}
                        aria-label={`Remove tag ${tag}`}
                      >
                        x
                      </button>
                    </span>
                  )}
                </For>
              </div>
            </Show>
          </div>

          <Show when={submitError()}>
            <p class="text-red-400 text-xs">{submitError()}</p>
          </Show>

          <div class="flex gap-2">
            <button
              data-testid="forum-create-post-submit"
              class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleCreatePost}
              disabled={isSubmitting() || !title().trim() || !content().trim()}
            >
              {isSubmitting() ? 'Creating...' : 'Create Post'}
            </button>
            <button
              class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Post list */}
      <div class="flex-1 overflow-y-auto">
        <Show when={forumStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!forumStore.isLoading && forumStore.posts.length === 0}>
          <div data-testid="forum-empty-state" class="flex flex-col items-center justify-center h-48 space-y-3">
            <p class="text-xcord-text-muted text-sm">No posts yet. Be the first to start a discussion!</p>
            <button
              class="text-xcord-brand hover:underline text-sm"
              onClick={() => setShowCreateForm(true)}
            >
              Create the first post
            </button>
          </div>
        </Show>

        <Show when={!forumStore.isLoading && forumStore.posts.length > 0}>
          <div class="divide-y divide-xcord-bg-tertiary">
            <For each={forumStore.posts}>
              {(post) => (
                <div
                  data-testid="forum-post-item"
                  class="px-4 py-4 hover:bg-xcord-bg-primary/30 cursor-pointer transition-colors"
                  onClick={() => handlePostClick(post)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') handlePostClick(post);
                  }}
                >
                  <div class="flex items-start space-x-3">
                    {/* Author avatar */}
                    <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold text-sm flex-shrink-0">
                      <Show
                        when={post.authorAvatarUrl}
                        fallback={(post.authorUsername ?? '?').charAt(0).toUpperCase()}
                      >
                        <img
                          src={post.authorAvatarUrl}
                          alt={post.authorUsername ?? ''}
                          class="w-full h-full rounded-full object-cover"
                        />
                      </Show>
                    </div>

                    <div class="flex-1 min-w-0">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex-1 min-w-0">
                          {/* Title + badges */}
                          <div class="flex items-center gap-2 flex-wrap">
                            <h3 class="text-xcord-text-primary font-semibold truncate">{post.title}</h3>
                            <Show when={post.isPinned}>
                              <span class="text-yellow-400 text-xs" title="Pinned">📌</span>
                            </Show>
                            <Show when={post.isLocked}>
                              <span class="text-xcord-text-muted text-xs" title="Locked">🔒</span>
                            </Show>
                          </div>

                          {/* Author + timestamp */}
                          <div class="flex items-center space-x-2 mt-0.5">
                            <span class="text-xs text-xcord-text-muted">{post.authorUsername}</span>
                            <span class="text-xs text-xcord-text-muted">•</span>
                            <span class="text-xs text-xcord-text-muted">
                              {formatTime(post.lastMessageAt ?? post.createdAt)}
                            </span>
                          </div>

                          {/* Tags */}
                          <Show when={(post.tags?.length ?? 0) > 0}>
                            <div class="flex flex-wrap gap-1 mt-2">
                              <For each={post.tags ?? []}>
                                {(tag) => (
                                  <span class="bg-xcord-brand/15 text-xcord-brand px-2 py-0.5 rounded text-xs">
                                    {tag}
                                  </span>
                                )}
                              </For>
                            </div>
                          </Show>
                        </div>

                        {/* Reply count */}
                        <div class="text-right flex-shrink-0 ml-2">
                          <p class="text-sm font-semibold text-xcord-text-primary">{post.messageCount}</p>
                          <p class="text-xs text-xcord-text-muted">
                            {post.messageCount === 1 ? 'reply' : 'replies'}
                          </p>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}
