import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

interface ConnectedAccount {
  id: string;
  provider: string;
  providerUsername: string;
  connectedAt: string;
}

const AVAILABLE_PROVIDERS = ['GitHub', 'Twitter', 'Spotify'];

// ---- Component ----

export default function ConnectedAccounts() {
  const [accounts, setAccounts] = createSignal<ConnectedAccount[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);

  const loadAccounts = async () => {
    setIsLoading(true);
    try {
      const data = await api.get<ConnectedAccount[]>('/api/v1/users/@me/connected-accounts');
      setAccounts(data ?? []);
    } catch {
      setAccounts([]);
    } finally {
      setIsLoading(false);
    }
  };

  onMount(() => {
    loadAccounts();
  });

  const handleDisconnect = async (accountId: string) => {
    try {
      await api.delete(`/api/v1/users/@me/connected-accounts/${accountId}`);
      setAccounts((prev) => prev.filter((a) => a.id !== accountId));
    } catch {
      // Swallow — UI will remain unchanged
    }
  };

  const handleConnect = (provider: string) => {
    // OAuth redirect — opens the provider auth flow
    window.location.href = `/api/v1/auth/connect/${provider.toLowerCase()}`;
  };

  const connectedProviders = () => new Set(accounts().map((a) => a.provider));

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Connected Accounts</h2>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-6">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Connected accounts list */}
          <Show when={accounts().length > 0}>
            <div class="space-y-2">
              <For each={accounts()}>
                {(account) => (
                  <div class="flex items-center justify-between bg-xcord-bg-primary rounded px-4 py-3">
                    <div>
                      <p class="text-white font-medium text-sm">{account.provider}</p>
                      <p class="text-xcord-text-muted text-xs">{account.providerUsername}</p>
                    </div>
                    <button
                      class="text-red-400 hover:text-red-300 text-sm px-3 py-1 rounded bg-red-500/10 hover:bg-red-500/20 transition-colors"
                      onClick={() => handleDisconnect(account.id)}
                    >
                      Disconnect
                    </button>
                  </div>
                )}
              </For>
            </div>
          </Show>

          {/* Add Connection section */}
          <div>
            <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-3">
              Add Connection
            </p>
            <div class="space-y-2">
              <For each={AVAILABLE_PROVIDERS}>
                {(provider) => (
                  <Show when={!connectedProviders().has(provider)}>
                    <button
                      class="w-full flex items-center justify-between bg-xcord-bg-primary rounded px-4 py-3 hover:bg-xcord-bg-primary/80 transition-colors"
                      aria-label={`Connect ${provider}`}
                      onClick={() => handleConnect(provider)}
                    >
                      <span class="text-white text-sm font-medium">{provider}</span>
                      <span class="text-xcord-brand text-sm">Connect</span>
                    </button>
                  </Show>
                )}
              </For>
            </div>
          </div>
        </Show>
      </div>
    </div>
  );
}
