import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import { useAuth } from '../stores/auth.store';

// ---- Types ----

export type BoostTier = 0 | 1 | 2 | 3;

export interface BoostStatus {
  serverId: string;
  tier: BoostTier;
  boostCount: number;
  boostedByMe: boolean;
  myBoostCount: number;
  premiumSubscriberCount: number;
  boostsToNextTier?: number;
}

// Backend API response shape from GET /api/v1/servers/{id}/boosts
interface BoostApiResponse {
  serverId: string;
  boostCount: number;
  boostLevel: number;
  boosters: Array<{ userId: string; startedAt: string }>;
}

export interface TierPerks {
  tier: BoostTier;
  label: string;
  requiredBoosts: number;
  perks: string[];
}

interface ServerBoostProps {
  serverId: string;
}

// ---- Constants ----

export const TIER_PERKS: TierPerks[] = [
  {
    tier: 0,
    label: 'No Tier',
    requiredBoosts: 0,
    perks: ['Basic server features'],
  },
  {
    tier: 1,
    label: 'Level 1',
    requiredBoosts: 2,
    perks: [
      '50 custom emoji slots',
      'Custom server invite background',
      '8 MB upload limit',
    ],
  },
  {
    tier: 2,
    label: 'Level 2',
    requiredBoosts: 7,
    perks: [
      '100 custom emoji slots',
      'Custom server banner',
      '50 MB upload limit',
      'Server discovery eligible',
    ],
  },
  {
    tier: 3,
    label: 'Level 3',
    requiredBoosts: 14,
    perks: [
      '250 custom emoji slots',
      'Custom vanity invite URL',
      '100 MB upload limit',
      'Animated server icon',
      'Server discovery featured',
    ],
  },
];

// ---- Helpers ----

export function getNextTierRequirement(tier: BoostTier): number | null {
  const next = TIER_PERKS.find((t) => t.tier === (tier + 1) as BoostTier);
  return next ? next.requiredBoosts : null;
}

export function getBoostsToNextTier(tier: BoostTier, boostCount: number): number {
  const nextRequired = getNextTierRequirement(tier);
  if (nextRequired === null) return 0;
  return Math.max(0, nextRequired - boostCount);
}

export function formatUploadLimit(perks: string[]): string | undefined {
  return perks.find((p) => p.includes('upload limit'));
}

export function tierProgressPercent(tier: BoostTier, boostCount: number): number {
  if (tier >= 3) return 100;
  const current = TIER_PERKS[tier].requiredBoosts;
  const next = TIER_PERKS[tier + 1]?.requiredBoosts ?? current;
  if (next <= current) return 100;
  const progress = boostCount - current;
  const range = next - current;
  return Math.min(100, Math.max(0, Math.round((progress / range) * 100)));
}

// ---- Component ----

export default function ServerBoost(props: ServerBoostProps) {
  const auth = useAuth();
  const [boostStatus, setBoostStatus] = createSignal<BoostStatus | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isBoosting, setIsBoosting] = createSignal(false);
  const [showConfirm, setShowConfirm] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [boostSuccess, setBoostSuccess] = createSignal(false);

  const loadBoostStatus = async (serverId: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const raw = await api.get<BoostApiResponse>(`/api/v1/servers/${serverId}/boosts`);
      const currentUserId = auth.user?.id ?? '';
      const boosters = raw.boosters ?? [];
      const myBoosts = boosters.filter((b) => String(b.userId) === String(currentUserId));
      const mapped: BoostStatus = {
        serverId: String(raw.serverId),
        tier: (Math.min(3, Math.max(0, raw.boostLevel ?? 0)) as BoostTier),
        boostCount: raw.boostCount ?? 0,
        boostedByMe: myBoosts.length > 0,
        myBoostCount: myBoosts.length,
        premiumSubscriberCount: boosters.length,
      };
      setBoostStatus(mapped);
    } catch {
      setError('Failed to load boost status.');
      setBoostStatus(null);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const id = props.serverId;
    if (id) {
      loadBoostStatus(id);
    }
  });

  const handleBoost = async () => {
    setShowConfirm(false);
    setIsBoosting(true);
    setError(null);
    setBoostSuccess(false);
    try {
      await api.post(`/api/v1/servers/${props.serverId}/boosts`);
      // Reload status
      await loadBoostStatus(props.serverId);
      setBoostSuccess(true);
      setTimeout(() => setBoostSuccess(false), 3000);
    } catch {
      setError('Failed to boost server. Please try again.');
    } finally {
      setIsBoosting(false);
    }
  };

  const currentTier = () => boostStatus()?.tier ?? 0;
  const currentTierPerks = () => TIER_PERKS[currentTier()];
  const nextTierPerks = () => TIER_PERKS[currentTier() + 1] ?? null;
  const boostCount = () => boostStatus()?.boostCount ?? 0;
  const progressPercent = () => tierProgressPercent(currentTier(), boostCount());
  const boostsNeeded = () => getBoostsToNextTier(currentTier(), boostCount());
  const isMaxTier = () => currentTier() >= 3;

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Server Boost</h2>
        <p class="text-xcord-text-muted text-xs mt-0.5">
          Boost this server to unlock perks for everyone.
        </p>
      </div>

      {/* Loading */}
      <Show when={isLoading()}>
        <div class="flex items-center justify-center flex-1">
          <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      <Show when={error()}>
        <p class="text-red-400 text-xs px-4 py-2">{error()}</p>
      </Show>

      <Show when={!isLoading() && boostStatus() !== null}>
        <div class="flex-1 overflow-y-auto p-4 space-y-4">
          {/* Current tier banner */}
          <div class="bg-xcord-bg-primary rounded-lg p-4 text-center">
            <div class="text-4xl mb-2">
              {currentTier() === 0 ? '🔘' : currentTier() === 1 ? '🥉' : currentTier() === 2 ? '🥈' : '🥇'}
            </div>
            <h3 class="text-xcord-text-primary font-bold text-lg">
              {currentTierPerks().label}
            </h3>
            <p class="text-xcord-text-muted text-sm mt-1">
              {boostCount()} boost{boostCount() !== 1 ? 's' : ''} active
            </p>
            <p class="text-xcord-text-muted text-xs mt-0.5">
              {boostStatus()!.premiumSubscriberCount} booster{boostStatus()!.premiumSubscriberCount !== 1 ? 's' : ''}
            </p>
          </div>

          {/* Progress to next tier */}
          <Show when={!isMaxTier() && nextTierPerks()}>
            <div class="space-y-2">
              <div class="flex justify-between text-xs text-xcord-text-muted">
                <span>{currentTierPerks().label}</span>
                <span>{nextTierPerks()!.label}</span>
              </div>
              <div class="w-full bg-xcord-bg-tertiary rounded-full h-2">
                <div
                  class="bg-xcord-brand h-2 rounded-full transition-all"
                  style={{ width: `${progressPercent()}%` }}
                  aria-label={`${progressPercent()}% to next tier`}
                />
              </div>
              <p class="text-xcord-text-muted text-xs text-center">
                {boostsNeeded()} more boost{boostsNeeded() !== 1 ? 's' : ''} needed for {nextTierPerks()!.label}
              </p>
            </div>
          </Show>

          <Show when={isMaxTier()}>
            <p class="text-center text-xcord-brand text-sm font-medium">
              Maximum tier reached!
            </p>
          </Show>

          {/* Current perks */}
          <div class="space-y-2">
            <h4 class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide">
              Current Perks
            </h4>
            <ul class="space-y-1">
              <For each={currentTierPerks().perks}>
                {(perk) => (
                  <li class="flex items-center gap-2 text-xcord-text-primary text-sm">
                    <span class="text-green-400 flex-shrink-0">&#10003;</span>
                    {perk}
                  </li>
                )}
              </For>
            </ul>
          </div>

          {/* Next tier perks preview */}
          <Show when={nextTierPerks()}>
            <div class="space-y-2">
              <h4 class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide">
                {nextTierPerks()!.label} Perks
              </h4>
              <ul class="space-y-1 opacity-60">
                <For each={nextTierPerks()!.perks}>
                  {(perk) => (
                    <li class="flex items-center gap-2 text-xcord-text-muted text-sm">
                      <span class="text-xcord-text-muted flex-shrink-0">&#8226;</span>
                      {perk}
                    </li>
                  )}
                </For>
              </ul>
            </div>
          </Show>

          {/* All tiers overview */}
          <div class="space-y-2">
            <h4 class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide">
              All Tiers
            </h4>
            <For each={TIER_PERKS.slice(1)}>
              {(tier) => (
                <div
                  class={`rounded p-3 border ${
                    currentTier() >= tier.tier
                      ? 'border-xcord-brand bg-xcord-brand/10'
                      : 'border-xcord-bg-tertiary bg-xcord-bg-primary'
                  }`}
                >
                  <div class="flex items-center justify-between mb-1">
                    <span class="text-xcord-text-primary text-sm font-medium">{tier.label}</span>
                    <span class="text-xcord-text-muted text-xs">
                      {tier.requiredBoosts} boosts
                    </span>
                  </div>
                  <p class="text-xcord-text-muted text-xs">{tier.perks.join(' · ')}</p>
                </div>
              )}
            </For>
          </div>
        </div>

        {/* Boost button */}
        <div class="px-4 py-3 border-t border-xcord-bg-tertiary flex-shrink-0 space-y-2">
          <Show when={boostSuccess()}>
            <p class="text-green-400 text-sm text-center">Server boosted successfully!</p>
          </Show>

          <Show when={boostStatus()?.boostedByMe}>
            <p class="text-xcord-text-muted text-xs text-center">
              You are boosting this server ({boostStatus()!.myBoostCount} boost{boostStatus()!.myBoostCount !== 1 ? 's' : ''})
            </p>
          </Show>

          {/* Confirm dialog */}
          <Show when={showConfirm()}>
            <div class="bg-xcord-bg-primary rounded p-3 space-y-2 text-center">
              <p class="text-xcord-text-primary text-sm">
                Boost this server? This uses one of your available server boosts.
              </p>
              <div class="flex gap-2 justify-center">
                <button
                  class="px-4 py-1.5 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand/80 transition-colors"
                  onClick={handleBoost}
                  aria-label="Confirm boost"
                >
                  Confirm Boost
                </button>
                <button
                  class="px-4 py-1.5 bg-xcord-bg-tertiary text-xcord-text-muted rounded text-sm hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
                  onClick={() => setShowConfirm(false)}
                  aria-label="Cancel boost"
                >
                  Cancel
                </button>
              </div>
            </div>
          </Show>

          <Show when={!showConfirm()}>
            <button
              class="w-full bg-xcord-brand text-white py-2 rounded font-medium text-sm hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={() => setShowConfirm(true)}
              disabled={isBoosting()}
              aria-label="Boost Server"
            >
              {isBoosting() ? 'Boosting...' : 'Boost Server'}
            </button>
          </Show>
        </div>
      </Show>
    </div>
  );
}
