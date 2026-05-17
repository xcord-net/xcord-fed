import { For, Show, createSignal, onMount } from 'solid-js';
import { useTiers } from '../stores/tier.store';
import { useSubscriptions } from '../stores/subscription.store';
import { getErrorMessage } from '../utils/errors';
import type { MemberSubscriptionStatus } from '../types/subscription';
import styles from './MembershipPanel.module.css';

interface MembershipPanelProps {
  serverId: string;
}

function formatPrice(cents: number, currency: string): string {
  const dollars = (cents / 100).toFixed(2);
  return `$${dollars} ${currency.toUpperCase()} / mo`;
}

function formatPeriodEnd(iso?: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function statusClass(status: MemberSubscriptionStatus): string {
  switch (status) {
    case 'Active': return styles.statusActive;
    case 'PastDue': return styles.statusPastDue;
    default: return styles.statusInactive;
  }
}

export default function MembershipPanel(props: MembershipPanelProps) {
  const tierStore = useTiers();
  const subStore = useSubscriptions();

  const [error, setError] = createSignal<string | null>(null);
  const [actionPendingTierId, setActionPendingTierId] = createSignal<string | null>(null);
  const [cancelPending, setCancelPending] = createSignal(false);
  const [loaded, setLoaded] = createSignal(false);

  onMount(async () => {
    try {
      await Promise.all([
        tierStore.fetchTiers(props.serverId),
        subStore.fetchSubscription(props.serverId),
        subStore.fetchBillingConfig(props.serverId).catch(() => {/* non-fatal; treat as configured */}),
      ]);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load membership info'));
    } finally {
      setLoaded(true);
    }
  });

  const stripeConfigured = () => subStore.billingConfig(props.serverId)?.stripeConfigured ?? true;
  const subscription = () => subStore.subscription(props.serverId);
  const activeTiers = () => tierStore.tiers(props.serverId).filter((t) => t.isActive);

  const handleSubscribe = async (tierId: string) => {
    setActionPendingTierId(tierId);
    setError(null);
    try {
      const checkoutUrl = await subStore.subscribe(props.serverId, tierId);
      if (checkoutUrl) {
        window.location.href = checkoutUrl;
      }
      // No URL: subscription was applied directly (dev/self-hosted). Store has been updated.
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to start subscription'));
    } finally {
      setActionPendingTierId(null);
    }
  };

  const handleCancel = async () => {
    setCancelPending(true);
    setError(null);
    try {
      await subStore.cancel(props.serverId);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to cancel subscription'));
    } finally {
      setCancelPending(false);
    }
  };

  return (
    <div class={styles.container}>
      <h2 data-testid="membership-panel-heading" class={styles.heading}>Membership</h2>

      <Show when={error()}>
        <div data-testid="membership-error" role="alert" class={styles.errorBanner}>{error()}</div>
      </Show>

      {/* Existing subscription view */}
      <Show when={subscription()}>
        {(sub) => (
          <div data-testid="membership-current" class={styles.currentSubscription}>
            <span data-testid="membership-current-tier-name" class={styles.subscriptionTitle}>{sub().tierName}</span>
            <span class={styles.subscriptionMeta}>{formatPrice(sub().priceMonthly, 'usd')}</span>
            <span
              data-testid="membership-status-badge"
              class={`${styles.statusBadge} ${statusClass(sub().status)}`}
            >
              {sub().status}
            </span>
            <Show when={sub().currentPeriodEnd}>
              <span class={styles.subscriptionMeta}>
                Renews on {formatPeriodEnd(sub().currentPeriodEnd)}
              </span>
            </Show>
            <button
              data-testid="membership-cancel-button"
              type="button"
              class={styles.cancelButton}
              onClick={handleCancel}
              disabled={cancelPending()}
            >
              {cancelPending() ? 'Cancelling...' : 'Cancel Subscription'}
            </button>
          </div>
        )}
      </Show>

      {/* No-subscription view */}
      <Show when={loaded() && subscription() === null}>
        <Show
          when={stripeConfigured()}
          fallback={
            <p data-testid="membership-disabled-banner" class={styles.disabledBanner}>
              Subscriptions are not currently enabled on this server.
            </p>
          }
        >
          <Show
            when={activeTiers().length > 0}
            fallback={
              <p data-testid="membership-no-tiers" class={styles.emptyState}>
                No subscription tiers are available right now.
              </p>
            }
          >
            <div class={styles.tierGrid}>
              <For each={activeTiers()}>
                {(tier) => (
                  <div data-testid={`membership-tier-${tier.id}`} class={styles.tierCard}>
                    <div class={styles.tierInfo}>
                      <span class={styles.tierName}>{tier.name}</span>
                      <span class={styles.tierPrice}>{formatPrice(tier.priceMonthly, tier.currency)}</span>
                      <Show when={tier.description}>
                        <span class={styles.tierDescription}>{tier.description}</span>
                      </Show>
                    </div>
                    <button
                      data-testid={`membership-subscribe-${tier.id}`}
                      type="button"
                      class={styles.subscribeButton}
                      onClick={() => handleSubscribe(tier.id)}
                      disabled={actionPendingTierId() === tier.id}
                    >
                      {actionPendingTierId() === tier.id ? 'Starting...' : 'Subscribe'}
                    </button>
                  </div>
                )}
              </For>
            </div>
          </Show>
        </Show>
      </Show>
    </div>
  );
}
