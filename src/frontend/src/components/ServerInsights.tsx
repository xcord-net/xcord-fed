import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './ServerInsights.module.css';

export type InsightsRange = '7d' | '30d' | '90d';

export interface DailyDataPoint {
  date: string;
  value: number;
}

export interface ChannelActivity {
  channelId: string;
  channelName: string;
  messageCount: number;
}

export interface ServerInsightsData {
  memberCount: number;
  memberGrowth: DailyDataPoint[];
  messageActivity: DailyDataPoint[];
  popularChannels: ChannelActivity[];
  totalMessages: number;
  newMembersInRange: number;
}

// Backend API response shape from GET /api/v1/servers/{id}/insights?days=N
interface InsightsApiDataPoint {
  date: string;
  totalMembers: number;
  newMembers: number;
  messageCount: number;
  activeMembers: number;
}

interface InsightsApiResponse {
  serverId: string;
  totalMembers: number;
  totalMessages: number;
  dataPoints: InsightsApiDataPoint[];
}

function rangeTodays(range: InsightsRange): number {
  switch (range) {
    case '7d': return 7;
    case '30d': return 30;
    case '90d': return 90;
  }
}

function mapInsightsResponse(raw: InsightsApiResponse): ServerInsightsData {
  const dataPoints = raw.dataPoints ?? [];
  const memberGrowth: DailyDataPoint[] = dataPoints.map((p) => ({
    date: p.date,
    value: p.totalMembers,
  }));
  const messageActivity: DailyDataPoint[] = dataPoints.map((p) => ({
    date: p.date,
    value: p.messageCount,
  }));
  const newMembersInRange = dataPoints.reduce((sum, p) => sum + p.newMembers, 0);
  return {
    memberCount: raw.totalMembers ?? 0,
    memberGrowth,
    messageActivity,
    popularChannels: [],
    totalMessages: raw.totalMessages ?? 0,
    newMembersInRange,
  };
}

interface ServerInsightsProps {
  serverId: string;
}

export function getMaxValue(points: DailyDataPoint[]): number {
  if (points.length === 0) return 1;
  return Math.max(...points.map((p) => p.value), 1);
}

export function formatInsightsDate(dateStr: string): string {
  const d = new Date(dateStr);
  return d.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}

export function computeBarHeightPct(value: number, maxValue: number): number {
  if (maxValue === 0) return 0;
  return Math.round((value / maxValue) * 100);
}

export default function ServerInsights(props: ServerInsightsProps) {
  const [range, setRange] = createSignal<InsightsRange>('30d');
  const [data, setData] = createSignal<ServerInsightsData | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  async function loadInsights() {
    setIsLoading(true);
    setError(null);
    try {
      const days = rangeTodays(range());
      const raw = await api.get<InsightsApiResponse>(
        `/api/v1/servers/${props.serverId}/insights?days=${days}`,
      );
      setData(mapInsightsResponse(raw));
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load insights'));
    } finally {
      setIsLoading(false);
    }
  }

  onMount(() => {
    loadInsights();
  });

  const ranges: InsightsRange[] = ['7d', '30d', '90d'];

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Server Insights</h2>
        <div class={styles.rangeButtons}>
          <For each={ranges}>
            {(r) => (
              <button
                class={`${styles.rangeButton} ${range() === r ? styles.rangeButtonActive : styles.rangeButtonInactive}`}
                onClick={() => {
                  setRange(r);
                  loadInsights();
                }}
              >
                {r}
              </button>
            )}
          </For>
        </div>
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <Show when={isLoading()}>
        <div class={styles.loadingCenter}>
          <p class={styles.loadingText}>Loading insights...</p>
        </div>
      </Show>

      <Show when={!isLoading() && data() !== null}>
        <div class={styles.content}>
          {/* Summary stats */}
          <div class={styles.statsGrid}>
            <div class={styles.statCard}>
              <p class={styles.statValue}>{data()!.memberCount.toLocaleString()}</p>
              <p class={styles.statLabel}>Total Members</p>
            </div>
            <div class={styles.statCard}>
              <p class={styles.statValueBrand}>
                +{data()!.newMembersInRange.toLocaleString()}
              </p>
              <p class={styles.statLabel}>New Members</p>
            </div>
            <div class={styles.statCard}>
              <p class={styles.statValue}>{data()!.totalMessages.toLocaleString()}</p>
              <p class={styles.statLabel}>Messages Sent</p>
            </div>
          </div>

          {/* Member Growth Chart */}
          <div class={styles.chartCard}>
            <h3 class={styles.chartTitle}>Member Growth</h3>
            <div class={styles.barChart}>
              <For each={data()!.memberGrowth}>
                {(point) => {
                  const maxVal = getMaxValue(data()!.memberGrowth);
                  const heightPct = computeBarHeightPct(point.value, maxVal);
                  return (
                    <div
                      class={styles.barColumn}
                      title={`${formatInsightsDate(point.date)}: ${point.value}`}
                    >
                      <div
                        class={styles.barBrand}
                        style={`height: ${heightPct}%`}
                      />
                    </div>
                  );
                }}
              </For>
            </div>
            <div class={styles.chartAxisRow}>
              <span class={styles.chartAxisLabel}>
                {data()!.memberGrowth.length > 0
                  ? formatInsightsDate(data()!.memberGrowth[0].date)
                  : ''}
              </span>
              <span class={styles.chartAxisLabel}>
                {data()!.memberGrowth.length > 0
                  ? formatInsightsDate(data()!.memberGrowth[data()!.memberGrowth.length - 1].date)
                  : ''}
              </span>
            </div>
          </div>

          {/* Message Activity Chart */}
          <div class={styles.chartCard}>
            <h3 class={styles.chartTitle}>Message Activity</h3>
            <div class={styles.barChart}>
              <For each={data()!.messageActivity}>
                {(point) => {
                  const maxVal = getMaxValue(data()!.messageActivity);
                  const heightPct = computeBarHeightPct(point.value, maxVal);
                  return (
                    <div
                      class={styles.barColumn}
                      title={`${formatInsightsDate(point.date)}: ${point.value}`}
                    >
                      <div
                        class={styles.barGreen}
                        style={`height: ${heightPct}%`}
                      />
                    </div>
                  );
                }}
              </For>
            </div>
            <div class={styles.chartAxisRow}>
              <span class={styles.chartAxisLabel}>
                {data()!.messageActivity.length > 0
                  ? formatInsightsDate(data()!.messageActivity[0].date)
                  : ''}
              </span>
              <span class={styles.chartAxisLabel}>
                {data()!.messageActivity.length > 0
                  ? formatInsightsDate(
                      data()!.messageActivity[data()!.messageActivity.length - 1].date,
                    )
                  : ''}
              </span>
            </div>
          </div>

          {/* Popular Channels */}
          <div class={styles.channelsCard}>
            <h3 class={styles.channelsTitle}>Most Active Channels</h3>
            <Show
              when={data()!.popularChannels.length > 0}
              fallback={
                <p class={styles.channelsEmpty}>No channel activity in this period.</p>
              }
            >
              <div class={styles.channelList}>
                <For each={data()!.popularChannels}>
                  {(channel) => {
                    const maxMsgs = Math.max(
                      ...data()!.popularChannels.map((c) => c.messageCount),
                      1,
                    );
                    const pct = computeBarHeightPct(channel.messageCount, maxMsgs);
                    return (
                      <div class={styles.channelRow}>
                        <span class={styles.channelName}>
                          # {channel.channelName}
                        </span>
                        <div class={styles.channelBarTrack}>
                          <div
                            class={styles.channelBarFill}
                            style={`width: ${pct}%`}
                          />
                        </div>
                        <span class={styles.channelCount}>
                          {channel.messageCount}
                        </span>
                      </div>
                    );
                  }}
                </For>
              </div>
            </Show>
          </div>
        </div>
      </Show>

      <Show when={!isLoading() && data() === null && !error()}>
        <div class={styles.emptyState}>
          <p>No insights data available.</p>
        </div>
      </Show>
    </div>
  );
}
