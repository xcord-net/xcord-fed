import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { MemberSubscriptionDto, SubscribeResponse } from '../types/subscription';

interface BillingConfig {
  stripeConfigured: boolean;
}

interface BillingConfigResponse {
  serverId: string;
  stripeConfigured: boolean;
}

/**
 * Cache semantics for `subscriptionByServer`:
 *   undefined → not yet fetched
 *   null      → fetched, server returned 404 (no active subscription)
 *   value     → fetched, active or past-due subscription
 */
const store = createRoot(() => {
  const [subscriptionByServer, setSubscriptionByServer] =
    createSignal<Record<string, MemberSubscriptionDto | null | undefined>>({});
  const [billingConfigByServer, setBillingConfigByServer] =
    createSignal<Record<string, BillingConfig | undefined>>({});

  return {
    subscriptionByServer, setSubscriptionByServer,
    billingConfigByServer, setBillingConfigByServer,
  };
});

function setSubscription(serverId: string, sub: MemberSubscriptionDto | null): void {
  store.setSubscriptionByServer({ ...store.subscriptionByServer(), [serverId]: sub });
}

function setBillingConfig(serverId: string, config: BillingConfig): void {
  store.setBillingConfigByServer({ ...store.billingConfigByServer(), [serverId]: config });
}

export function useSubscriptions() {
  return {
    get subscriptionByServer() { return store.subscriptionByServer(); },
    get billingConfigByServer() { return store.billingConfigByServer(); },

    subscription(serverId: string): MemberSubscriptionDto | null | undefined {
      return store.subscriptionByServer()[serverId];
    },

    billingConfig(serverId: string): BillingConfig | undefined {
      return store.billingConfigByServer()[serverId];
    },

    async fetchSubscription(serverId: string): Promise<MemberSubscriptionDto | null> {
      try {
        const sub = await api.get<MemberSubscriptionDto>(`/api/v1/servers/${serverId}/subscription`);
        setSubscription(serverId, sub);
        return sub;
      } catch (err: unknown) {
        // 404 means "no active subscription" — cache as null
        const e = err as { status?: number; code?: string; title?: string } | null;
        const code = (e?.code ?? e?.title ?? '').toUpperCase();
        if (e?.status === 404 || code === 'NO_SUBSCRIPTION') {
          setSubscription(serverId, null);
          return null;
        }
        throw err;
      }
    },

    async fetchBillingConfig(serverId: string): Promise<BillingConfig> {
      const res = await api.get<BillingConfigResponse>(`/api/v1/servers/${serverId}/billing/dashboard`);
      const config = { stripeConfigured: !!res.stripeConfigured };
      setBillingConfig(serverId, config);
      return config;
    },

    /**
     * Initiates subscribe flow. Returns the Stripe checkout URL when the instance
     * has Stripe configured. When Stripe is not configured the subscription is
     * applied directly (dev/self-hosted) and an empty string is returned; the
     * caller should refresh the subscription view rather than redirecting.
     */
    async subscribe(serverId: string, tierId: string): Promise<string> {
      // Sent as a string: a tier id is a snowflake, and Number() silently
      // rounds anything past 2^53 - so subscribing to a real tier asked the
      // server for an id that does not exist, and every attempt came back
      // "tier not found or inactive". The API reads ids from strings.
      const res = await api.post<SubscribeResponse>(`/api/v1/servers/${serverId}/subscribe`, { tierId: String(tierId) });
      if (res.requiresCheckout && res.checkoutUrl) return res.checkoutUrl;
      if (res.subscription) {
        setSubscription(serverId, res.subscription);
      }
      return '';
    },

    async cancel(serverId: string): Promise<void> {
      await api.post(`/api/v1/servers/${serverId}/subscription/cancel`);
      setSubscription(serverId, null);
    },

    reset(): void {
      store.setSubscriptionByServer({});
      store.setBillingConfigByServer({});
    },
  };
}
