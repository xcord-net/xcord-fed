import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { SearchFilters, SearchResult } from '../types/search';

const store = createRoot(() => {
  const [results, setResults] = createSignal<SearchResult | null>(null);
  const [filters, setFilters] = createSignal<SearchFilters>({ query: '' });
  const [isSearching, setIsSearching] = createSignal(false);

  return {
    results,
    setResults,
    filters,
    setFilters,
    isSearching,
    setIsSearching,
  };
});

export function useSearch() {
  return {
    get results() { return store.results(); },
    get filters() { return store.filters(); },
    get isSearching() { return store.isSearching(); },

    async search(filters: SearchFilters): Promise<void> {
      store.setIsSearching(true);
      try {
        const params = new URLSearchParams();
        params.append('query', filters.query);
        if (filters.authorId) params.append('authorId', filters.authorId);
        if (filters.mentionsUserId) params.append('mentionsUserId', filters.mentionsUserId);
        if (filters.hasLink !== undefined) params.append('hasLink', String(filters.hasLink));
        if (filters.hasEmbed !== undefined) params.append('hasEmbed', String(filters.hasEmbed));
        if (filters.hasAttachment !== undefined) params.append('hasAttachment', String(filters.hasAttachment));
        if (filters.cursor) params.append('cursor', filters.cursor);
        if (filters.channelId) params.append('channelId', filters.channelId);
        params.append('limit', '25');

        const result = await api.get<SearchResult>(`/api/v1/search?${params.toString()}`);
        store.setResults(result);
        store.setFilters(filters);
      } finally {
        store.setIsSearching(false);
      }
    },

    clearSearch(): void {
      store.setResults(null);
      store.setFilters({ query: '' });
    },

    reset(): void {
      store.setResults(null);
      store.setFilters({ query: '' });
      store.setIsSearching(false);
    },
  };
}
