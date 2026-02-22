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
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  };

  return (
    <div class="bg-white rounded-lg shadow p-6">
      <h2 class="text-2xl font-bold mb-6">Instance Overview</h2>

      {error() && (
        <div class="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
          {error()}
        </div>
      )}

      <Show when={isLoading()} fallback={
        <Show when={stats()}>
          {(currentStats) => (
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              <div class="p-6 bg-blue-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Total Users</p>
                <p class="text-3xl font-bold text-blue-700">{currentStats().totalUsers.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-green-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Total Servers</p>
                <p class="text-3xl font-bold text-green-700">{currentStats().totalServers.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-purple-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Total Channels</p>
                <p class="text-3xl font-bold text-purple-700">{currentStats().totalChannels.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-yellow-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Total Messages</p>
                <p class="text-3xl font-bold text-yellow-700">{currentStats().totalMessages.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-indigo-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Active Users (24h)</p>
                <p class="text-3xl font-bold text-indigo-700">{currentStats().activeUsers24h.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-pink-50 rounded-lg">
                <p class="text-sm text-gray-600 mb-2">Active Users (7d)</p>
                <p class="text-3xl font-bold text-pink-700">{currentStats().activeUsers7d.toLocaleString()}</p>
              </div>

              <div class="p-6 bg-orange-50 rounded-lg col-span-full">
                <p class="text-sm text-gray-600 mb-2">Storage Used</p>
                <p class="text-3xl font-bold text-orange-700">{formatBytes(currentStats().storageUsedBytes)}</p>
              </div>
            </div>
          )}
        </Show>
      }>
        <p class="text-gray-500">Loading instance statistics...</p>
      </Show>

      <div class="mt-6">
        <button
          onClick={loadStats}
          class="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Refresh Stats
        </button>
      </div>
    </div>
  );
}
