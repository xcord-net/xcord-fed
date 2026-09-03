import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import AppDirectory, {
  formatInstallCount,
  filterBots,
  sortBotsByInstalls,
} from './AppDirectory';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleApp = {
  id: 'a-1',
  name: 'ModBot',
  shortDescription: 'Moderation utilities',
  iconUrl: '',
  category: 'Moderation',
  installCount: 1234,
  isVerified: true,
};

describe('AppDirectory pure helpers', () => {
  it('formatInstallCount uses K and M suffixes', () => {
    expect(formatInstallCount(500)).toBe('500');
    expect(formatInstallCount(1500)).toBe('1.5K');
    expect(formatInstallCount(2_500_000)).toBe('2.5M');
  });

  it('filterBots returns all bots when query is empty and category is empty', () => {
    const bots = [
      { name: 'A', description: '', tags: [], category: 'Music' },
      { name: 'B', description: '', tags: [], category: 'Games' },
    ] as never[];
    expect(filterBots(bots, '', '')).toHaveLength(2);
  });

  it('filterBots applies category filter', () => {
    const bots = [
      { name: 'A', description: '', tags: [], category: 'Music' },
      { name: 'B', description: '', tags: [], category: 'Games' },
    ] as never[];
    expect(filterBots(bots, '', 'Music')).toHaveLength(1);
  });

  it('filterBots applies query against name/description/tags', () => {
    const bots = [
      { name: 'Alpha', description: 'first', tags: ['cool'], category: 'Music' },
      { name: 'Beta', description: 'second', tags: [], category: 'Music' },
    ] as never[];
    expect(filterBots(bots, 'alpha', '')).toHaveLength(1);
    expect(filterBots(bots, 'cool', '')).toHaveLength(1);
  });

  it('sortBotsByInstalls sorts by installCount desc', () => {
    const bots = [
      { name: 'A', installCount: 1 },
      { name: 'B', installCount: 99 },
    ] as never[];
    expect(sortBotsByInstalls(bots)[0]).toEqual(bots[1]);
  });

  it('filterBots and sortBotsByInstalls handle null/undefined input safely', () => {
    expect(filterBots(null, '', '')).toEqual([]);
    expect(sortBotsByInstalls(undefined)).toEqual([]);
  });
});

describe('AppDirectory', () => {
  it('renders the App Directory header', async () => {
    mockFetch({
      'GET /api/v1/app-directory': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => (
      <AppDirectory availableServerIds={['s-1']} serverNames={{ 's-1': 'My Server' }} />
    ));
    expect(await findByText('App Directory')).toBeInTheDocument();
  });

  it('shows the empty state when there are no bots', async () => {
    mockFetch({
      'GET /api/v1/app-directory': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => (
      <AppDirectory availableServerIds={[]} serverNames={{}} />
    ));
    expect(await findByTestId('empty-state-title')).toBeInTheDocument();
  });

  it('renders a bot card with name and category', async () => {
    mockFetch({
      'GET /api/v1/app-directory': () => ({ status: 200, body: [sampleApp] }),
    });
    const { findByText, findAllByText } = render(() => (
      <AppDirectory availableServerIds={['s-1']} serverNames={{ 's-1': 'My Server' }} />
    ));
    expect(await findByText('ModBot')).toBeInTheDocument();
    // 'Moderation' appears both in the category filter button and the bot card meta.
    const matches = await findAllByText('Moderation');
    expect(matches.length).toBeGreaterThan(0);
  });

  it('opens the bot detail view when a card is clicked', async () => {
    mockFetch({
      'GET /api/v1/app-directory': () => ({ status: 200, body: [sampleApp] }),
    });
    const { findByText, findByLabelText } = render(() => (
      <AppDirectory availableServerIds={['s-1']} serverNames={{ 's-1': 'My Server' }} />
    ));
    fireEvent.click(await findByLabelText('View ModBot details'));
    expect(await findByText('About')).toBeInTheDocument();
    expect(await findByText('Add to Server')).toBeInTheDocument();
  });

  it('filters by typing in the search box', async () => {
    mockFetch({
      'GET /api/v1/app-directory': () => ({
        status: 200,
        body: [sampleApp, { ...sampleApp, id: 'a-2', name: 'OtherBot' }],
      }),
    });
    const { findByText, container, queryByText } = render(() => (
      <AppDirectory availableServerIds={['s-1']} serverNames={{ 's-1': 'My Server' }} />
    ));
    await findByText('ModBot');
    const search = container.querySelector('input[type="search"]') as HTMLInputElement;
    fireEvent.input(search, { target: { value: 'Other' } });
    await waitFor(() => {
      expect(queryByText('ModBot')).toBeNull();
    });
    expect(await findByText('OtherBot')).toBeInTheDocument();
  });

  it('issues POST /api/v1/app-directory/:id/install when Add is clicked', async () => {
    const calls = mockFetch({
      'GET /api/v1/app-directory': () => ({ status: 200, body: [sampleApp] }),
      'POST /api/v1/app-directory/a-1/install': () => ({ status: 204, body: null }),
    });
    const { findByLabelText } = render(() => (
      <AppDirectory availableServerIds={['s-1']} serverNames={{ 's-1': 'My Server' }} />
    ));
    fireEvent.click(await findByLabelText('View ModBot details'));
    fireEvent.click(await findByLabelText('Add ModBot to server'));
    await waitFor(() =>
      expect(
        calls.calls.some(
          (c) => c.method === 'POST' && c.url === '/api/v1/app-directory/a-1/install',
        ),
      ).toBe(true),
    );
  });
});
