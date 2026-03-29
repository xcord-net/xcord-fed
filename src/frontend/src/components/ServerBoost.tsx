import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import { useAuth } from '../stores/auth.store';
import styles from './ServerBoost.module.css';

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
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Server Boost</h2>
        <p class={styles.headerSubtitle}>
          Boost this server to unlock perks for everyone.
        </p>
      </div>

      {/* Loading */}
      <Show when={isLoading()}>
        <div class={styles.loadingCenter}>
          <div class={styles.spinner} />
        </div>
      </Show>

      <Show when={error()}>
        <p class={styles.errorText}>{error()}</p>
      </Show>

      <Show when={!isLoading() && boostStatus() !== null}>
        <div class={styles.scrollArea}>
          {/* Current tier banner */}
          <div class={styles.tierBanner}>
            <div class={styles.tierEmoji}>
              {currentTier() === 0 ? '🔘' : currentTier() === 1 ? '🥉' : currentTier() === 2 ? '🥈' : '🥇'}
            </div>
            <h3 class={styles.tierLabel}>
              {currentTierPerks().label}
            </h3>
            <p class={styles.boostCount}>
              {boostCount()} boost{boostCount() !== 1 ? 's' : ''} active
            </p>
            <p class={styles.boosterCount}>
              {boostStatus()!.premiumSubscriberCount} booster{boostStatus()!.premiumSubscriberCount !== 1 ? 's' : ''}
            </p>
          </div>

          {/* Progress to next tier */}
          <Show when={!isMaxTier() && nextTierPerks()}>
            <div class={styles.progressSection}>
              <div class={styles.progressLabels}>
                <span>{currentTierPerks().label}</span>
                <span>{nextTierPerks()!.label}</span>
              </div>
              <div class={styles.progressTrack}>
                <div
                  class={styles.progressFill}
                  style={{ width: `${progressPercent()}%` }}
                  aria-label={`${progressPercent()}% to next tier`}
                />
              </div>
              <p class={styles.progressHint}>
                {boostsNeeded()} more boost{boostsNeeded() !== 1 ? 's' : ''} needed for {nextTierPerks()!.label}
              </p>
            </div>
          </Show>

          <Show when={isMaxTier()}>
            <p class={styles.maxTierText}>
              Maximum tier reached!
            </p>
          </Show>

          {/* Current perks */}
          <div class={styles.progressSection}>
            <h4 class={styles.sectionHeading}>
              Current Perks
            </h4>
            <ul class={styles.perkList}>
              <For each={currentTierPerks().perks}>
                {(perk) => (
                  <li class={styles.perkItem}>
                    <span class={styles.perkCheck}>&#10003;</span>
                    {perk}
                  </li>
                )}
              </For>
            </ul>
          </div>

          {/* Next tier perks preview */}
          <Show when={nextTierPerks()}>
            <div class={styles.progressSection}>
              <h4 class={styles.sectionHeading}>
                {nextTierPerks()!.label} Perks
              </h4>
              <ul class={styles.nextPerkList}>
                <For each={nextTierPerks()!.perks}>
                  {(perk) => (
                    <li class={styles.nextPerkItem}>
                      <span class={styles.nextPerkBullet}>&#8226;</span>
                      {perk}
                    </li>
                  )}
                </For>
              </ul>
            </div>
          </Show>

          {/* All tiers overview */}
          <div class={styles.progressSection}>
            <h4 class={styles.sectionHeading}>
              All Tiers
            </h4>
            <For each={TIER_PERKS.slice(1)}>
              {(tier) => (
                <div
                  class={currentTier() >= tier.tier ? `${styles.tierCard} ${styles.tierCardActive}` : styles.tierCard}
                >
                  <div class={styles.tierCardHeader}>
                    <span class={styles.tierCardName}>{tier.label}</span>
                    <span class={styles.tierCardBoosts}>
                      {tier.requiredBoosts} boosts
                    </span>
                  </div>
                  <p class={styles.tierCardPerks}>{tier.perks.join(' · ')}</p>
                </div>
              )}
            </For>
          </div>
        </div>

        {/* Boost button */}
        <div class={styles.footer}>
          <Show when={boostSuccess()}>
            <p class={styles.successText}>Server boosted successfully!</p>
          </Show>

          <Show when={boostStatus()?.boostedByMe}>
            <p class={styles.boostedByText}>
              You are boosting this server ({boostStatus()!.myBoostCount} boost{boostStatus()!.myBoostCount !== 1 ? 's' : ''})
            </p>
          </Show>

          {/* Confirm dialog */}
          <Show when={showConfirm()}>
            <div class={styles.confirmDialog}>
              <p class={styles.confirmText}>
                Boost this server? This uses one of your available server boosts.
              </p>
              <div class={styles.confirmButtons}>
                <button
                  class={styles.confirmBoostBtn}
                  onClick={handleBoost}
                  aria-label="Confirm boost"
                >
                  Confirm Boost
                </button>
                <button
                  class={styles.confirmCancelBtn}
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
              class={styles.boostBtn}
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
