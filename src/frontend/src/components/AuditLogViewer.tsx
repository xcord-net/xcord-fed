import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './AuditLogViewer.module.css';

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
    <div class={styles.container}>
      <div class={styles.filterHeader}>
        <h2 class={styles.filterTitle}>Audit Log</h2>
        <div class={styles.filterControls}>
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
          <button
            class={styles.applyButton}
            onClick={() => applyFilters()}
          >
            Apply Filters
          </button>
        </div>
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <div class={styles.scrollArea}>
        <Show when={!isLoading() && entries().length === 0}>
          <div class={styles.emptyState}>
            <p class={styles.emptyStateTitle}>No audit log entries</p>
            <p class={styles.emptyStateSubtitle}>No actions recorded yet{actionFilter() ? ' for this filter.' : '.'}</p>
          </div>
        </Show>

        <For each={entries()}>
          {(entry) => (
            <div class={styles.entryRow}>
              <span class={styles.entryIcon} aria-hidden="true">
                {getActionIcon(entry.actionType)}
              </span>

              <div class={styles.entryBody}>
                <div class={styles.entryHeadline}>
                  <span class={styles.entryActor}>{entry.actorUsername}</span>
                  <span class={styles.entryAction}>{entry.actionType}</span>
                </div>

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
            </div>
          )}
        </For>

        <Show when={isLoading()}>
          <div class={styles.loadingCenter}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <Show when={!isLoading() && hasMore() && entries().length > 0}>
          <div class={styles.loadMoreRow}>
            <button
              class={styles.loadMoreButton}
              onClick={() => loadEntries(false)}
            >
              Load More
            </button>
          </div>
        </Show>

        <Show when={!isLoading() && !hasMore() && entries().length > 0}>
          <div class={styles.endOfLog}>
            <p class={styles.endOfLogText}>End of audit log</p>
          </div>
        </Show>
      </div>
    </div>
  );
}

export { formatTimestamp, getActionIcon };
