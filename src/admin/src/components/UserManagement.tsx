import { createSignal, For, Show } from 'solid-js';
import { api } from '../api/client';
import type { AdminUser, AdminUsersResponse } from '../types/user';

export function UserManagement() {
  const [users, setUsers] = createSignal<AdminUser[]>([]);
  const [totalCount, setTotalCount] = createSignal(0);
  const [searchQuery, setSearchQuery] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal('');
  const [selectedUser, setSelectedUser] = createSignal<AdminUser | null>(null);
  const [hasLoaded, setHasLoaded] = createSignal(false);

  const loadUsers = async () => {
    setIsLoading(true);
    setError('');
    try {
      const query = searchQuery() ? `?search=${encodeURIComponent(searchQuery())}` : '';
      const response = await api.get<AdminUsersResponse>(`/api/v1/admin/users${query}`);
      setUsers(response.users);
      setTotalCount(response.totalCount);
      setHasLoaded(true);
    } catch (err: any) {
      setError(err?.message || err?.error || 'Failed to load users. The admin users endpoint may not be deployed yet.');
      setHasLoaded(true);
    } finally {
      setIsLoading(false);
    }
  };

  const searchByUsername = async () => {
    const query = searchQuery().trim();
    if (!query) {
      loadUsers();
      return;
    }

    setIsLoading(true);
    setError('');
    try {
      // Try the admin search endpoint first
      const searchUrl = `/api/v1/admin/users?search=${encodeURIComponent(query)}`;
      const response = await api.get<AdminUsersResponse>(searchUrl);
      setUsers(response.users);
      setTotalCount(response.totalCount);
      setHasLoaded(true);
    } catch {
      // Fall back to the public user-by-username endpoint
      try {
        const user = await api.get<any>(`/api/v1/users/by-username/${encodeURIComponent(query)}`);
        if (user) {
          const adminUser: AdminUser = {
            id: user.id || user.userId,
            username: user.username,
            displayName: user.displayName || user.username,
            avatarUrl: user.avatarUrl || null,
            isBot: user.isBot || false,
            isAdmin: user.isAdmin || false,
            isDisabled: user.isDisabled || false,
            emailConfirmed: user.emailConfirmed || false,
            createdAt: user.createdAt || '',
            lastLoginAt: user.lastLoginAt || null,
          };
          setUsers([adminUser]);
          setTotalCount(1);
        } else {
          setUsers([]);
          setTotalCount(0);
        }
        setHasLoaded(true);
      } catch (err: any) {
        setError('User not found');
        setUsers([]);
        setTotalCount(0);
        setHasLoaded(true);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleDisableUser = async (userId: string, disable: boolean) => {
    const action = disable ? 'disable' : 'enable';
    if (!confirm(`Are you sure you want to ${action} this user?`)) return;

    setError('');
    try {
      await api.patch(`/api/v1/admin/users/${userId}`, { isDisabled: disable });
      // Refresh the user in the list
      setUsers((prev) =>
        prev.map((u) => (u.id === userId ? { ...u, isDisabled: disable } : u))
      );
      if (selectedUser()?.id === userId) {
        setSelectedUser({ ...selectedUser()!, isDisabled: disable });
      }
    } catch (err: any) {
      setError(err?.message || `Failed to ${action} user`);
    }
  };

  const handleSearch = (e: Event) => {
    e.preventDefault();
    searchByUsername();
  };

  const formatDate = (dateStr: string | null): string => {
    if (!dateStr) return 'Never';
    return new Date(dateStr).toLocaleString();
  };

  return (
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold text-white">User Management</h2>
        <button
          onClick={loadUsers}
          class="px-4 py-2 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand-hover transition-colors"
        >
          Load All Users
        </button>
      </div>

      {/* Search bar */}
      <form onSubmit={handleSearch} class="mb-6">
        <div class="flex gap-2">
          <input
            type="text"
            value={searchQuery()}
            onInput={(e) => setSearchQuery(e.currentTarget.value)}
            class="flex-1 px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm placeholder-xcord-text-muted focus:outline-none focus:border-xcord-brand"
            placeholder="Search by username..."
          />
          <button
            type="submit"
            disabled={isLoading()}
            class="px-4 py-2 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand-hover disabled:opacity-50 transition-colors"
          >
            Search
          </button>
        </div>
      </form>

      {error() && (
        <div class="bg-xcord-danger/10 border border-xcord-danger/30 text-xcord-danger px-4 py-3 rounded text-sm mb-4">
          {error()}
        </div>
      )}

      <Show when={isLoading()}>
        <div class="text-xcord-text-muted text-sm">Loading users...</div>
      </Show>

      <Show when={!isLoading()}>
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* User list */}
          <div class="lg:col-span-2">
            <Show when={hasLoaded()}>
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-semibold uppercase text-xcord-text-muted">
                  Users ({totalCount()})
                </h3>
              </div>

              <Show when={users().length > 0} fallback={
                <p class="text-xcord-text-muted text-sm">No users found</p>
              }>
                <div class="bg-xcord-bg-secondary rounded-lg border border-xcord-border overflow-hidden">
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="border-b border-xcord-border">
                        <th class="text-left px-4 py-3 text-xs font-semibold uppercase text-xcord-text-muted">User</th>
                        <th class="text-left px-4 py-3 text-xs font-semibold uppercase text-xcord-text-muted">Status</th>
                        <th class="text-left px-4 py-3 text-xs font-semibold uppercase text-xcord-text-muted">Created</th>
                        <th class="text-right px-4 py-3 text-xs font-semibold uppercase text-xcord-text-muted">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      <For each={users()}>
                        {(user) => (
                          <tr
                            class={`border-b border-xcord-border/50 cursor-pointer transition-colors ${
                              selectedUser()?.id === user.id ? 'bg-xcord-brand/10' : 'hover:bg-xcord-bg-input/50'
                            }`}
                            onClick={() => setSelectedUser(user)}
                          >
                            <td class="px-4 py-3">
                              <div class="flex items-center gap-3">
                                <div class="w-8 h-8 rounded-full bg-xcord-bg-input flex items-center justify-center text-xcord-text-secondary text-xs font-bold">
                                  {user.username.charAt(0).toUpperCase()}
                                </div>
                                <div>
                                  <p class="text-white font-medium">{user.displayName || user.username}</p>
                                  <p class="text-xs text-xcord-text-muted">@{user.username}</p>
                                </div>
                              </div>
                            </td>
                            <td class="px-4 py-3">
                              <div class="flex gap-1 flex-wrap">
                                {user.isBot && (
                                  <span class="px-1.5 py-0.5 bg-xcord-brand/20 text-xcord-brand rounded text-[10px] font-semibold uppercase">Bot</span>
                                )}
                                {user.isAdmin && (
                                  <span class="px-1.5 py-0.5 bg-xcord-warning/20 text-xcord-warning rounded text-[10px] font-semibold uppercase">Admin</span>
                                )}
                                {user.isDisabled && (
                                  <span class="px-1.5 py-0.5 bg-xcord-danger/20 text-xcord-danger rounded text-[10px] font-semibold uppercase">Disabled</span>
                                )}
                                {!user.isBot && !user.isAdmin && !user.isDisabled && (
                                  <span class="px-1.5 py-0.5 bg-xcord-success/20 text-xcord-success rounded text-[10px] font-semibold uppercase">Active</span>
                                )}
                              </div>
                            </td>
                            <td class="px-4 py-3 text-xcord-text-secondary text-xs">
                              {formatDate(user.createdAt)}
                            </td>
                            <td class="px-4 py-3 text-right">
                              {!user.isAdmin && (
                                <button
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    handleDisableUser(user.id, !user.isDisabled);
                                  }}
                                  class={`px-2 py-1 rounded text-xs font-medium transition-colors ${
                                    user.isDisabled
                                      ? 'bg-xcord-success/20 text-xcord-success hover:bg-xcord-success/30'
                                      : 'bg-xcord-danger/20 text-xcord-danger hover:bg-xcord-danger/30'
                                  }`}
                                >
                                  {user.isDisabled ? 'Enable' : 'Disable'}
                                </button>
                              )}
                            </td>
                          </tr>
                        )}
                      </For>
                    </tbody>
                  </table>
                </div>
              </Show>
            </Show>

            <Show when={!hasLoaded()}>
              <div class="text-center py-12">
                <p class="text-xcord-text-muted text-sm mb-2">Search for a user by username or click "Load All Users" to browse.</p>
              </div>
            </Show>
          </div>

          {/* User detail panel */}
          <div>
            <Show when={selectedUser()}>
              {(user) => (
                <div class="bg-xcord-bg-secondary rounded-lg border border-xcord-border p-4">
                  <div class="flex items-center gap-3 mb-4">
                    <div class="w-14 h-14 rounded-full bg-xcord-bg-input flex items-center justify-center text-xcord-text-secondary text-xl font-bold">
                      {user().username.charAt(0).toUpperCase()}
                    </div>
                    <div>
                      <p class="font-semibold text-white">{user().displayName || user().username}</p>
                      <p class="text-sm text-xcord-text-muted">@{user().username}</p>
                    </div>
                  </div>

                  <div class="space-y-3">
                    <div>
                      <p class="text-xs font-semibold uppercase text-xcord-text-muted mb-1">User ID</p>
                      <p class="text-sm text-xcord-text-primary font-mono">{user().id}</p>
                    </div>

                    <div>
                      <p class="text-xs font-semibold uppercase text-xcord-text-muted mb-1">Flags</p>
                      <div class="flex gap-1 flex-wrap">
                        {user().isBot && <span class="px-2 py-0.5 bg-xcord-brand/20 text-xcord-brand rounded text-xs">Bot</span>}
                        {user().isAdmin && <span class="px-2 py-0.5 bg-xcord-warning/20 text-xcord-warning rounded text-xs">Admin</span>}
                        {user().isDisabled && <span class="px-2 py-0.5 bg-xcord-danger/20 text-xcord-danger rounded text-xs">Disabled</span>}
                        {user().emailConfirmed && <span class="px-2 py-0.5 bg-xcord-success/20 text-xcord-success rounded text-xs">Email Confirmed</span>}
                      </div>
                    </div>

                    <div>
                      <p class="text-xs font-semibold uppercase text-xcord-text-muted mb-1">Created</p>
                      <p class="text-sm text-xcord-text-secondary">{formatDate(user().createdAt)}</p>
                    </div>

                    <div>
                      <p class="text-xs font-semibold uppercase text-xcord-text-muted mb-1">Last Login</p>
                      <p class="text-sm text-xcord-text-secondary">{formatDate(user().lastLoginAt)}</p>
                    </div>

                    {!user().isAdmin && (
                      <div class="pt-3 border-t border-xcord-border">
                        <button
                          onClick={() => handleDisableUser(user().id, !user().isDisabled)}
                          class={`w-full py-2 rounded text-sm font-medium transition-colors ${
                            user().isDisabled
                              ? 'bg-xcord-success text-white hover:bg-xcord-success-hover'
                              : 'bg-xcord-danger text-white hover:bg-xcord-danger-hover'
                          }`}
                        >
                          {user().isDisabled ? 'Enable Account' : 'Disable Account'}
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </Show>

            <Show when={!selectedUser()}>
              <div class="bg-xcord-bg-secondary rounded-lg border border-xcord-border p-4">
                <p class="text-xcord-text-muted text-sm text-center">Select a user to view details</p>
              </div>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
}
