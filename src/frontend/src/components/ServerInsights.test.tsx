import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ServerInsights, {
  getMaxValue,
  formatInsightsDate,
  computeBarHeightPct,
} from './ServerInsights';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleInsights = {
  serverId: 's-1',
  totalMembers: 42,
  totalMessages: 1234,
  dataPoints: [
    { date: '2025-01-01', totalMembers: 40, newMembers: 2, messageCount: 100, activeMembers: 5 },
    { date: '2025-01-02', totalMembers: 42, newMembers: 2, messageCount: 200, activeMembers: 7 },
  ],
};

describe('getMaxValue', () => {
  it('returns 1 for empty list', () => {
    expect(getMaxValue([])).toBe(1);
  });

  it('returns the largest value', () => {
    expect(getMaxValue([{ date: 'a', value: 3 }, { date: 'b', value: 7 }])).toBe(7);
  });
});

describe('computeBarHeightPct', () => {
  it('returns 0 when max is 0', () => {
    expect(computeBarHeightPct(5, 0)).toBe(0);
  });

  it('returns rounded percentage', () => {
    expect(computeBarHeightPct(50, 100)).toBe(50);
    expect(computeBarHeightPct(1, 3)).toBe(33);
  });
});

describe('formatInsightsDate', () => {
  it('formats a date string into a short form', () => {
    expect(formatInsightsDate('2025-03-15')).toMatch(/Mar/);
  });
});

describe('ServerInsights', () => {
  it('renders the heading', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/insights': () => ({ status: 200, body: sampleInsights }) });
    const { findByText } = render(() => <ServerInsights serverId="s-1" />);
    expect(await findByText('Server Insights')).toBeInTheDocument();
  });

  it('renders summary stats from insights data', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/insights': () => ({ status: 200, body: sampleInsights }) });
    const { findByText, container } = render(() => <ServerInsights serverId="s-1" />);
    expect(await findByText('Total Members')).toBeInTheDocument();
    expect(await findByText('42')).toBeInTheDocument();
    await waitFor(() => expect(container.textContent).toMatch(/1[,.  ]?234/));
  });

  it('shows error banner when insights fetch fails', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/insights': () => ({ status: 500, body: { message: 'Insights down' } }) });
    const { findByText } = render(() => <ServerInsights serverId="s-1" />);
    expect(await findByText(/Insights down|Failed to load insights/)).toBeInTheDocument();
  });

  it('refetches insights when range button clicked', async () => {
    const calls = mockFetch({ 'GET /api/v1/servers/s-1/insights': () => ({ status: 200, body: sampleInsights }) });
    const { findByText } = render(() => <ServerInsights serverId="s-1" />);
    await findByText('Server Insights');
    const initial = calls.calls.length;
    fireEvent.click(await findByText('7d'));
    await waitFor(() => expect(calls.calls.length).toBeGreaterThan(initial));
    expect(calls.calls.some(c => c.url.includes('days=7'))).toBe(true);
  });

  it('shows the empty channels message when there are no popular channels', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/insights': () => ({ status: 200, body: sampleInsights }) });
    const { findByText } = render(() => <ServerInsights serverId="s-1" />);
    expect(await findByText(/No channel activity in this period/)).toBeInTheDocument();
  });
});
