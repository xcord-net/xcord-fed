import { createSignal, createEffect, Show } from 'solid-js';
import { api } from '../api/client';
import type { InstanceStats } from '../types/stats';

export function InstanceOverview() {
  const [stats, setStats] = createSignal<InstanceStats | null>(null);
  const [isLoading, setIsLoading] = createSignal(true);
  const [error, setError] = createSignal('');

  createEffect(() => {
    loadStats();
  });

  const loadStats = async () => {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.get<InstanceStats>('/api/v1/admin/stats');
      setStats(response);
    } catch (err: any) {
      setError(err?.message || 'Failed to load instance stats');
    } finally {
      setIsLoading(false);
    }
  };

  const formatBytes = (bytes: number): string => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  };

  const StatCard = (props: { label: string; value: string; accent: string }) => (
    <div class="bg-xcord-bg-secondary rounded-lg p-5 border border-xcord-border">
      <p class="text-xs uppercase font-semibold text-xcord-text-muted mb-2">{props.label}</p>
      <p class={`text-3xl font-bold ${props.accent}`}>{props.value}</p>
    </div>
  );

  return (
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold text-white">Instance Overview</h2>
        <button
          onClick={loadStats}
          class="px-4 py-2 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand-hover transition-colors"
        >
          Refresh
        </button>
      </div>

      {error() && (
        <div class="bg-xcord-danger/10 border border-xcord-danger/30 text-xcord-danger px-4 py-3 rounded text-sm mb-4">
          {error()}
        </div>
      )}

      <Show when={!isLoading()} fallback={
        <div class="text-xcord-text-muted">Loading instance statistics...</div>
      }>
        <Show when={stats()}>
          {(currentStats) => (
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <StatCard
                label="Total Users"
                value={currentStats().totalUsers.toLocaleString()}
                accent="text-xcord-brand"
              />
              <StatCard
                label="Total Servers"
                value={currentStats().totalServers.toLocaleString()}
                accent="text-xcord-success"
              />
              <StatCard
                label="Total Channels"
                value={currentStats().totalChannels.toLocaleString()}
                accent="text-purple-400"
              />
              <StatCard
                label="Total Messages"
                value={currentStats().totalMessages.toLocaleString()}
                accent="text-xcord-warning"
              />
              <StatCard
                label="Active Users (24h)"
                value={currentStats().activeUsers24h.toLocaleString()}
                accent="text-cyan-400"
              />
              <StatCard
                label="Active Users (7d)"
                value={currentStats().activeUsers7d.toLocaleString()}
                accent="text-pink-400"
              />
              <StatCard
                label="Total Bots"
                value={currentStats().totalBots.toLocaleString()}
                accent="text-xcord-text-primary"
              />
              <StatCard
                label="Total Bans"
                value={currentStats().totalBans.toLocaleString()}
                accent="text-xcord-danger"
              />
              <StatCard
                label="Storage Used"
                value={formatBytes(currentStats().storageUsedBytes)}
                accent="text-orange-400"
              />
            </div>
          )}
        </Show>
      </Show>
    </div>
  );
}
