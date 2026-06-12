import { For, Show, createSignal, createEffect } from 'solid-js';
import { useForums } from '../stores/forum.store';
import type { ForumPost } from '../types/forum';
import styles from './ForumPostList.module.css';

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
    <div class={styles.container} data-testid="forum-post-list">
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Forum Posts</h2>
        <button
          data-testid="forum-new-post-button"
          class={styles.newPostButton}
          onClick={() => setShowCreateForm(true)}
          aria-label="New Post"
        >
          + New Post
        </button>
      </div>

      {/* Create Post Form */}
      <Show when={showCreateForm()}>
        <div data-testid="forum-create-post-form" class={styles.createForm}>
          <h3 class={styles.createFormTitle}>Create New Post</h3>

          <div class={styles.fieldGroup}>
            <label class={styles.fieldLabel}>
              Title
            </label>
            <input
              data-testid="forum-post-title-input"
              type="text"
              class={styles.textInput}
              placeholder="Post title..."
              value={title()}
              onInput={(e) => setTitle(e.currentTarget.value)}
            />
          </div>

          <div class={styles.fieldGroup}>
            <label class={styles.fieldLabel}>
              Content
            </label>
            <textarea
              data-testid="forum-post-content-input"
              class={styles.textarea}
              placeholder="Write your post content..."
              rows={4}
              value={content()}
              onInput={(e) => setContent(e.currentTarget.value)}
            />
          </div>

          <div class={styles.fieldGroup}>
            <label class={styles.fieldLabel}>
              Tags
            </label>
            <div class={styles.tagInputRow}>
              <input
                type="text"
                class={styles.textInput}
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
                class={styles.addTagButton}
                aria-label="Add tag"
                onClick={handleAddTag}
              >
                Add
              </button>
            </div>
            <Show when={selectedTags().length > 0}>
              <div class={styles.selectedTags}>
                <For each={selectedTags()}>
                  {(tag) => (
                    <span class={styles.tagChip}>
                      {tag}
                      <button
                        class={styles.removeTagButton}
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
            <p class={styles.submitError}>{submitError()}</p>
          </Show>

          <div class={styles.formActions}>
            <button
              data-testid="forum-create-post-submit"
              class={styles.submitButton}
              onClick={handleCreatePost}
              disabled={isSubmitting() || !title().trim() || !content().trim()}
            >
              {isSubmitting() ? 'Creating...' : 'Create Post'}
            </button>
            <button
              class={styles.cancelButton}
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Post list */}
      <div class={styles.postList}>
        <Show when={forumStore.isLoading}>
          <div class={styles.spinnerWrapper}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!forumStore.isLoading && forumStore.loadError}>
          <div data-testid="forum-load-error" class={styles.emptyState}>
            <p class={styles.emptyStateText}>Couldn't load posts. Check your connection and try again.</p>
          </div>
        </Show>

        <Show when={!forumStore.isLoading && !forumStore.loadError && forumStore.posts.length === 0}>
          <div data-testid="forum-empty-state" class={styles.emptyState}>
            <p class={styles.emptyStateText}>No posts yet. Be the first to start a discussion!</p>
            <button
              class={styles.emptyStateLink}
              onClick={() => setShowCreateForm(true)}
            >
              Create the first post
            </button>
          </div>
        </Show>

        <Show when={!forumStore.isLoading && forumStore.posts.length > 0}>
          <div class={styles.postDividerList}>
            <For each={forumStore.posts}>
              {(post) => (
                <div
                  data-testid="forum-post-item"
                  class={styles.postItem}
                  onClick={() => handlePostClick(post)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') handlePostClick(post);
                  }}
                >
                  <div class={styles.postItemRow}>
                    {/* Author avatar */}
                    <div class={styles.authorAvatar}>
                      <Show
                        when={post.authorAvatarUrl}
                        fallback={(post.authorUsername ?? '?').charAt(0).toUpperCase()}
                      >
                        <img
                          src={post.authorAvatarUrl}
                          alt={post.authorUsername ?? ''}
                          class={styles.authorAvatarImg}
                        />
                      </Show>
                    </div>

                    <div class={styles.postBody}>
                      <div class={styles.postBodyInner}>
                        <div class={styles.postMeta}>
                          {/* Title + badges */}
                          <div class={styles.postTitleRow}>
                            <h3 class={styles.postTitle}>{post.title}</h3>
                            <Show when={post.isPinned}>
                              <span class={styles.pinnedIcon} title="Pinned">📌</span>
                            </Show>
                            <Show when={post.isLocked}>
                              <span class={styles.lockedIcon} title="Locked">🔒</span>
                            </Show>
                          </div>

                          {/* Author + timestamp */}
                          <div class={styles.postAuthorRow}>
                            <span class={styles.postAuthorName}>{post.authorUsername}</span>
                            <span class={styles.postDot}>•</span>
                            <span class={styles.postTimestamp}>
                              {formatTime(post.lastMessageAt ?? post.createdAt)}
                            </span>
                          </div>

                          {/* Tags */}
                          <Show when={(post.tags?.length ?? 0) > 0}>
                            <div class={styles.postTags}>
                              <For each={post.tags ?? []}>
                                {(tag) => (
                                  <span class={styles.postTag}>
                                    {tag}
                                  </span>
                                )}
                              </For>
                            </div>
                          </Show>
                        </div>

                        {/* Reply count */}
                        <div class={styles.replyCount}>
                          <p class={styles.replyCountNumber}>{post.messageCount}</p>
                          <p class={styles.replyCountLabel}>
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
