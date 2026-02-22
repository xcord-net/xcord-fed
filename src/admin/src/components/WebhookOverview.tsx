import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';
import type { Webhook } from '../types/webhook';

interface Server {
  id: string;
  name: string;
}

export function WebhookOverview() {
  const [servers, setServers] = createSignal<Server[]>([]);
  const [selectedServerId, setSelectedServerId] = createSignal<string | null>(null);
  const [webhooks, setWebhooks] = createSignal<Webhook[]>([]);
  const [isLoading, setIsLoading] = createSignal(true);
  const [isLoadingWebhooks, setIsLoadingWebhooks] = createSignal(false);
  const [error, setError] = createSignal('');

  createEffect(() => {
    loadServers();
  });

  createEffect(() => {
    const serverId = selectedServerId();
    if (serverId) {
      loadWebhooks(serverId);
    }
  });

  const loadServers = async () => {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.get<Server[]>('/api/v1/servers');
      setServers(response);
    } catch (err: any) {
      setError(err?.message || 'Failed to load servers');
    } finally {
      setIsLoading(false);
    }
  };

  const loadWebhooks = async (serverId: string) => {
    setIsLoadingWebhooks(true);
    setError('');
    try {
      const response = await api.get<Webhook[]>(`/api/v1/servers/${serverId}/webhooks`);
      setWebhooks(response);
    } catch (err: any) {
      setError(err?.message || 'Failed to load webhooks');
    } finally {
      setIsLoadingWebhooks(false);
    }
  };

  return (
    <div class="bg-white rounded-lg shadow p-6">
      <h2 class="text-2xl font-bold mb-6">Webhook Overview</h2>

      {error() && (
        <div class="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
          {error()}
        </div>
      )}

      <Show when={isLoading()} fallback={
        <>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <h3 class="text-lg font-semibold mb-4">Servers</h3>
              <Show when={servers().length === 0} fallback={
                <div class="space-y-2">
                  <For each={servers()}>
                    {(server) => (
                      <button
                        onClick={() => setSelectedServerId(server.id)}
                        class={`w-full text-left p-4 rounded border ${
                          selectedServerId() === server.id
                            ? 'border-blue-500 bg-blue-50'
                            : 'border-gray-200 hover:bg-gray-50'
                        }`}
                      >
                        <p class="font-medium">{server.name}</p>
                        <p class="text-xs text-gray-500">ID: {server.id}</p>
                      </button>
                    )}
                  </For>
                </div>
              }>
                <p class="text-gray-500">No servers found</p>
              </Show>
            </div>

            <div>
              <Show when={selectedServerId()}>
                <h3 class="text-lg font-semibold mb-4">Webhooks</h3>

                <Show when={isLoadingWebhooks()}>
                  <p class="text-gray-500">Loading webhooks...</p>
                </Show>

                <Show when={!isLoadingWebhooks() && webhooks().length === 0}>
                  <p class="text-gray-500">No webhooks found for this server</p>
                </Show>

                <Show when={!isLoadingWebhooks() && webhooks().length > 0}>
                  <div class="space-y-3">
                    <For each={webhooks()}>
                      {(webhook) => (
                        <div class="p-4 border border-gray-200 rounded">
                          <div class="flex items-start gap-3">
                            <div class="w-10 h-10 rounded-full bg-gray-300 flex items-center justify-center text-white font-bold">
                              {webhook.name.charAt(0)}
                            </div>
                            <div class="flex-1">
                              <p class="font-medium">{webhook.name}</p>
                              <p class="text-xs text-gray-500">ID: {webhook.id}</p>
                              <p class="text-xs text-gray-500">Channel ID: {webhook.channelId}</p>
                              <p class="text-xs text-gray-500">Created: {new Date(webhook.createdAt).toLocaleDateString()}</p>
                            </div>
                          </div>
                        </div>
                      )}
                    </For>
                  </div>
                </Show>
              </Show>
            </div>
          </div>
        </>
      }>
        <p class="text-gray-500">Loading servers...</p>
      </Show>
    </div>
  );
}
