import { createSignal, For, Show, onMount, onCleanup } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './GifPicker.module.css';
import EmptyState from './ui/EmptyState';

interface GifDto {
  id: string;
  title: string;
  url: string;
  previewUrl: string;
  width: number;
  height: number;
}

interface GifSearchResponse {
  gifs: GifDto[];
}

interface GifPickerProps {
  onSelect: (gifUrl: string) => void;
  onClose?: () => void;
}

const GIF_LIMIT = 25;
const DEBOUNCE_MS = 400;
const COLS = 3;

export default function GifPicker(props: GifPickerProps) {
  const [gifs, setGifs] = createSignal<GifDto[]>([]);
  const [searchQuery, setSearchQuery] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [focusedIndex, setFocusedIndex] = createSignal(-1);

  let debounceTimer: ReturnType<typeof setTimeout> | undefined;
  let searchInputRef: HTMLInputElement | undefined;

  onMount(() => {
    loadTrending();
    searchInputRef?.focus();
  });

  onCleanup(() => {
    if (debounceTimer !== undefined) {
      clearTimeout(debounceTimer);
    }
  });

  async function loadTrending() {
    setIsLoading(true);
    setError(null);
    try {
      const response = await api.get<GifSearchResponse>(
        `/api/v1/gifs/trending?limit=${GIF_LIMIT}`,
      );
      setGifs(response.gifs ?? []);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load trending GIFs'));
    } finally {
      setIsLoading(false);
    }
  }

  async function searchGifs(query: string) {
    if (!query.trim()) {
      await loadTrending();
      return;
    }

    setIsLoading(true);
    setError(null);
    try {
      const response = await api.get<GifSearchResponse>(
        `/api/v1/gifs/search?query=${encodeURIComponent(query.trim())}&limit=${GIF_LIMIT}`,
      );
      setGifs(response.gifs ?? []);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to search GIFs'));
    } finally {
      setIsLoading(false);
    }
  }

  function handleSearchInput(e: Event) {
    const value = (e.target as HTMLInputElement).value;
    setSearchQuery(value);
    setFocusedIndex(-1);

    if (debounceTimer !== undefined) {
      clearTimeout(debounceTimer);
    }

    debounceTimer = setTimeout(() => {
      searchGifs(value);
    }, DEBOUNCE_MS);
  }

  function handleGridKeyDown(e: KeyboardEvent) {
    const count = gifs().length;
    if (count === 0) return;

    let idx = focusedIndex();

    switch (e.key) {
      case 'ArrowRight':
        e.preventDefault();
        idx = idx < 0 ? 0 : (idx + 1) % count;
        break;
      case 'ArrowLeft':
        e.preventDefault();
        idx = idx < 0 ? 0 : (idx - 1 + count) % count;
        break;
      case 'ArrowDown':
        e.preventDefault();
        idx = idx < 0 ? 0 : Math.min(idx + COLS, count - 1);
        break;
      case 'ArrowUp':
        e.preventDefault();
        idx = Math.max(idx - COLS, 0);
        break;
      case 'Enter':
      case ' ':
        e.preventDefault();
        if (focusedIndex() >= 0) {
          const gif = gifs()[focusedIndex()];
          if (gif) props.onSelect(gif.url);
        }
        return;
      case 'Escape':
        e.preventDefault();
        props.onClose?.();
        return;
      default:
        return;
    }

    setFocusedIndex(idx);
    const grid = e.currentTarget as HTMLElement;
    const buttons = grid.querySelectorAll<HTMLElement>('button');
    buttons[idx]?.focus();
  }

  return (
    <div
      role="dialog"
      aria-label="GIF picker"
      class={styles.picker}
    >
      {/* Header */}
      <div class={styles.header}>
        <h3 class={styles.headerTitle}>GIFs</h3>
      </div>

      {/* Search input */}
      <div class={styles.searchBar}>
        <input
          ref={searchInputRef}
          type="text"
          placeholder="Search GIFs..."
          value={searchQuery()}
          onInput={handleSearchInput}
          onKeyDown={(e) => {
            if (e.key === 'Escape') {
              e.preventDefault();
              props.onClose?.();
            }
          }}
          class={styles.searchInput}
          aria-label="Search GIFs"
        />
      </div>

      {/* Error state */}
      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      {/* GIF grid */}
      <div
        class={styles.gridArea}
        onKeyDown={handleGridKeyDown}
      >
        {/* Loading state */}
        <Show when={isLoading()}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading GIFs...</p>
          </div>
        </Show>

        {/* Empty state */}
        <Show when={!isLoading() && gifs().length === 0 && !error()}>
          <EmptyState
            title={searchQuery() ? 'No GIFs match that search' : 'No GIFs to show'}
            body={searchQuery() ? 'Try a different word.' : undefined}
            dense
            data-testid="gif-picker-empty"
          />
        </Show>

        {/* Results grid */}
        <Show when={!isLoading() && gifs().length > 0}>
          <div class={styles.gifGrid}>
            <For each={gifs()}>
              {(gif, index) => (
                <button
                  class={styles.gifBtn}
                  style={{ 'aspect-ratio': `${gif.width} / ${gif.height}` }}
                  onClick={() => props.onSelect(gif.url)}
                  title={gif.title}
                  aria-label={gif.title || 'GIF'}
                  tabIndex={focusedIndex() === index() ? 0 : -1}
                >
                  <img
                    src={gif.previewUrl}
                    alt={gif.title || 'GIF'}
                    class={styles.gifImage}
                    loading="lazy"
                  />
                </button>
              )}
            </For>
          </div>
        </Show>
      </div>

      {/* Footer hint */}
      <div class={styles.footer}>
        <p class={styles.footerText}>
          {searchQuery() ? 'Search results' : 'Trending'}
        </p>
      </div>
    </div>
  );
}
