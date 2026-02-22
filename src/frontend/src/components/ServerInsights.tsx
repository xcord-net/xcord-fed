import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

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
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load insights');
    } finally {
      setIsLoading(false);
    }
  }

  onMount(() => {
    loadInsights();
  });

  const ranges: InsightsRange[] = ['7d', '30d', '90d'];

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">Server Insights</h2>
        <div class="flex items-center space-x-1">
          <For each={ranges}>
            {(r) => (
              <button
                class={`px-3 py-1 rounded text-sm transition-colors ${
                  range() === r
                    ? 'bg-xcord-brand text-white'
                    : 'bg-xcord-bg-tertiary text-xcord-text-muted hover:text-white'
                }`}
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
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <Show when={isLoading()}>
        <div class="flex items-center justify-center flex-1">
          <p class="text-xcord-text-muted">Loading insights...</p>
        </div>
      </Show>

      <Show when={!isLoading() && data() !== null}>
        <div class="flex-1 overflow-y-auto p-4 space-y-6">
          {/* Summary stats */}
          <div class="grid grid-cols-3 gap-3">
            <div class="bg-xcord-bg-primary rounded-lg p-3 text-center">
              <p class="text-2xl font-bold text-white">{data()!.memberCount.toLocaleString()}</p>
              <p class="text-xcord-text-muted text-xs mt-1">Total Members</p>
            </div>
            <div class="bg-xcord-bg-primary rounded-lg p-3 text-center">
              <p class="text-2xl font-bold text-xcord-brand">
                +{data()!.newMembersInRange.toLocaleString()}
              </p>
              <p class="text-xcord-text-muted text-xs mt-1">New Members</p>
            </div>
            <div class="bg-xcord-bg-primary rounded-lg p-3 text-center">
              <p class="text-2xl font-bold text-white">{data()!.totalMessages.toLocaleString()}</p>
              <p class="text-xcord-text-muted text-xs mt-1">Messages Sent</p>
            </div>
          </div>

          {/* Member Growth Chart */}
          <div class="bg-xcord-bg-primary rounded-lg p-4">
            <h3 class="text-white text-sm font-semibold mb-3">Member Growth</h3>
            <div class="flex items-end space-x-1 h-24">
              <For each={data()!.memberGrowth}>
                {(point) => {
                  const maxVal = getMaxValue(data()!.memberGrowth);
                  const heightPct = computeBarHeightPct(point.value, maxVal);
                  return (
                    <div
                      class="flex-1 flex flex-col items-center justify-end group"
                      title={`${formatInsightsDate(point.date)}: ${point.value}`}
                    >
                      <div
                        class="w-full bg-xcord-brand/70 rounded-sm hover:bg-xcord-brand transition-colors"
                        style={`height: ${heightPct}%`}
                      />
                    </div>
                  );
                }}
              </For>
            </div>
            <div class="flex justify-between mt-1">
              <span class="text-xcord-text-muted text-xs">
                {data()!.memberGrowth.length > 0
                  ? formatInsightsDate(data()!.memberGrowth[0].date)
                  : ''}
              </span>
              <span class="text-xcord-text-muted text-xs">
                {data()!.memberGrowth.length > 0
                  ? formatInsightsDate(data()!.memberGrowth[data()!.memberGrowth.length - 1].date)
                  : ''}
              </span>
            </div>
          </div>

          {/* Message Activity Chart */}
          <div class="bg-xcord-bg-primary rounded-lg p-4">
            <h3 class="text-white text-sm font-semibold mb-3">Message Activity</h3>
            <div class="flex items-end space-x-1 h-24">
              <For each={data()!.messageActivity}>
                {(point) => {
                  const maxVal = getMaxValue(data()!.messageActivity);
                  const heightPct = computeBarHeightPct(point.value, maxVal);
                  return (
                    <div
                      class="flex-1 flex flex-col items-center justify-end group"
                      title={`${formatInsightsDate(point.date)}: ${point.value}`}
                    >
                      <div
                        class="w-full bg-green-500/70 rounded-sm hover:bg-green-500 transition-colors"
                        style={`height: ${heightPct}%`}
                      />
                    </div>
                  );
                }}
              </For>
            </div>
            <div class="flex justify-between mt-1">
              <span class="text-xcord-text-muted text-xs">
                {data()!.messageActivity.length > 0
                  ? formatInsightsDate(data()!.messageActivity[0].date)
                  : ''}
              </span>
              <span class="text-xcord-text-muted text-xs">
                {data()!.messageActivity.length > 0
                  ? formatInsightsDate(
                      data()!.messageActivity[data()!.messageActivity.length - 1].date,
                    )
                  : ''}
              </span>
            </div>
          </div>

          {/* Popular Channels */}
          <div class="bg-xcord-bg-primary rounded-lg p-4">
            <h3 class="text-white text-sm font-semibold mb-3">Most Active Channels</h3>
            <Show
              when={data()!.popularChannels.length > 0}
              fallback={
                <p class="text-xcord-text-muted text-sm">No channel activity in this period.</p>
              }
            >
              <div class="space-y-2">
                <For each={data()!.popularChannels}>
                  {(channel) => {
                    const maxMsgs = Math.max(
                      ...data()!.popularChannels.map((c) => c.messageCount),
                      1,
                    );
                    const pct = computeBarHeightPct(channel.messageCount, maxMsgs);
                    return (
                      <div class="flex items-center space-x-3">
                        <span class="text-xcord-text-muted text-sm w-32 truncate">
                          # {channel.channelName}
                        </span>
                        <div class="flex-1 bg-xcord-bg-tertiary rounded-full h-2">
                          <div
                            class="bg-xcord-brand h-2 rounded-full transition-all"
                            style={`width: ${pct}%`}
                          />
                        </div>
                        <span class="text-xcord-text-muted text-xs w-10 text-right">
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
        <div class="flex flex-col items-center justify-center flex-1 text-xcord-text-muted">
          <p>No insights data available.</p>
        </div>
      </Show>
    </div>
  );
}
