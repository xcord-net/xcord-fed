import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import ConfirmationButton from './ui/ConfirmationButton';
import styles from './WebhookManager.module.css';
import EmptyState from './ui/EmptyState';
import { Webhook } from 'lucide-solid';

/**
 * The events a webhook can subscribe to.
 *
 * These are the names the server validates against, and it rejects anything
 * else outright - the dotted forms this used to send ("message.created") were
 * refused with "Invalid event types", so no webhook could be created at all.
 * Four is the whole set the server supports today.
 */
export type WebhookEvent =
  | 'MessageCreated'
  | 'MemberJoined'
  | 'MemberLeft'
  | 'MemberBanned';

export const WEBHOOK_EVENT_LABELS: Record<WebhookEvent, string> = {
  MessageCreated: 'Message Created',
  MemberJoined: 'Member Joined',
  MemberLeft: 'Member Left',
  MemberBanned: 'Member Banned',
};

export const ALL_WEBHOOK_EVENTS: WebhookEvent[] = [
  'MessageCreated',
  'MemberJoined',
  'MemberLeft',
  'MemberBanned',
];

/**
 * An outgoing webhook, as the server actually describes it.
 *
 * There is no name and no secret in this contract, and the fields are
 * `eventTypes` and `isActive` - this type previously claimed otherwise, against
 * a URL that did not exist either, so nothing here could load, create or delete.
 * A webhook is identified by where it points.
 */
export interface OutgoingWebhook {
  id: string;
  serverId: string;
  targetUrl: string;
  eventTypes: WebhookEvent[];
  isActive: boolean;
  createdByUserId: string;
  createdAt: string;
  /**
   * Only present on the webhook you have just created - the listing does not
   * carry it, so there is nothing to reveal for a webhook loaded from the
   * server. Same shape of promise as a bot token: shown once.
   */
  secret?: string;
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

/**
 * Kept for the API surface's sake, but no longer used by the form: the server
 * has nowhere to put a webhook's name.
 */
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
  const [formUrl, setFormUrl] = createSignal('');
  const [formEvents, setFormEvents] = createSignal<WebhookEvent[]>([]);
  const [formError, setFormError] = createSignal<string | null>(null);

  async function loadWebhooks() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<OutgoingWebhook[]>(
        `/api/v1/servers/${props.serverId}/outgoing-webhooks`,
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

    const urlErr = validateWebhookUrl(formUrl());
    if (urlErr) { setFormError(urlErr); return; }

    if (formEvents().length === 0) {
      setFormError('Select at least one event to subscribe to.');
      return;
    }

    setIsSaving(true);
    try {
      const created = await api.post<OutgoingWebhook>(
        `/api/v1/servers/${props.serverId}/outgoing-webhooks`,
        { targetUrl: formUrl().trim(), eventTypes: formEvents() },
      );
      setWebhooks([...webhooks(), created]);
      setShowForm(false);
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
        `/api/v1/servers/${props.serverId}/outgoing-webhooks/${webhook.id}`,
        { enabled: !webhook.isActive },
      );
      setWebhooks(webhooks().map((w) => (w.id === webhook.id ? updated : w)));
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to update webhook'));
    }
  }

  async function handleDelete(webhookId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/outgoing-webhooks/${webhookId}`);
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
    <div class={styles.container} data-testid="webhook-manager-container">
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Outgoing Webhooks</h2>
        <button
          class={styles.addButton}
          data-testid="create-webhook-button"
          onClick={() => setShowForm(!showForm())}
        >
          {showForm() ? 'Cancel' : 'Add Webhook'}
        </button>
      </div>

      {/* Create form */}
      <Show when={showForm()}>
        <div class={styles.createFormSection} data-testid="webhook-create-form">
          <h3 class={styles.createFormTitle}>New Webhook</h3>
          <form onSubmit={handleCreate} class={styles.createFormFields}>
            {/* No name field: the server stores none, so asking for one collected
                something that was thrown away the moment it was submitted. A
                webhook is identified by its destination. */}
            <input
              type="url"
              data-testid="webhook-url-input"
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
                        data-testid={`webhook-event-${evt}`}
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
              data-testid="webhook-submit-button"
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
      <div class={styles.webhookList} data-testid="webhook-list">
        <Show when={isLoading()}>
          <div class={styles.loadingContainer}>
            <p class={styles.mutedText}>Loading webhooks...</p>
          </div>
        </Show>

        <Show when={!isLoading() && webhooks().length === 0}>
          <EmptyState
            icon={Webhook}
            title="No outgoing webhooks"
            body="A webhook posts this community's events to a URL you control."
            data-testid="webhook-list-empty-state"
          />
        </Show>

        <For each={webhooks()}>
          {(webhook) => (
            <div class={styles.webhookCard} data-testid={`webhook-item-${webhook.id}`}>
              <div class={styles.webhookHeader}>
                <div class={styles.webhookInfo}>
                  <div class={styles.webhookNameRow}>
                    {/* The destination is the identity, and it already has its
                        own line below - no separate name to show. */}
                    <span
                      class={webhook.isActive ? styles.statusBadgeActive : styles.statusBadgeDisabled}
                    >
                      {webhook.isActive ? 'Active' : 'Disabled'}
                    </span>
                  </div>
                  <p class={styles.webhookUrl} data-testid={`webhook-url-${webhook.id}`}>{webhook.targetUrl}</p>
                  <div class={styles.webhookEvents}>
                    <For each={webhook.eventTypes}>
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
                    class={webhook.isActive ? styles.toggleButtonEnabled : styles.toggleButtonDisabled}
                    data-testid={`webhook-toggle-button-${webhook.id}`}
                    onClick={() => handleToggleEnabled(webhook)}
                    title={webhook.isActive ? 'Disable webhook' : 'Enable webhook'}
                  >
                    {webhook.isActive ? 'Disable' : 'Enable'}
                  </button>
                  <ConfirmationButton
                    isConfirming={confirmingDelete() === webhook.id}
                    onStartConfirm={() => setConfirmingDelete(webhook.id)}
                    onConfirm={() => { handleDelete(webhook.id); setConfirmingDelete(null); }}
                    onCancel={() => setConfirmingDelete(null)}
                    label="Delete"
                    testId={`delete-webhook-button-${webhook.id}`}
                  />
                </div>
              </div>

              {/* Secret key */}
              <Show when={webhook.secret}>
              <div class={styles.secretRow} data-testid={`webhook-secret-row-${webhook.id}`}>
                <span class={styles.secretLabel}>Secret:</span>
                <Show
                  when={revealedSecret() === webhook.id}
                  fallback={
                    <button
                      class={styles.revealButton}
                      data-testid={`webhook-reveal-secret-button-${webhook.id}`}
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
              </Show>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
