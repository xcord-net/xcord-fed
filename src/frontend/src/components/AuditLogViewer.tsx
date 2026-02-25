import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

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
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load audit log');
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
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold mb-2">Audit Log</h2>
        <div class="flex flex-col space-y-2">
          <select
            class="bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
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
            class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
            value={userFilter()}
            onInput={(e) => setUserFilter(e.currentTarget.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') applyFilters(); }}
          />
          <button
            class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover self-end"
            onClick={() => applyFilters()}
          >
            Apply Filters
          </button>
        </div>
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <div class="flex-1 overflow-y-auto">
        <Show when={!isLoading() && entries().length === 0}>
          <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
            <p class="text-lg font-semibold">No audit log entries</p>
            <p class="text-sm mt-1">No actions recorded yet{actionFilter() ? ' for this filter.' : '.'}</p>
          </div>
        </Show>

        <For each={entries()}>
          {(entry) => (
            <div class="px-4 py-3 flex items-start space-x-3 hover:bg-xcord-bg-primary/50 border-b border-xcord-border">
              <span class="text-2xl flex-shrink-0 mt-0.5" aria-hidden="true">
                {getActionIcon(entry.actionType)}
              </span>

              <div class="flex-1 min-w-0">
                <div class="flex items-baseline space-x-2">
                  <span class="text-white font-medium text-sm">{entry.actorUsername}</span>
                  <span class="text-xcord-text-muted text-xs">{entry.actionType}</span>
                </div>

                <Show when={entry.targetName || entry.targetId}>
                  <p class="text-sm text-xcord-text-muted truncate">
                    Target: {entry.targetName ?? entry.targetId}
                  </p>
                </Show>

                <Show when={entry.reason}>
                  <p class="text-xs text-xcord-text-muted truncate">Reason: {entry.reason}</p>
                </Show>

                <time
                  class="text-xs text-xcord-text-muted"
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
          <div class="flex items-center justify-center py-6">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={!isLoading() && hasMore() && entries().length > 0}>
          <div class="px-4 py-3 flex justify-center">
            <button
              class="bg-xcord-bg-primary text-xcord-text-muted px-4 py-2 rounded text-sm hover:bg-xcord-bg-tertiary hover:text-white"
              onClick={() => loadEntries(false)}
            >
              Load More
            </button>
          </div>
        </Show>

        <Show when={!isLoading() && !hasMore() && entries().length > 0}>
          <div class="px-4 py-3 flex justify-center">
            <p class="text-xcord-text-muted text-sm">End of audit log</p>
          </div>
        </Show>
      </div>
    </div>
  );
}

export { formatTimestamp, getActionIcon };
