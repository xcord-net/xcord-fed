import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

interface BannedUser {
  id: string;
  userId: string;
  username: string;
  avatarUrl?: string;
  moderatorId?: string;
  reason?: string;
  createdAt: string;
}

interface BanManagerProps {
  serverId: string;
}

// ---- Pure helpers ----

export function filterBans<T extends { username: string }>(bans: T[], query: string): T[] {
  const q = query.toLowerCase();
  if (!q) return bans;
  return bans.filter((b) => b.username.toLowerCase().includes(q));
}

export function paginateBans<T>(bans: T[], page: number, pageSize: number): T[] {
  const start = (page - 1) * pageSize;
  return bans.slice(start, start + pageSize);
}

export function totalBanPages(count: number, pageSize: number): number {
  return Math.max(1, Math.ceil(count / pageSize));
}

// ---- Component ----

export default function BanManager(props: BanManagerProps) {
  const [bans, setBans] = createSignal<BannedUser[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [searchQuery, setSearchQuery] = createSignal('');
  const [confirmingUnban, setConfirmingUnban] = createSignal<string | null>(null);
  const [page, setPage] = createSignal(1);
  const PAGE_SIZE = 20;

  async function loadBans() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<BannedUser[]>(`/api/v1/servers/${props.serverId}/bans`);
      setBans(result);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load bans');
    } finally {
      setIsLoading(false);
    }
  }

  async function unban(userId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/bans/${userId}`);
      setBans(bans().filter((b) => b.userId !== userId));
      setConfirmingUnban(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to unban user');
    }
  }

  function filteredBans() {
    const q = searchQuery().toLowerCase();
    if (!q) return bans();
    return bans().filter((b) => b.username.toLowerCase().includes(q));
  }

  function paginatedBans() {
    const all = filteredBans();
    const start = (page() - 1) * PAGE_SIZE;
    return all.slice(start, start + PAGE_SIZE);
  }

  function totalPages() {
    return Math.max(1, Math.ceil(filteredBans().length / PAGE_SIZE));
  }

  onMount(() => {
    loadBans();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold mb-2">Bans</h2>
        <input
          type="text"
          placeholder="Search banned users..."
          class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
          value={searchQuery()}
          onInput={(e) => { setSearchQuery(e.currentTarget.value); setPage(1); }}
        />
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <div class="flex-1 overflow-y-auto">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading bans...</p>
          </div>
        </Show>

        <Show when={!isLoading() && filteredBans().length === 0}>
          <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
            <p class="text-lg font-semibold">No bans found</p>
            <p class="text-sm mt-1">
              {searchQuery() ? 'Try a different search.' : 'No users are currently banned.'}
            </p>
          </div>
        </Show>

        <For each={paginatedBans()}>
          {(ban) => (
            <div class="px-4 py-3 flex items-start space-x-3 hover:bg-xcord-bg-primary/50 border-b border-xcord-border">
              <div class="w-10 h-10 rounded-full bg-xcord-brand flex-shrink-0 flex items-center justify-center text-white font-semibold overflow-hidden">
                <Show when={ban.avatarUrl} fallback={<span>{ban.username.charAt(0).toUpperCase()}</span>}>
                  <img
                    src={ban.avatarUrl}
                    alt={ban.username}
                    class="w-full h-full object-cover"
                  />
                </Show>
              </div>

              <div class="flex-1 min-w-0">
                <h3 class="text-white font-medium truncate">{ban.username}</h3>
                <Show when={ban.reason}>
                  <p class="text-sm text-xcord-text-muted truncate">Reason: {ban.reason}</p>
                </Show>
                <p class="text-xs text-xcord-text-muted">
                  Banned {new Date(ban.createdAt).toLocaleDateString()}
                </p>
              </div>

              <Show
                when={confirmingUnban() === ban.userId}
                fallback={
                  <button
                    class="bg-red-500 text-white px-3 py-1 rounded text-sm hover:bg-red-600 flex-shrink-0"
                    onClick={() => setConfirmingUnban(ban.userId)}
                  >
                    Unban
                  </button>
                }
              >
                <div class="flex flex-col items-end space-y-1">
                  <p class="text-xs text-xcord-text-muted">Confirm unban?</p>
                  <div class="flex space-x-2">
                    <button
                      class="bg-xcord-bg-primary text-xcord-text-muted px-2 py-1 rounded text-xs hover:bg-xcord-bg-tertiary"
                      onClick={() => setConfirmingUnban(null)}
                    >
                      Cancel
                    </button>
                    <button
                      class="bg-green-600 text-white px-2 py-1 rounded text-xs hover:bg-green-700"
                      onClick={() => unban(ban.userId)}
                    >
                      Confirm
                    </button>
                  </div>
                </div>
              </Show>
            </div>
          )}
        </For>
      </div>

      <Show when={!isLoading() && filteredBans().length > PAGE_SIZE}>
        <div class="px-4 py-2 border-t border-xcord-border flex items-center justify-between">
          <button
            class="text-xcord-text-muted text-sm hover:text-white disabled:opacity-40"
            disabled={page() <= 1}
            onClick={() => setPage(page() - 1)}
          >
            Previous
          </button>
          <span class="text-xcord-text-muted text-sm">
            Page {page()} of {totalPages()}
          </span>
          <button
            class="text-xcord-text-muted text-sm hover:text-white disabled:opacity-40"
            disabled={page() >= totalPages()}
            onClick={() => setPage(page() + 1)}
          >
            Next
          </button>
        </div>
      </Show>
    </div>
  );
}
