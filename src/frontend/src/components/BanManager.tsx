import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import ConfirmationButton from './ui/ConfirmationButton';
import styles from './BanManager.module.css';

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
      setError(getErrorMessage(err, 'Failed to load bans'));
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
      setError(getErrorMessage(err, 'Failed to unban user'));
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
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Bans</h2>
        <input
          type="text"
          placeholder="Search banned users..."
          class={styles.searchInput}
          value={searchQuery()}
          onInput={(e) => { setSearchQuery(e.currentTarget.value); setPage(1); }}
        />
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <div class={styles.listArea}>
        <Show when={isLoading()}>
          <div class={styles.loadingContainer}>
            <p class={styles.mutedText}>Loading bans...</p>
          </div>
        </Show>

        <Show when={!isLoading() && filteredBans().length === 0}>
          <div class={styles.emptyContainer}>
            <p class={styles.emptyTitle}>No bans found</p>
            <p class={styles.emptySubtitle}>
              {searchQuery() ? 'Try a different search.' : 'No users are currently banned.'}
            </p>
          </div>
        </Show>

        <For each={paginatedBans()}>
          {(ban) => (
            <div data-testid={`ban-list-item-${ban.username}`} class={styles.banItem}>
              <div class={styles.avatar}>
                <Show when={ban.avatarUrl} fallback={<span>{ban.username.charAt(0).toUpperCase()}</span>}>
                  <img
                    src={ban.avatarUrl}
                    alt={ban.username}
                    class={styles.avatarImg}
                  />
                </Show>
              </div>

              <div class={styles.banInfo}>
                <h3 class={styles.banUsername}>{ban.username}</h3>
                <Show when={ban.reason}>
                  <p class={styles.banReason}>Reason: {ban.reason}</p>
                </Show>
                <p class={styles.banDate}>
                  Banned {new Date(ban.createdAt).toLocaleDateString()}
                </p>
              </div>

              <ConfirmationButton
                isConfirming={confirmingUnban() === ban.userId}
                onStartConfirm={() => setConfirmingUnban(ban.userId)}
                onConfirm={() => unban(ban.userId)}
                onCancel={() => setConfirmingUnban(null)}
                label="Unban"
                confirmText="Confirm unban?"
                testId={`unban-button-${ban.username}`}
              />
            </div>
          )}
        </For>
      </div>

      <Show when={!isLoading() && filteredBans().length > PAGE_SIZE}>
        <div class={styles.pagination}>
          <button
            class={styles.pageButton}
            disabled={page() <= 1}
            onClick={() => setPage(page() - 1)}
            aria-label="Previous page"
          >
            Previous
          </button>
          <span class={styles.pageInfo}>
            Page {page()} of {totalPages()}
          </span>
          <button
            class={styles.pageButton}
            disabled={page() >= totalPages()}
            onClick={() => setPage(page() + 1)}
            aria-label="Next page"
          >
            Next
          </button>
        </div>
      </Show>
    </div>
  );
}
