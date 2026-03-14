import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

export type WebhookEvent =
  | 'message.created'
  | 'message.deleted'
  | 'member.joined'
  | 'member.left'
  | 'channel.created'
  | 'channel.deleted'
  | 'group.updated';

export const WEBHOOK_EVENT_LABELS: Record<WebhookEvent, string> = {
  'message.created': 'Message Created',
  'message.deleted': 'Message Deleted',
  'member.joined': 'Member Joined',
  'member.left': 'Member Left',
  'channel.created': 'Channel Created',
  'channel.deleted': 'Channel Deleted',
  'group.updated': 'Group Updated',
};

export const ALL_WEBHOOK_EVENTS: WebhookEvent[] = [
  'message.created',
  'message.deleted',
  'member.joined',
  'member.left',
  'channel.created',
  'channel.deleted',
  'group.updated',
];

export interface OutgoingWebhook {
  id: string;
  serverId: string;
  name: string;
  targetUrl: string;
  events: WebhookEvent[];
  secret: string;
  enabled: boolean;
  createdAt: string;
}

interface WebhookManagerProps {
  serverId: string;
}

export function validateWebhookUrl(url: string): string | null {
  const trimmed = url.trim();
  if (!trimmed) return 'Target URL is required.';
  try {
    const parsed = new URL(trimmed);
    if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
      return 'URL must use http or https.';
    }
  } catch {
    return 'Invalid URL format.';
  }
  return null;
}

export function validateWebhookName(name: string): string | null {
  const trimmed = name.trim();
  if (!trimmed) return 'Webhook name is required.';
  if (trimmed.length > 64) return 'Name must be 64 characters or fewer.';
  return null;
}

export function toggleEvent(events: WebhookEvent[], event: WebhookEvent): WebhookEvent[] {
  if (events.includes(event)) {
    return events.filter((e) => e !== event);
  }
  return [...events, event];
}

export default function WebhookManager(props: WebhookManagerProps) {
  const [webhooks, setWebhooks] = createSignal<OutgoingWebhook[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [showForm, setShowForm] = createSignal(false);
  const [revealedSecret, setRevealedSecret] = createSignal<string | null>(null);

  // Form state
  const [formName, setFormName] = createSignal('');
  const [formUrl, setFormUrl] = createSignal('');
  const [formEvents, setFormEvents] = createSignal<WebhookEvent[]>([]);
  const [formError, setFormError] = createSignal<string | null>(null);

  async function loadWebhooks() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<OutgoingWebhook[]>(
        `/api/v1/servers/${props.serverId}/webhooks/outgoing`,
      );
      setWebhooks(result);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load webhooks');
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreate(e: Event) {
    e.preventDefault();
    setFormError(null);

    const nameErr = validateWebhookName(formName());
    if (nameErr) { setFormError(nameErr); return; }

    const urlErr = validateWebhookUrl(formUrl());
    if (urlErr) { setFormError(urlErr); return; }

    if (formEvents().length === 0) {
      setFormError('Select at least one event to subscribe to.');
      return;
    }

    setIsSaving(true);
    try {
      const created = await api.post<OutgoingWebhook>(
        `/api/v1/servers/${props.serverId}/webhooks/outgoing`,
        { name: formName().trim(), targetUrl: formUrl().trim(), events: formEvents() },
      );
      setWebhooks([...webhooks(), created]);
      setShowForm(false);
      setFormName('');
      setFormUrl('');
      setFormEvents([]);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setFormError(e?.error ?? 'Failed to create webhook');
    } finally {
      setIsSaving(false);
    }
  }

  async function handleToggleEnabled(webhook: OutgoingWebhook) {
    try {
      const updated = await api.put<OutgoingWebhook>(
        `/api/v1/servers/${props.serverId}/webhooks/outgoing/${webhook.id}`,
        { enabled: !webhook.enabled },
      );
      setWebhooks(webhooks().map((w) => (w.id === webhook.id ? updated : w)));
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to update webhook');
    }
  }

  async function handleDelete(webhookId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/webhooks/outgoing/${webhookId}`);
      setWebhooks(webhooks().filter((w) => w.id !== webhookId));
      if (revealedSecret() === webhookId) setRevealedSecret(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to delete webhook');
    }
  }

  onMount(() => {
    loadWebhooks();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">Outgoing Webhooks</h2>
        <button
          class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover transition-colors"
          onClick={() => setShowForm(!showForm())}
        >
          {showForm() ? 'Cancel' : 'Add Webhook'}
        </button>
      </div>

      {/* Create form */}
      <Show when={showForm()}>
        <div class="px-4 py-4 border-b border-xcord-border bg-xcord-bg-primary/30">
          <h3 class="text-white text-sm font-semibold mb-3">New Webhook</h3>
          <form onSubmit={handleCreate} class="space-y-3">
            <input
              type="text"
              placeholder="Webhook name"
              value={formName()}
              onInput={(e) => setFormName(e.currentTarget.value)}
              maxLength={64}
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <input
              type="url"
              placeholder="Target URL (https://...)"
              value={formUrl()}
              onInput={(e) => setFormUrl(e.currentTarget.value)}
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <div>
              <p class="text-xcord-text-muted text-xs mb-2">Events to subscribe:</p>
              <div class="grid grid-cols-2 gap-1">
                <For each={ALL_WEBHOOK_EVENTS}>
                  {(evt) => (
                    <label class="flex items-center space-x-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formEvents().includes(evt)}
                        onChange={() => setFormEvents(toggleEvent(formEvents(), evt))}
                        class="accent-xcord-brand"
                      />
                      <span class="text-xcord-text-muted text-xs">{WEBHOOK_EVENT_LABELS[evt]}</span>
                    </label>
                  )}
                </For>
              </div>
            </div>
            <Show when={formError()}>
              <p class="text-red-400 text-xs">{formError()}</p>
            </Show>
            <button
              type="submit"
              disabled={isSaving()}
              class="bg-xcord-brand text-white px-4 py-2 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50 transition-colors"
            >
              {isSaving() ? 'Creating...' : 'Create Webhook'}
            </button>
          </form>
        </div>
      </Show>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      {/* Webhook list */}
      <div class="flex-1 overflow-y-auto p-4 space-y-3">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-24">
            <p class="text-xcord-text-muted">Loading webhooks...</p>
          </div>
        </Show>

        <Show when={!isLoading() && webhooks().length === 0}>
          <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
            <p class="font-semibold">No outgoing webhooks</p>
            <p class="text-sm mt-1">Create a webhook to receive server events at an external URL.</p>
          </div>
        </Show>

        <For each={webhooks()}>
          {(webhook) => (
            <div class="bg-xcord-bg-primary rounded-lg p-4 space-y-3">
              <div class="flex items-start justify-between">
                <div class="flex-1 min-w-0">
                  <div class="flex items-center space-x-2">
                    <p class="text-white font-medium text-sm truncate">{webhook.name}</p>
                    <span
                      class={`text-xs px-2 py-0.5 rounded-full ${
                        webhook.enabled
                          ? 'bg-green-500/20 text-green-400'
                          : 'bg-xcord-bg-tertiary text-xcord-text-muted'
                      }`}
                    >
                      {webhook.enabled ? 'Active' : 'Disabled'}
                    </span>
                  </div>
                  <p class="text-xcord-text-muted text-xs mt-1 truncate">{webhook.targetUrl}</p>
                  <div class="flex flex-wrap gap-1 mt-2">
                    <For each={webhook.events}>
                      {(evt) => (
                        <span class="text-xs bg-xcord-bg-tertiary text-xcord-text-muted px-2 py-0.5 rounded">
                          {WEBHOOK_EVENT_LABELS[evt] ?? evt}
                        </span>
                      )}
                    </For>
                  </div>
                </div>
                <div class="flex items-center space-x-2 ml-3 flex-shrink-0">
                  <button
                    class={`text-xs px-2 py-1 rounded transition-colors ${
                      webhook.enabled
                        ? 'bg-xcord-bg-tertiary text-xcord-text-muted hover:text-white'
                        : 'bg-green-500/20 text-green-400 hover:bg-green-500/30'
                    }`}
                    onClick={() => handleToggleEnabled(webhook)}
                    title={webhook.enabled ? 'Disable webhook' : 'Enable webhook'}
                  >
                    {webhook.enabled ? 'Disable' : 'Enable'}
                  </button>
                  <button
                    class="text-xs px-2 py-1 rounded bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors"
                    onClick={() => handleDelete(webhook.id)}
                    title="Delete webhook"
                  >
                    Delete
                  </button>
                </div>
              </div>

              {/* Secret key */}
              <div class="flex items-center space-x-2">
                <span class="text-xcord-text-muted text-xs">Secret:</span>
                <Show
                  when={revealedSecret() === webhook.id}
                  fallback={
                    <button
                      class="text-xs text-xcord-brand hover:underline"
                      onClick={() => setRevealedSecret(webhook.id)}
                    >
                      Click to reveal
                    </button>
                  }
                >
                  <code class="text-xs bg-xcord-bg-tertiary text-green-400 px-2 py-0.5 rounded font-mono break-all">
                    {webhook.secret}
                  </code>
                  <button
                    class="text-xs text-xcord-text-muted hover:text-white ml-1"
                    onClick={() => setRevealedSecret(null)}
                  >
                    Hide
                  </button>
                </Show>
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
