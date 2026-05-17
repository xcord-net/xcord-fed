/**
 * Member-subscription types — mirrors backend `MemberSubscriptionDto` and
 * `MemberSubscriptionStatus` enum (serialized as a string via the global
 * JsonStringEnumConverter).
 */

export type MemberSubscriptionStatus =
  | 'Active'
  | 'PastDue'
  | 'Cancelled'
  | 'Expired';

export interface MemberSubscriptionDto {
  id: string;
  serverId: string;
  tierId: string;
  tierName: string;
  /** Price in cents at time of subscription. */
  priceMonthly: number;
  status: MemberSubscriptionStatus;
  /** ISO-8601 timestamp; null when the subscription has no current period. */
  currentPeriodEnd?: string | null;
}

/**
 * Response from POST /api/v1/servers/{serverId}/subscribe.
 *
 * - If the instance has Stripe configured, `checkoutUrl` is set and the caller
 *   redirects to it (`requiresCheckout === true`).
 * - If Stripe is NOT configured (dev/self-hosted), the subscription is applied
 *   directly and returned inline as `subscription`.
 */
export interface SubscribeResponse {
  checkoutUrl: string | null;
  requiresCheckout: boolean;
  subscription: MemberSubscriptionDto | null;
}
