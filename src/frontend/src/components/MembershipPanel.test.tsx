import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import MembershipPanel from './MembershipPanel';
import { useTiers } from '../stores/tier.store';
import { useSubscriptions } from '../stores/subscription.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const tier = {
  id: 't-1', serverId: 's-1', name: 'Gold', description: 'Best',
  priceMonthly: 500, currency: 'usd', groupIds: [], isActive: true, position: 0,
};

const activeSub = {
  id: 'sub-1', serverId: 's-1', tierId: 't-1', tierName: 'Gold',
  priceMonthly: 500, status: 'Active', currentPeriodEnd: '2099-01-01T00:00:00Z',
};

describe('MembershipPanel', () => {
  let originalLocation: Location;

  beforeEach(() => {
    useTiers().reset();
    useSubscriptions().reset();
    originalLocation = window.location;
    // jsdom location is read-only; replace with a stub that captures `href` assignments.
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: { ...originalLocation, href: '', assign: vi.fn() },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: originalLocation,
    });
  });

  it('renders tier list when no subscription exists', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [tier] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 404, body: { code: 'NO_SUBSCRIPTION', message: 'No active subscription' } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
    });
    const { findByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    expect(await findByTestId('membership-tier-t-1')).toHaveTextContent('Gold');
    expect(await findByTestId('membership-subscribe-t-1')).toBeInTheDocument();
  });

  it('renders current subscription with status badge', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [tier] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 200, body: activeSub }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
    });
    const { findByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    expect(await findByTestId('membership-current-tier-name')).toHaveTextContent('Gold');
    expect(await findByTestId('membership-status-badge')).toHaveTextContent('Active');
  });

  it('redirects to checkout URL when subscribe returns one', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [tier] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 404, body: { code: 'NO_SUBSCRIPTION', message: 'No' } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'POST /api/v1/servers/s-1/subscribe': () => ({ status: 200, body: { checkoutUrl: 'https://checkout.stripe.com/x', requiresCheckout: true, subscription: null } }),
    });
    const { findByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    fireEvent.click(await findByTestId('membership-subscribe-t-1'));
    await waitFor(() => expect(window.location.href).toBe('https://checkout.stripe.com/x'));
  });

  it('cancels subscription when cancel is clicked', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [tier] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 200, body: activeSub }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'POST /api/v1/servers/s-1/subscription/cancel': () => ({ status: 200, body: { message: 'ok' } }),
    });
    const { findByTestId, queryByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    fireEvent.click(await findByTestId('membership-cancel-button'));
    await waitFor(() => expect(queryByTestId('membership-current')).toBeNull());
    expect(calls.calls.some(c => c.method === 'POST' && c.url.endsWith('/subscription/cancel'))).toBe(true);
  });

  it('shows disabled banner when stripe is not configured and no subscription exists', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [tier] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 404, body: { code: 'NO_SUBSCRIPTION', message: 'No' } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: false } }),
    });
    const { findByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    expect(await findByTestId('membership-disabled-banner')).toBeInTheDocument();
  });

  it('shows empty state when there are no active tiers', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [] } }),
      'GET /api/v1/servers/s-1/subscription': () => ({ status: 404, body: { code: 'NO_SUBSCRIPTION', message: 'No' } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
    });
    const { findByTestId } = render(() => <MembershipPanel serverId="s-1" />);
    expect(await findByTestId('membership-no-tiers')).toBeInTheDocument();
  });
});
