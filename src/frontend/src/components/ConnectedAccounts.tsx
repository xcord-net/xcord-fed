import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import styles from './ConnectedAccounts.module.css';

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
      // Swallow - UI will remain unchanged
    }
  };

  const handleConnect = (provider: string) => {
    // OAuth redirect - opens the provider auth flow
    window.location.href = `/api/v1/auth/connect/${provider.toLowerCase()}`;
  };

  const connectedProviders = () => new Set(accounts().map((a) => a.provider));

  return (
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 class={styles.heading}>Connected Accounts</h2>
      </div>

      <div class={styles.content}>
        <Show when={isLoading()}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Connected accounts list */}
          <Show when={accounts().length > 0}>
            <div class={styles.accountList}>
              <For each={accounts()}>
                {(account) => (
                  <div class={styles.accountRow}>
                    <div class={styles.accountInfo}>
                      <p class={styles.accountProvider}>{account.provider}</p>
                      <p class={styles.accountUsername}>{account.providerUsername}</p>
                    </div>
                    <button
                      class={styles.disconnectButton}
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
          <div class={styles.addSection}>
            <p class={styles.addSectionLabel}>
              Add Connection
            </p>
            <div class={styles.providerList}>
              <For each={AVAILABLE_PROVIDERS}>
                {(provider) => (
                  <Show when={!connectedProviders().has(provider)}>
                    <button
                      class={styles.connectButton}
                      aria-label={`Connect ${provider}`}
                      onClick={() => handleConnect(provider)}
                    >
                      <span class={styles.providerName}>{provider}</span>
                      <span class={styles.connectLabel}>Connect</span>
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
