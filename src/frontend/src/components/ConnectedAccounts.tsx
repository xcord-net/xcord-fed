import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export type ConnectionProvider =
  | 'GitHub'
  | 'Twitter'
  | 'Spotify'
  | 'YouTube'
  | 'Twitch'
  | 'Reddit'
  | 'Steam';

export interface ConnectedAccount {
  id: string;
  provider: ConnectionProvider;
  providerUserId: string;
  username: string;
  displayName?: string;
  avatarUrl?: string;
  connectedAt: string;
  showOnProfile: boolean;
}

interface ConnectedAccountsProps {
  compact?: boolean;
  userId?: string;
}

// ---- Helpers ----

const providerIcons: Record<ConnectionProvider, string> = {
  GitHub: 'GH',
  Twitter: 'TW',
  Spotify: 'SP',
  YouTube: 'YT',
  Twitch: 'TV',
  Reddit: 'RD',
  Steam: 'ST',
};

const providerColors: Record<ConnectionProvider, string> = {
  GitHub: 'bg-gray-700',
  Twitter: 'bg-sky-500',
  Spotify: 'bg-green-600',
  YouTube: 'bg-red-600',
  Twitch: 'bg-purple-600',
  Reddit: 'bg-orange-500',
  Steam: 'bg-blue-800',
};

export function getProviderIcon(provider: ConnectionProvider): string {
  return providerIcons[provider] ?? provider.slice(0, 2).toUpperCase();
}

export function getProviderColor(provider: ConnectionProvider): string {
  return providerColors[provider] ?? 'bg-xcord-bg-tertiary';
}

export function sortConnections(connections: ConnectedAccount[]): ConnectedAccount[] {
  return [...connections].sort((a, b) => a.provider.localeCompare(b.provider));
}

export function filterVisibleConnections(connections: ConnectedAccount[]): ConnectedAccount[] {
  return connections.filter((c) => c.showOnProfile);
}

// ---- Component ----

export default function ConnectedAccounts(props: ConnectedAccountsProps) {
  const [connections, setConnections] = createSignal<ConnectedAccount[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [disconnecting, setDisconnecting] = createSignal<string | null>(null);
  const [error, setError] = createSignal<string | null>(null);

  const loadConnections = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const path = props.userId
        ? `/api/v1/users/${props.userId}/connections`
        : '/api/v1/users/@me/connections';
      const data = await api.get<ConnectedAccount[]>(path);
      setConnections(sortConnections(data));
    } catch {
      setError('Failed to load connected accounts.');
      setConnections([]);
    } finally {
      setIsLoading(false);
    }
  };

  onMount(() => {
    loadConnections();
  });

  const disconnect = async (connectionId: string) => {
    setDisconnecting(connectionId);
    setError(null);
    try {
      await api.delete(`/api/v1/users/@me/connections/${connectionId}`);
      setConnections((prev) => prev.filter((c) => c.id !== connectionId));
    } catch {
      setError('Failed to disconnect account. Please try again.');
    } finally {
      setDisconnecting(null);
    }
  };

  const connect = async (provider: ConnectionProvider) => {
    // Redirect to OAuth flow
    window.location.href = `/api/v1/users/@me/connections/${provider.toLowerCase()}/authorize`;
  };

  const availableProviders: ConnectionProvider[] = [
    'GitHub',
    'Twitter',
    'Spotify',
    'YouTube',
    'Twitch',
    'Reddit',
    'Steam',
  ];

  const connectedProviders = () => new Set(connections().map((c) => c.provider));

  return (
    <div class="flex flex-col bg-xcord-bg-secondary">
      <Show when={!props.compact}>
        <div class="px-4 py-3 border-b border-xcord-bg-tertiary">
          <h2 class="text-xcord-text-primary font-semibold">Connected Accounts</h2>
          <p class="text-xcord-text-muted text-xs mt-0.5">
            Link external accounts to show on your profile.
          </p>
        </div>
      </Show>

      {/* Loading state */}
      <Show when={isLoading()}>
        <div class="flex items-center justify-center h-24">
          <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      <Show when={error()}>
        <p class="text-red-400 text-xs px-4 py-2">{error()}</p>
      </Show>

      <Show when={!isLoading()}>
        {/* Connected accounts list */}
        <div class="divide-y divide-xcord-bg-tertiary">
          <For each={connections()}>
            {(account) => (
              <div class="px-4 py-3 flex items-center gap-3">
                {/* Provider icon badge */}
                <div
                  class={`w-9 h-9 rounded flex items-center justify-center text-white text-xs font-bold flex-shrink-0 ${getProviderColor(account.provider)}`}
                  aria-label={account.provider}
                >
                  {getProviderIcon(account.provider)}
                </div>

                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="text-xcord-text-primary font-medium text-sm">{account.provider}</span>
                    <Show when={!account.showOnProfile}>
                      <span class="text-xcord-text-muted text-xs">(hidden)</span>
                    </Show>
                  </div>
                  <p class="text-xcord-text-muted text-xs truncate">
                    {account.displayName ?? account.username}
                  </p>
                  <p class="text-xcord-text-muted text-xs">
                    Connected {new Date(account.connectedAt).toLocaleDateString()}
                  </p>
                </div>

                <Show when={!props.compact}>
                  <button
                    class="flex-shrink-0 px-3 py-1 rounded text-xs bg-xcord-bg-tertiary text-xcord-text-muted hover:bg-red-600 hover:text-white transition-colors disabled:opacity-50"
                    onClick={() => disconnect(account.id)}
                    disabled={disconnecting() === account.id}
                    aria-label={`Disconnect ${account.provider}`}
                  >
                    {disconnecting() === account.id ? 'Removing...' : 'Disconnect'}
                  </button>
                </Show>
              </div>
            )}
          </For>
        </div>

        {/* Available providers to connect */}
        <Show when={!props.compact}>
          <div class="px-4 py-3 border-t border-xcord-bg-tertiary">
            <p class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide mb-2">
              Add Connection
            </p>
            <div class="flex flex-wrap gap-2">
              <For each={availableProviders}>
                {(provider) => (
                  <Show when={!connectedProviders().has(provider)}>
                    <button
                      class={`flex items-center gap-2 px-3 py-1.5 rounded text-xs text-white transition-opacity hover:opacity-80 ${getProviderColor(provider)}`}
                      onClick={() => connect(provider)}
                      aria-label={`Connect ${provider}`}
                    >
                      <span class="font-bold">{getProviderIcon(provider)}</span>
                      <span>{provider}</span>
                    </button>
                  </Show>
                )}
              </For>
            </div>
          </div>
        </Show>

        {/* Empty state for compact view */}
        <Show when={props.compact && connections().length === 0}>
          <p class="text-xcord-text-muted text-xs px-4 py-2">No connected accounts</p>
        </Show>
      </Show>
    </div>
  );
}

export { providerIcons, providerColors };
