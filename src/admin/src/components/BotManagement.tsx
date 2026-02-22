import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';
import type { Bot, BotToken, CreateBotTokenRequest } from '../types/bot';

export function BotManagement() {
  const [bots, setBots] = createSignal<Bot[]>([]);
  const [selectedBotId, setSelectedBotId] = createSignal<string | null>(null);
  const [tokens, setTokens] = createSignal<BotToken[]>([]);
  const [isLoading, setIsLoading] = createSignal(true);
  const [isLoadingTokens, setIsLoadingTokens] = createSignal(false);
  const [error, setError] = createSignal('');
  const [showCreateToken, setShowCreateToken] = createSignal(false);
  const [tokenName, setTokenName] = createSignal('');
  const [permissions, setPermissions] = createSignal('0');
  const [createdToken, setCreatedToken] = createSignal('');

  createEffect(() => {
    loadBots();
  });

  createEffect(() => {
    const botId = selectedBotId();
    if (botId) {
      loadTokens(botId);
    }
  });

  const loadBots = async () => {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.get<Bot[]>('/api/v1/admin/bots');
      setBots(response);
    } catch (err: any) {
      setError(err?.message || 'Failed to load bots');
    } finally {
      setIsLoading(false);
    }
  };

  const loadTokens = async (botId: string) => {
    setIsLoadingTokens(true);
    setError('');
    try {
      const response = await api.get<BotToken[]>(`/api/v1/admin/bots/${botId}/tokens`);
      setTokens(response);
    } catch (err: any) {
      setError(err?.message || 'Failed to load tokens');
    } finally {
      setIsLoadingTokens(false);
    }
  };

  const handleCreateToken = async (e: Event) => {
    e.preventDefault();
    const botId = selectedBotId();
    if (!botId) return;

    setError('');
    try {
      const request: CreateBotTokenRequest = {
        tokenName: tokenName(),
        permissions: parseInt(permissions(), 10),
      };
      const response = await api.post<BotToken>(`/api/v1/admin/bots/${botId}/tokens`, request);
      setCreatedToken(response.token || '');
      setShowCreateToken(false);
      setTokenName('');
      setPermissions('0');
      await loadTokens(botId);
    } catch (err: any) {
      setError(err?.message || 'Failed to create token');
    }
  };

  const handleRevokeToken = async (botId: string, tokenId: string) => {
    if (!confirm('Are you sure you want to revoke this token?')) return;

    setError('');
    try {
      await api.delete(`/api/v1/admin/bots/${botId}/tokens/${tokenId}`);
      await loadTokens(botId);
    } catch (err: any) {
      setError(err?.message || 'Failed to revoke token');
    }
  };

  return (
    <div class="bg-white rounded-lg shadow p-6">
      <h2 class="text-2xl font-bold mb-6">Bot Management</h2>

      {error() && (
        <div class="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
          {error()}
        </div>
      )}

      {createdToken() && (
        <div class="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded mb-4">
          <p class="font-semibold mb-2">Token created successfully!</p>
          <p class="text-sm mb-2">Copy this token now - it will not be shown again:</p>
          <code class="block bg-white p-2 rounded text-xs break-all">{createdToken()}</code>
          <button
            onClick={() => setCreatedToken('')}
            class="mt-2 text-sm underline"
          >
            Dismiss
          </button>
        </div>
      )}

      <Show when={isLoading()} fallback={
        <>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <h3 class="text-lg font-semibold mb-4">Bots</h3>
              <Show when={bots().length === 0} fallback={
                <div class="space-y-2">
                  <For each={bots()}>
                    {(bot) => (
                      <button
                        onClick={() => setSelectedBotId(bot.id)}
                        class={`w-full text-left p-4 rounded border ${
                          selectedBotId() === bot.id
                            ? 'border-blue-500 bg-blue-50'
                            : 'border-gray-200 hover:bg-gray-50'
                        }`}
                      >
                        <div class="flex items-center gap-3">
                          <div class="w-10 h-10 rounded-full bg-gray-300 flex items-center justify-center text-white font-bold">
                            {bot.name.charAt(0)}
                          </div>
                          <div>
                            <p class="font-medium">{bot.name}#{bot.discriminator}</p>
                            <p class="text-xs text-gray-500">ID: {bot.id}</p>
                          </div>
                        </div>
                      </button>
                    )}
                  </For>
                </div>
              }>
                <p class="text-gray-500">No bots found</p>
              </Show>
            </div>

            <div>
              <Show when={selectedBotId()}>
                <div class="flex items-center justify-between mb-4">
                  <h3 class="text-lg font-semibold">Tokens</h3>
                  <button
                    onClick={() => setShowCreateToken(!showCreateToken())}
                    class="px-3 py-1 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
                  >
                    Create Token
                  </button>
                </div>

                <Show when={showCreateToken()}>
                  <form onSubmit={handleCreateToken} class="mb-4 p-4 bg-gray-50 rounded">
                    <div class="space-y-3">
                      <div>
                        <label class="block text-sm font-medium mb-1">Token Name</label>
                        <input
                          type="text"
                          value={tokenName()}
                          onInput={(e) => setTokenName(e.currentTarget.value)}
                          required
                          class="w-full px-3 py-2 border border-gray-300 rounded"
                          placeholder="e.g., Production Token"
                        />
                      </div>
                      <div>
                        <label class="block text-sm font-medium mb-1">Permissions (numeric)</label>
                        <input
                          type="number"
                          value={permissions()}
                          onInput={(e) => setPermissions(e.currentTarget.value)}
                          required
                          class="w-full px-3 py-2 border border-gray-300 rounded"
                          placeholder="0"
                        />
                      </div>
                      <div class="flex gap-2">
                        <button
                          type="submit"
                          class="px-3 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
                        >
                          Create
                        </button>
                        <button
                          type="button"
                          onClick={() => setShowCreateToken(false)}
                          class="px-3 py-2 bg-gray-200 rounded text-sm hover:bg-gray-300"
                        >
                          Cancel
                        </button>
                      </div>
                    </div>
                  </form>
                </Show>

                <Show when={isLoadingTokens()}>
                  <p class="text-gray-500">Loading tokens...</p>
                </Show>

                <Show when={!isLoadingTokens() && tokens().length === 0}>
                  <p class="text-gray-500">No tokens created yet</p>
                </Show>

                <Show when={!isLoadingTokens() && tokens().length > 0}>
                  <div class="space-y-2">
                    <For each={tokens()}>
                      {(token) => (
                        <div class="p-4 border border-gray-200 rounded">
                          <div class="flex items-start justify-between">
                            <div>
                              <p class="font-medium">{token.tokenName}</p>
                              <p class="text-xs text-gray-500">Permissions: {token.permissions}</p>
                              <p class="text-xs text-gray-500">Created: {new Date(token.createdAt).toLocaleDateString()}</p>
                              {token.lastUsedAt && (
                                <p class="text-xs text-gray-500">Last used: {new Date(token.lastUsedAt).toLocaleDateString()}</p>
                              )}
                            </div>
                            <button
                              onClick={() => handleRevokeToken(token.botId, token.id)}
                              class="px-2 py-1 bg-red-600 text-white rounded text-xs hover:bg-red-700"
                            >
                              Revoke
                            </button>
                          </div>
                        </div>
                      )}
                    </For>
                  </div>
                </Show>
              </Show>
            </div>
          </div>
        </>
      }>
        <p class="text-gray-500">Loading bots...</p>
      </Show>
    </div>
  );
}
