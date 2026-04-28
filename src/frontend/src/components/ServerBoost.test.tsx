import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ServerBoost, {
  TIER_PERKS,
  getNextTierRequirement,
  getBoostsToNextTier,
  formatUploadLimit,
  tierProgressPercent,
} from './ServerBoost';
import { useAuth } from '../stores/auth.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('ServerBoost pure helpers', () => {
  it('TIER_PERKS contains 4 tiers (0..3)', () => {
    expect(TIER_PERKS.map(t => t.tier)).toEqual([0, 1, 2, 3]);
  });

  it('getNextTierRequirement returns next tier required boosts, or null at max', () => {
    expect(getNextTierRequirement(0)).toBe(2);
    expect(getNextTierRequirement(2)).toBe(14);
    expect(getNextTierRequirement(3)).toBeNull();
  });

  it('getBoostsToNextTier returns 0 when above the requirement', () => {
    expect(getBoostsToNextTier(0, 2)).toBe(0);
    expect(getBoostsToNextTier(0, 1)).toBe(1);
    expect(getBoostsToNextTier(3, 0)).toBe(0);
  });

  it('formatUploadLimit returns the upload-limit perk string when present', () => {
    expect(formatUploadLimit(['8 MB upload limit', 'Other'])).toBe('8 MB upload limit');
    expect(formatUploadLimit(['Other'])).toBeUndefined();
  });

  it('tierProgressPercent caps at 100 and floors at 0', () => {
    expect(tierProgressPercent(3, 0)).toBe(100);
    expect(tierProgressPercent(0, 0)).toBe(0);
    expect(tierProgressPercent(0, 1)).toBe(50);
  });
});

describe('ServerBoost', () => {
  beforeEach(() => {
    useAuth().reset();
  });

  it('renders the heading', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => ({
        status: 200,
        body: { serverId: 's-1', boostCount: 0, boostLevel: 0, boosters: [] },
      }),
    });
    const { findByText } = render(() => <ServerBoost serverId="s-1" />);
    expect(await findByText('Server Boost')).toBeInTheDocument();
  });

  it('renders the current tier label and zero-boost copy', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => ({
        status: 200,
        body: { serverId: 's-1', boostCount: 0, boostLevel: 0, boosters: [] },
      }),
    });
    const { findAllByText, findByText } = render(() => <ServerBoost serverId="s-1" />);
    // 'No Tier' appears in both the banner heading and the progress label.
    const noTierMatches = await findAllByText('No Tier');
    expect(noTierMatches.length).toBeGreaterThan(0);
    expect(await findByText(/0 boosts active/i)).toBeInTheDocument();
  });

  it('renders Maximum tier reached message at tier 3', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => ({
        status: 200,
        body: { serverId: 's-1', boostCount: 14, boostLevel: 3, boosters: [] },
      }),
    });
    const { findByText } = render(() => <ServerBoost serverId="s-1" />);
    expect(await findByText(/Maximum tier reached/i)).toBeInTheDocument();
  });

  it('shows error message when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => ({ status: 500, body: { message: 'Boom' } }),
    });
    const { findByText } = render(() => <ServerBoost serverId="s-1" />);
    expect(await findByText(/Failed to load boost status/i)).toBeInTheDocument();
  });

  it('clicking Boost Server opens the confirm dialog with Confirm/Cancel actions', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => ({
        status: 200,
        body: { serverId: 's-1', boostCount: 0, boostLevel: 0, boosters: [] },
      }),
    });
    const { findByLabelText, findByText } = render(() => <ServerBoost serverId="s-1" />);
    fireEvent.click(await findByLabelText('Boost Server'));
    expect(await findByText(/Boost this server\?/i)).toBeInTheDocument();
    expect(await findByLabelText('Confirm boost')).toBeInTheDocument();
    expect(await findByLabelText('Cancel boost')).toBeInTheDocument();
  });

  it('confirming the boost POSTs to the boosts endpoint', async () => {
    let callCount = 0;
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/boosts': () => {
        callCount++;
        return {
          status: 200,
          body: {
            serverId: 's-1',
            boostCount: callCount > 1 ? 1 : 0,
            boostLevel: 0,
            boosters: callCount > 1 ? [{ userId: 'me', startedAt: '2025-01-01T00:00:00Z' }] : [],
          },
        };
      },
      'POST /api/v1/servers/s-1/boosts': () => ({ status: 204, body: null }),
    });
    const { findByLabelText } = render(() => <ServerBoost serverId="s-1" />);
    fireEvent.click(await findByLabelText('Boost Server'));
    fireEvent.click(await findByLabelText('Confirm boost'));
    await waitFor(() =>
      expect(
        calls.calls.some(c => c.method === 'POST' && c.url === '/api/v1/servers/s-1/boosts'),
      ).toBe(true),
    );
  });
});
