import { createSignal, For, Show } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useSearch } from '../stores/search.store';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import type { SearchFilters } from '../types/search';
import type { Message } from '../types/message';
import styles from './SearchPanel.module.css';

export default function SearchPanel() {
  const searchStore = useSearch();
  const channelStore = useChannels();
  const serverStore = useServers();
  const navigate = useNavigate();
  const [query, setQuery] = createSignal('');
  const [showFilters, setShowFilters] = createSignal(false);

  const handleJumpToMessage = (message: Message) => {
    // Find channel matching the message's conversationId
    const channel = channelStore.channels.find(
      (c) => c.conversationId === message.conversationId
    );
    if (channel && serverStore.selectedServerId) {
      navigate(`/channels/${serverStore.selectedServerId}/${channel.id}`);
    }
  };

  const handleSearch = async () => {
    if (!query().trim()) return;

    const filters: SearchFilters = {
      query: query(),
    };

    await searchStore.search(filters);
  };

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div data-testid="search-panel" class={styles.panel}>
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Search</h2>

        <div class={styles.searchRow}>
          <input
            data-testid="search-input"
            type="text"
            placeholder="Search messages..."
            class={styles.searchInput}
            value={query()}
            onInput={(e) => setQuery(e.currentTarget.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
          />
          <button
            data-testid="search-submit-button"
            class={styles.searchButton}
            onClick={handleSearch}
          >
            Search
          </button>
        </div>

        <button
          class={styles.filtersToggle}
          onClick={() => setShowFilters(!showFilters())}
        >
          {showFilters() ? 'Hide' : 'Show'} Filters
        </button>
      </div>

      <Show when={showFilters()}>
        <div class={styles.filtersPanel}>
          <label class={styles.filterLabel}>
            <span class={styles.filterLabelText}>Has Link</span>
            <input type="checkbox" class={styles.filterCheckbox} />
          </label>
          <label class={styles.filterLabel}>
            <span class={styles.filterLabelText}>Has Attachment</span>
            <input type="checkbox" class={styles.filterCheckbox} />
          </label>
        </div>
      </Show>

      <div data-testid="search-results" class={styles.results}>
        <Show when={searchStore.isSearching}>
          <div class={styles.centeredStatus}>
            <p class={styles.mutedText}>Searching...</p>
          </div>
        </Show>

        <Show when={searchStore.results && !searchStore.isSearching}>
          <Show
            when={searchStore.results!.messages.length > 0}
            fallback={
              <div class={styles.centeredStatus}>
                <p data-testid="search-no-results" class={styles.mutedText}>No results found</p>
              </div>
            }
          >
            <For each={searchStore.results!.messages}>
              {(message) => (
                <div
                  data-testid="search-result-item"
                  class={styles.resultItem}
                  onClick={() => handleJumpToMessage(message)}
                >
                  <div class={styles.resultItemRow}>
                    <div class={styles.avatar}>
                      {message.authorUsername?.charAt(0).toUpperCase() || 'U'}
                    </div>

                    <div class={styles.resultContent}>
                      <div class={styles.resultMeta}>
                        <span class={styles.authorName}>
                          {message.authorUsername || 'Unknown User'}
                        </span>
                        <span class={styles.timestamp}>{formatTime(message.createdAt)}</span>
                      </div>

                      <p class={styles.resultMessage}>{message.content}</p>
                    </div>
                  </div>
                </div>
              )}
            </For>

            <Show when={searchStore.results!.hasMore}>
              <div class={styles.loadMoreRow}>
                <button class={styles.loadMoreButton}>Load more</button>
              </div>
            </Show>
          </Show>
        </Show>
      </div>
    </div>
  );
}
