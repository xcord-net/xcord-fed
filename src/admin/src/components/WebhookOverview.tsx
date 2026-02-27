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
      const response = await api.get<Server[]>('/api/v1/users/@me/servers');
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
    <div>
      <h2 class="text-xl font-bold text-white mb-6">Webhooks</h2>

      {error() && (
        <div class="bg-xcord-danger/10 border border-xcord-danger/30 text-xcord-danger px-4 py-3 rounded text-sm mb-4">
          {error()}
        </div>
      )}

      <Show when={!isLoading()} fallback={
        <div class="text-xcord-text-muted text-sm">Loading servers...</div>
      }>
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Server list */}
          <div>
            <h3 class="text-sm font-semibold uppercase text-xcord-text-muted mb-3">Servers</h3>
            <Show when={servers().length > 0} fallback={
              <p class="text-xcord-text-muted text-sm">No servers found</p>
            }>
              <div class="space-y-2">
                <For each={servers()}>
                  {(server) => (
                    <button
                      onClick={() => setSelectedServerId(server.id)}
                      class={`w-full text-left p-4 rounded-lg border transition-colors ${
                        selectedServerId() === server.id
                          ? 'border-xcord-brand bg-xcord-brand/10'
                          : 'border-xcord-border bg-xcord-bg-secondary hover:border-xcord-bg-input'
                      }`}
                    >
                      <p class="font-medium text-white">{server.name}</p>
                      <p class="text-xs text-xcord-text-muted">ID: {server.id}</p>
                    </button>
                  )}
                </For>
              </div>
            </Show>
          </div>

          {/* Webhook list for selected server */}
          <div>
            <Show when={selectedServerId()}>
              <h3 class="text-sm font-semibold uppercase text-xcord-text-muted mb-3">Webhooks</h3>

              <Show when={isLoadingWebhooks()}>
                <p class="text-xcord-text-muted text-sm">Loading webhooks...</p>
              </Show>

              <Show when={!isLoadingWebhooks() && webhooks().length === 0}>
                <p class="text-xcord-text-muted text-sm">No webhooks for this server</p>
              </Show>

              <Show when={!isLoadingWebhooks() && webhooks().length > 0}>
                <div class="space-y-2">
                  <For each={webhooks()}>
                    {(webhook) => (
                      <div class="p-4 bg-xcord-bg-secondary rounded-lg border border-xcord-border">
                        <div class="flex items-start gap-3">
                          <div class="w-10 h-10 rounded-full bg-xcord-bg-input flex items-center justify-center text-xcord-text-secondary font-bold text-sm">
                            {webhook.name.charAt(0).toUpperCase()}
                          </div>
                          <div class="flex-1">
                            <p class="font-medium text-white">{webhook.name}</p>
                            <p class="text-xs text-xcord-text-muted">ID: {webhook.id}</p>
                            <p class="text-xs text-xcord-text-muted">Channel: {webhook.channelId}</p>
                            <p class="text-xs text-xcord-text-muted">Created: {new Date(webhook.createdAt).toLocaleString()}</p>
                          </div>
                        </div>
                      </div>
                    )}
                  </For>
                </div>
              </Show>
            </Show>

            <Show when={!selectedServerId()}>
              <p class="text-xcord-text-muted text-sm">Select a server to view its webhooks</p>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
}
