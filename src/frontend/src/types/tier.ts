/**
 * Member-subscription tier types — mirrors backend `Xcord.Features.Billing.TierDto`.
 *
 * Snowflake IDs serialize as strings via the global SnowflakeJsonConverter, so
 * `id`, `serverId`, and `groupIds` are all string-encoded on the wire even though
 * the backend stores them as `long`.
 */
export interface Tier {
  id: string;
  serverId: string;
  name: string;
  description?: string | null;
  /** Price in cents (e.g. 500 = $5.00). Backend enforces 100..100000. */
  priceMonthly: number;
  /** ISO-4217 currency code, lowercase (e.g. "usd"). */
  currency: string;
  /** Group IDs auto-assigned to subscribers on this tier. */
  groupIds: string[];
  isActive: boolean;
  position: number;
}

export interface CreateTierInput {
  name: string;
  description?: string;
  /** Price in cents. */
  priceMonthly: number;
  /** Defaults to "usd" server-side when omitted. */
  currency?: string;
  groupIds?: string[];
}

export interface UpdateTierInput {
  name?: string;
  description?: string;
  priceMonthly?: number;
  groupIds?: string[];
  isActive?: boolean;
}
