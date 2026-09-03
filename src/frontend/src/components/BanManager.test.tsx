import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import BanManager, { filterBans, paginateBans, totalBanPages } from './BanManager';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleBan = {
  id: 'ban-1',
  userId: 'u-1',
  username: 'alice',
  reason: 'spam',
  createdAt: '2025-01-01T00:00:00Z',
};

describe('BanManager pure helpers', () => {
  it('filterBans returns all entries when query is empty', () => {
    const items = [{ username: 'alice' }, { username: 'bob' }];
    expect(filterBans(items, '')).toEqual(items);
  });

  it('filterBans is case-insensitive', () => {
    const items = [{ username: 'Alice' }, { username: 'Bob' }];
    expect(filterBans(items, 'AL')).toEqual([{ username: 'Alice' }]);
  });

  it('paginateBans slices by page and pageSize', () => {
    const items = [1, 2, 3, 4, 5];
    expect(paginateBans(items, 2, 2)).toEqual([3, 4]);
  });

  it('totalBanPages returns at least 1', () => {
    expect(totalBanPages(0, 20)).toBe(1);
    expect(totalBanPages(45, 20)).toBe(3);
  });
});

describe('BanManager', () => {
  it('renders the Bans header', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/bans': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => <BanManager serverId="s-1" />);
    expect(await findByText('Bans')).toBeInTheDocument();
  });

  it('shows empty state when there are no bans', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/bans': () => ({ status: 200, body: [] }) });
    const { findByText, findByTestId } = render(() => <BanManager serverId="s-1" />);
    expect(await findByTestId('ban-manager-empty')).toBeInTheDocument();
  });

  it('renders a banned user row with reason', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/bans': () => ({ status: 200, body: [sampleBan] }) });
    const { findByTestId, container } = render(() => <BanManager serverId="s-1" />);
    expect(await findByTestId('ban-list-item-alice')).toBeInTheDocument();
    await waitFor(() => expect(container.textContent).toContain('Reason: spam'));
  });

  it('shows error banner when load fails', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/bans': () => ({ status: 500, body: { message: 'Boom' } }) });
    const { findByText } = render(() => <BanManager serverId="s-1" />);
    expect(await findByText(/Failed to load bans|Boom/)).toBeInTheDocument();
  });

  it('filters the visible list when typing in the search box', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/bans': () => ({
        status: 200,
        body: [sampleBan, { ...sampleBan, id: 'ban-2', userId: 'u-2', username: 'bob' }],
      }),
    });
    const { findByTestId, queryByTestId, container } = render(() => <BanManager serverId="s-1" />);
    await findByTestId('ban-list-item-alice');
    const search = container.querySelector('input[type="text"]') as HTMLInputElement;
    fireEvent.input(search, { target: { value: 'bob' } });
    await waitFor(() => {
      expect(queryByTestId('ban-list-item-alice')).toBeNull();
      expect(queryByTestId('ban-list-item-bob')).not.toBeNull();
    });
  });
});
