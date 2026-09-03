import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Flexbox from './ui/Flexbox';
import styles from './AuditLogViewer.module.css';
import EmptyState from './ui/EmptyState';
import { ScrollText } from 'lucide-solid';

export interface AuditLogEntry {
  id: string;
  actionType: string;
  actorId: string;
  actorUsername: string;
  targetId?: string;
  targetName?: string;
  reason?: string;
  createdAt: string;
  changes?: Record<string, unknown>;
}

const ACTION_ICONS: Record<string, string> = {
  MemberKick: '🚫',
  MemberBan: '🔨',
  MemberUnban: '✅',
  MemberUpdate: '✏️',
  MemberTimeout: '⏰',
  ChannelCreate: '📢',
  ChannelDelete: '🗑️',
  ChannelUpdate: '📝',
  RoleCreate: '🏷️',
  RoleDelete: '🗑️',
  RoleUpdate: '✏️',
  MessageDelete: '🗑️',
  MessageBulkDelete: '🗑️',
  ServerUpdate: '⚙️',
  InviteCreate: '🔗',
  InviteDelete: '❌',
};

function getActionIcon(actionType: string): string {
  return ACTION_ICONS[actionType] ?? '📋';
}

function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

const ACTION_TYPES = [
  'MemberKick',
  'MemberBan',
  'MemberUnban',
  'MemberUpdate',
  'MemberTimeout',
  'ChannelCreate',
  'ChannelDelete',
  'ChannelUpdate',
  'RoleCreate',
  'RoleDelete',
  'RoleUpdate',
  'MessageDelete',
  'MessageBulkDelete',
  'ServerUpdate',
  'InviteCreate',
  'InviteDelete',
];

interface AuditLogViewerProps {
  serverId: string;
}

export default function AuditLogViewer(props: AuditLogViewerProps) {
  const [entries, setEntries] = createSignal<AuditLogEntry[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [hasMore, setHasMore] = createSignal(true);
  const [error, setError] = createSignal<string | null>(null);
  const [actionFilter, setActionFilter] = createSignal('');
  const [userFilter, setUserFilter] = createSignal('');
  const LIMIT = 50;

  async function loadEntries(reset = false) {
    if (isLoading()) return;
    setIsLoading(true);
    setError(null);

    try {
      const current = reset ? [] : entries();
      const beforeId = current.length > 0 ? current[current.length - 1].id : undefined;

      let url = `/api/v1/servers/${props.serverId}/audit-log?limit=${LIMIT}`;
      if (beforeId) url += `&before=${beforeId}`;
      if (actionFilter()) url += `&actionType=${encodeURIComponent(actionFilter())}`;
      if (userFilter()) url += `&actorId=${encodeURIComponent(userFilter())}`;

      const result = await api.get<AuditLogEntry[]>(url);
      const newEntries = reset ? result : [...current, ...result];
      setEntries(newEntries);
      setHasMore(result.length === LIMIT);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load audit log'));
    } finally {
      setIsLoading(false);
    }
  }

  function applyFilters() {
    loadEntries(true);
  }

  onMount(() => {
    loadEntries(true);
  });

  return (
    <Flexbox direction="vertical" class={styles.container}>
      <div class={styles.filterHeader}>
        <h2 data-testid="audit-log-heading" class={styles.filterTitle}>Audit Log</h2>
        <Flexbox direction="vertical" gap={0.5} class={styles.filterControls}>
          <select
            class={styles.filterSelect}
            value={actionFilter()}
            onChange={(e) => { setActionFilter(e.currentTarget.value); applyFilters(); }}
          >
            <option value="">All Actions</option>
            <For each={ACTION_TYPES}>
              {(type) => <option value={type}>{type}</option>}
            </For>
          </select>
          <input
            type="text"
            placeholder="Filter by user (username or ID)..."
            class={styles.filterInput}
            value={userFilter()}
            onInput={(e) => setUserFilter(e.currentTarget.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') applyFilters(); }}
          />
          <Flexbox.Item align="end">
            <button
              class={styles.applyButton}
              onClick={() => applyFilters()}
            >
              Apply Filters
            </button>
          </Flexbox.Item>
        </Flexbox>
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <div class={styles.scrollArea}>
        <Show when={!isLoading() && entries().length === 0}>
          <EmptyState
            icon={ScrollText}
            title={actionFilter() ? 'No entries match that action' : 'No audit log entries yet'}
            body={actionFilter()
              ? 'Clear the filter to see everything recorded so far.'
              : 'Moderation and settings changes are recorded here as they happen.'}
            data-testid="audit-log-empty"
          />
        </Show>

        <For each={entries()}>
          {(entry) => (
            <Flexbox align="start" gap={0.75} data-testid="audit-log-entry" data-action={entry.actionType} class={styles.entryRow}>
              <span class={styles.entryIcon} aria-hidden="true">
                {getActionIcon(entry.actionType)}
              </span>

              <div class={styles.entryBody}>
                <Flexbox align="baseline" gap={0.5} class={styles.entryHeadline}>
                  <span class={styles.entryActor}>{entry.actorUsername}</span>
                  <span class={styles.entryAction}>{entry.actionType}</span>
                </Flexbox>

                <Show when={entry.targetName || entry.targetId}>
                  <p class={styles.entryTarget}>
                    Target: {entry.targetName ?? entry.targetId}
                  </p>
                </Show>

                <Show when={entry.reason}>
                  <p class={styles.entryReason}>Reason: {entry.reason}</p>
                </Show>

                <time
                  class={styles.entryTime}
                  dateTime={entry.createdAt}
                  title={new Date(entry.createdAt).toISOString()}
                >
                  {formatTimestamp(entry.createdAt)}
                </time>
              </div>
            </Flexbox>
          )}
        </For>

        <Show when={isLoading()}>
          <Flexbox align="center" justify="center" class={styles.loadingCenter}>
            <p class={styles.loadingText}>Loading...</p>
          </Flexbox>
        </Show>

        <Show when={!isLoading() && hasMore() && entries().length > 0}>
          <Flexbox justify="center" class={styles.loadMoreRow}>
            <button
              class={styles.loadMoreButton}
              onClick={() => loadEntries(false)}
            >
              Load More
            </button>
          </Flexbox>
        </Show>

        <Show when={!isLoading() && !hasMore() && entries().length > 0}>
          <Flexbox justify="center" class={styles.endOfLog}>
            <p class={styles.endOfLogText}>End of audit log</p>
          </Flexbox>
        </Show>
      </div>
    </Flexbox>
  );
}

export { formatTimestamp, getActionIcon };
