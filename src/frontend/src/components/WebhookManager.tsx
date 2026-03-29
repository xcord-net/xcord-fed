import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import ConfirmationButton from './ui/ConfirmationButton';
import styles from './WebhookManager.module.css';

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
  const [confirmingDelete, setConfirmingDelete] = createSignal<string | null>(null);

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
      setError(getErrorMessage(err, 'Failed to load webhooks'));
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
      setFormError(getErrorMessage(err, 'Failed to create webhook'));
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
      setError(getErrorMessage(err, 'Failed to update webhook'));
    }
  }

  async function handleDelete(webhookId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/webhooks/outgoing/${webhookId}`);
      setWebhooks(webhooks().filter((w) => w.id !== webhookId));
      if (revealedSecret() === webhookId) setRevealedSecret(null);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to delete webhook'));
    }
  }

  onMount(() => {
    loadWebhooks();
  });

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Outgoing Webhooks</h2>
        <button
          class={styles.addButton}
          onClick={() => setShowForm(!showForm())}
        >
          {showForm() ? 'Cancel' : 'Add Webhook'}
        </button>
      </div>

      {/* Create form */}
      <Show when={showForm()}>
        <div class={styles.createFormSection}>
          <h3 class={styles.createFormTitle}>New Webhook</h3>
          <form onSubmit={handleCreate} class={styles.createFormFields}>
            <input
              type="text"
              placeholder="Webhook name"
              value={formName()}
              onInput={(e) => setFormName(e.currentTarget.value)}
              maxLength={64}
              class={styles.textInput}
            />
            <input
              type="url"
              placeholder="Target URL (https://...)"
              value={formUrl()}
              onInput={(e) => setFormUrl(e.currentTarget.value)}
              class={styles.textInput}
            />
            <div>
              <p class={styles.eventsLabel}>Events to subscribe:</p>
              <div class={styles.eventsGrid}>
                <For each={ALL_WEBHOOK_EVENTS}>
                  {(evt) => (
                    <label class={styles.eventCheckLabel}>
                      <input
                        type="checkbox"
                        checked={formEvents().includes(evt)}
                        onChange={() => setFormEvents(toggleEvent(formEvents(), evt))}
                        class={styles.eventCheckInput}
                      />
                      <span class={styles.eventCheckText}>{WEBHOOK_EVENT_LABELS[evt]}</span>
                    </label>
                  )}
                </For>
              </div>
            </div>
            <Show when={formError()}>
              <p class={styles.formError}>{formError()}</p>
            </Show>
            <button
              type="submit"
              disabled={isSaving()}
              class={styles.createSubmitButton}
            >
              {isSaving() ? 'Creating...' : 'Create Webhook'}
            </button>
          </form>
        </div>
      </Show>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      {/* Webhook list */}
      <div class={styles.webhookList}>
        <Show when={isLoading()}>
          <div class={styles.loadingContainer}>
            <p class={styles.mutedText}>Loading webhooks...</p>
          </div>
        </Show>

        <Show when={!isLoading() && webhooks().length === 0}>
          <div class={styles.emptyContainer}>
            <p class={styles.emptyTitle}>No outgoing webhooks</p>
            <p class={styles.emptySubtitle}>Create a webhook to receive server events at an external URL.</p>
          </div>
        </Show>

        <For each={webhooks()}>
          {(webhook) => (
            <div class={styles.webhookCard}>
              <div class={styles.webhookHeader}>
                <div class={styles.webhookInfo}>
                  <div class={styles.webhookNameRow}>
                    <p class={styles.webhookName}>{webhook.name}</p>
                    <span
                      class={webhook.enabled ? styles.statusBadgeActive : styles.statusBadgeDisabled}
                    >
                      {webhook.enabled ? 'Active' : 'Disabled'}
                    </span>
                  </div>
                  <p class={styles.webhookUrl}>{webhook.targetUrl}</p>
                  <div class={styles.webhookEvents}>
                    <For each={webhook.events}>
                      {(evt) => (
                        <span class={styles.eventTag}>
                          {WEBHOOK_EVENT_LABELS[evt] ?? evt}
                        </span>
                      )}
                    </For>
                  </div>
                </div>
                <div class={styles.webhookButtons}>
                  <button
                    class={webhook.enabled ? styles.toggleButtonEnabled : styles.toggleButtonDisabled}
                    onClick={() => handleToggleEnabled(webhook)}
                    title={webhook.enabled ? 'Disable webhook' : 'Enable webhook'}
                  >
                    {webhook.enabled ? 'Disable' : 'Enable'}
                  </button>
                  <ConfirmationButton
                    isConfirming={confirmingDelete() === webhook.id}
                    onStartConfirm={() => setConfirmingDelete(webhook.id)}
                    onConfirm={() => { handleDelete(webhook.id); setConfirmingDelete(null); }}
                    onCancel={() => setConfirmingDelete(null)}
                    label="Delete"
                  />
                </div>
              </div>

              {/* Secret key */}
              <div class={styles.secretRow}>
                <span class={styles.secretLabel}>Secret:</span>
                <Show
                  when={revealedSecret() === webhook.id}
                  fallback={
                    <button
                      class={styles.revealButton}
                      onClick={() => setRevealedSecret(webhook.id)}
                    >
                      Click to reveal
                    </button>
                  }
                >
                  <code class={styles.secretCode}>
                    {webhook.secret}
                  </code>
                  <button
                    class={styles.hideButton}
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
