import { describe, it, expect, beforeEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import SearchPanel from './SearchPanel';
import { useSearch } from '../stores/search.store';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('SearchPanel', () => {
  beforeEach(() => {
    useSearch().reset();
    useChannels().reset();
    useServers().reset();
  });

  it('renders search input and submit button', () => {
    const { getByTestId } = renderWithRouter(() => <SearchPanel />);
    expect(getByTestId('search-panel')).toBeInTheDocument();
    expect(getByTestId('search-input')).toBeInTheDocument();
    expect(getByTestId('search-submit-button')).toBeInTheDocument();
  });

  it('does not call API when query is empty/whitespace', () => {
    const calls = mockFetch({});
    const { getByTestId } = renderWithRouter(() => <SearchPanel />);
    fireEvent.input(getByTestId('search-input'), { target: { value: '   ' } });
    fireEvent.click(getByTestId('search-submit-button'));
    expect(calls.calls.length).toBe(0);
  });

  it('issues search request when query is non-empty', async () => {
    const calls = mockFetch({
      'GET /api/v1/search': () => ({ status: 200, body: { messages: [], hasMore: false } }),
    });
    const { getByTestId } = renderWithRouter(() => <SearchPanel />);
    fireEvent.input(getByTestId('search-input'), { target: { value: 'hello' } });
    fireEvent.click(getByTestId('search-submit-button'));
    await waitFor(() => expect(calls.calls.some(c => c.method === 'GET' && c.url.startsWith('/api/v1/search'))).toBe(true));
  });

  it('shows no-results message when search returns empty', async () => {
    mockFetch({
      'GET /api/v1/search': () => ({ status: 200, body: { messages: [], hasMore: false } }),
    });
    const { getByTestId, findByTestId } = renderWithRouter(() => <SearchPanel />);
    fireEvent.input(getByTestId('search-input'), { target: { value: 'nothing' } });
    fireEvent.click(getByTestId('search-submit-button'));
    expect(await findByTestId('search-no-results')).toHaveTextContent('No results found');
  });

  it('renders search result items', async () => {
    mockFetch({
      'GET /api/v1/search': () => ({
        status: 200,
        body: {
          messages: [{
            id: 'm-1', conversationId: 'conv-1', authorId: 'u-1', authorUsername: 'alice',
            type: 'Default', content: 'hello world', isPinned: false, createdAt: '2025-01-01T00:00:00Z',
          }],
          hasMore: false,
        },
      }),
    });
    const { getByTestId, findAllByTestId, container } = renderWithRouter(() => <SearchPanel />);
    fireEvent.input(getByTestId('search-input'), { target: { value: 'hello' } });
    fireEvent.click(getByTestId('search-submit-button'));
    const items = await findAllByTestId('search-result-item');
    expect(items.length).toBe(1);
    expect(container.textContent).toContain('hello world');
    expect(container.textContent).toContain('alice');
  });

  it('toggles filters panel when toggle button clicked', () => {
    const { getByText, queryByText } = renderWithRouter(() => <SearchPanel />);
    expect(queryByText('Has Link')).toBeNull();
    fireEvent.click(getByText('Show Filters'));
    expect(getByText('Has Link')).toBeInTheDocument();
  });
});
