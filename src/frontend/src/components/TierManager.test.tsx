import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import TierManager from './TierManager';
import { useTiers } from '../stores/tier.store';
import { useSubscriptions } from '../stores/subscription.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleTier = {
  id: 't-1',
  serverId: 's-1',
  name: 'Gold',
  description: 'Best tier',
  priceMonthly: 500,
  currency: 'usd',
  groupIds: [],
  isActive: true,
  position: 0,
};

describe('TierManager', () => {
  beforeEach(() => {
    useTiers().reset();
    useSubscriptions().reset();
  });

  it('renders heading and fetches tiers on mount', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [sampleTier] } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <TierManager serverId="s-1" />);
    expect(await findByTestId('tier-manager-heading')).toHaveTextContent('Subscription Tiers');
    expect(await findByTestId('tier-item-t-1')).toHaveTextContent('Gold');
  });

  it('shows Stripe banner when not configured', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [] } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: false } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <TierManager serverId="s-1" />);
    expect(await findByTestId('tier-stripe-banner')).toBeInTheDocument();
  });

  it('shows empty state when there are no tiers', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [] } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <TierManager serverId="s-1" />);
    expect(await findByTestId('tier-empty-state')).toBeInTheDocument();
  });

  it('creates a new tier when form is submitted', async () => {
    const created = { ...sampleTier, id: 't-new', name: 'Silver', priceMonthly: 300 };
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [] } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
      'POST /api/v1/servers/s-1/tiers': () => ({ status: 201, body: created }),
    });
    const { findByTestId } = render(() => <TierManager serverId="s-1" />);
    fireEvent.click(await findByTestId('tier-create-button'));
    fireEvent.input(await findByTestId('tier-form-name'), { target: { value: 'Silver' } });
    fireEvent.input(await findByTestId('tier-form-price'), { target: { value: '3.00' } });
    fireEvent.click(await findByTestId('tier-form-save'));
    await waitFor(() => expect(calls.calls.some(c => c.method === 'POST' && c.url.endsWith('/tiers'))).toBe(true));
  });

  it('deletes a tier when delete button is clicked', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 200, body: { tiers: [sampleTier] } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
      'DELETE /api/v1/servers/s-1/tiers/t-1': () => ({ status: 200, body: { message: 'ok' } }),
    });
    const { findByTestId, queryByTestId } = render(() => <TierManager serverId="s-1" />);
    fireEvent.click(await findByTestId('tier-delete-t-1'));
    await waitFor(() => expect(queryByTestId('tier-item-t-1')).toBeNull());
    expect(calls.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/tiers/t-1'))).toBe(true);
  });

  it('shows error banner when fetching tiers fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/tiers': () => ({ status: 500, body: { message: 'Boom' } }),
      'GET /api/v1/servers/s-1/billing/dashboard': () => ({ status: 200, body: { serverId: 's-1', stripeConfigured: true } }),
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <TierManager serverId="s-1" />);
    const banner = await findByTestId('tier-manager-error');
    expect(banner.textContent).toMatch(/Boom|Failed to load tiers/);
  });
});
