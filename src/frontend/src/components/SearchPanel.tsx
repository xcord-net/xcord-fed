import { createSignal, For, Show } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useSearch } from '../stores/search.store';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import type { SearchFilters } from '../types/search';
import type { Message } from '../types/message';

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
    <div data-testid="search-panel" class="flex flex-col h-full bg-xcord-bg-secondary border-l border-xcord-border w-96">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold mb-3">Search</h2>

        <div class="flex space-x-2">
          <input
            data-testid="search-input"
            type="text"
            placeholder="Search messages..."
            class="flex-1 bg-xcord-bg-primary text-white px-3 py-2 rounded border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
            value={query()}
            onInput={(e) => setQuery(e.currentTarget.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
          />
          <button
            data-testid="search-submit-button"
            class="bg-xcord-brand text-white px-4 py-2 rounded hover:bg-xcord-brand-hover transition"
            onClick={handleSearch}
          >
            Search
          </button>
        </div>

        <button
          class="text-xcord-text-muted text-sm mt-2 hover:text-white"
          onClick={() => setShowFilters(!showFilters())}
        >
          {showFilters() ? 'Hide' : 'Show'} Filters
        </button>
      </div>

      <Show when={showFilters()}>
        <div class="px-4 py-3 border-b border-xcord-border space-y-2">
          <label class="block">
            <span class="text-xs text-xcord-text-muted">Has Link</span>
            <input type="checkbox" class="ml-2" />
          </label>
          <label class="block">
            <span class="text-xs text-xcord-text-muted">Has Attachment</span>
            <input type="checkbox" class="ml-2" />
          </label>
        </div>
      </Show>

      <div data-testid="search-results" class="flex-1 overflow-y-auto">
        <Show when={searchStore.isSearching}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Searching...</p>
          </div>
        </Show>

        <Show when={searchStore.results && !searchStore.isSearching}>
          <Show
            when={searchStore.results!.messages.length > 0}
            fallback={
              <div class="flex items-center justify-center h-32">
                <p data-testid="search-no-results" class="text-xcord-text-muted">No results found</p>
              </div>
            }
          >
            <For each={searchStore.results!.messages}>
              {(message) => (
                <div
                  data-testid="search-result-item"
                  class="px-4 py-3 border-b border-xcord-border hover:bg-xcord-bg-primary/30 cursor-pointer"
                  onClick={() => handleJumpToMessage(message)}
                >
                  <div class="flex items-start space-x-3">
                    <div class="w-8 h-8 rounded-full bg-xcord-brand flex items-center justify-center text-white text-sm font-semibold flex-shrink-0">
                      {message.authorUsername?.charAt(0).toUpperCase() || 'U'}
                    </div>

                    <div class="flex-1 min-w-0">
                      <div class="flex items-baseline space-x-2">
                        <span class="font-semibold text-white text-sm">
                          {message.authorUsername || 'Unknown User'}
                        </span>
                        <span class="text-xs text-xcord-text-muted">{formatTime(message.createdAt)}</span>
                      </div>

                      <p class="text-sm text-xcord-text-primary mt-1 break-words">{message.content}</p>
                    </div>
                  </div>
                </div>
              )}
            </For>

            <Show when={searchStore.results!.hasMore}>
              <div class="px-4 py-3 text-center">
                <button class="text-xcord-brand hover:underline">Load more</button>
              </div>
            </Show>
          </Show>
        </Show>
      </div>
    </div>
  );
}
