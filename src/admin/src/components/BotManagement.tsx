import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';
import type {
  BotTokenDto,
  ListBotsResponse,
  CreateBotRequest,
  CreateBotResponse,
  CreateBotTokenRequest,
  CreateBotTokenResponse,
  BotTokenMetadataDto,
  ListBotTokensResponse,
} from '../types/bot';

/**
 * Groups flat BotTokenDto list (from GET /api/v1/admin/bots) into
 * a per-bot structure keyed by userId.
 */
interface BotGroup {
  userId: string;
  username: string;
  tokens: BotTokenDto[];
}

function groupBotsByUser(bots: BotTokenDto[]): BotGroup[] {
  const map = new Map<string, BotGroup>();
  for (const bot of bots) {
    let group = map.get(bot.userId);
    if (!group) {
      group = { userId: bot.userId, username: bot.username, tokens: [] };
      map.set(bot.userId, group);
    }
    group.tokens.push(bot);
  }
  return Array.from(map.values());
}

export function BotManagement() {
  const [botGroups, setBotGroups] = createSignal<BotGroup[]>([]);
  const [selectedBotUserId, setSelectedBotUserId] = createSignal<string | null>(null);
  const [selectedBotTokens, setSelectedBotTokens] = createSignal<BotTokenMetadataDto[]>([]);
  const [isLoading, setIsLoading] = createSignal(true);
  const [isLoadingTokens, setIsLoadingTokens] = createSignal(false);
  const [error, setError] = createSignal('');

  // Create bot form
  const [showCreateBot, setShowCreateBot] = createSignal(false);
  const [newBotUsername, setNewBotUsername] = createSignal('');
  const [newBotDisplayName, setNewBotDisplayName] = createSignal('');
  const [newBotTokenName, setNewBotTokenName] = createSignal('');
  const [newBotPermissions, setNewBotPermissions] = createSignal('0');

  // Create token form
  const [showCreateToken, setShowCreateToken] = createSignal(false);
  const [tokenName, setTokenName] = createSignal('');
  const [tokenPermissions, setTokenPermissions] = createSignal('0');

  // Raw token display (shown once after creation)
  const [createdToken, setCreatedToken] = createSignal('');

  createEffect(() => {
    loadBots();
  });

  createEffect(() => {
    const botId = selectedBotUserId();
    if (botId) {
      loadTokens(botId);
    }
  });

  const loadBots = async () => {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.get<ListBotsResponse>('/api/v1/admin/bots');
      setBotGroups(groupBotsByUser(response.bots));
    } catch (err: any) {
      setError(err?.message || 'Failed to load bots');
    } finally {
      setIsLoading(false);
    }
  };

  const loadTokens = async (botUserId: string) => {
    setIsLoadingTokens(true);
    setError('');
    try {
      const response = await api.get<ListBotTokensResponse>(`/api/v1/admin/bots/${botUserId}/tokens`);
      setSelectedBotTokens(response.tokens);
    } catch (err: any) {
      setError(err?.message || 'Failed to load tokens');
    } finally {
      setIsLoadingTokens(false);
    }
  };

  const handleCreateBot = async (e: Event) => {
    e.preventDefault();
    setError('');
    try {
      const request: CreateBotRequest = {
        username: newBotUsername(),
        displayName: newBotDisplayName(),
        tokenName: newBotTokenName(),
        permissions: parseInt(newBotPermissions(), 10),
      };
      const response = await api.post<CreateBotResponse>('/api/v1/admin/bots', request);
      setCreatedToken(response.rawToken);
      setShowCreateBot(false);
      setNewBotUsername('');
      setNewBotDisplayName('');
      setNewBotTokenName('');
      setNewBotPermissions('0');
      await loadBots();
      setSelectedBotUserId(response.userId);
    } catch (err: any) {
      setError(err?.message || 'Failed to create bot');
    }
  };

  const handleCreateToken = async (e: Event) => {
    e.preventDefault();
    const botId = selectedBotUserId();
    if (!botId) return;

    setError('');
    try {
      const request: CreateBotTokenRequest = {
        tokenName: tokenName(),
        permissions: parseInt(tokenPermissions(), 10),
      };
      const response = await api.post<CreateBotTokenResponse>(`/api/v1/admin/bots/${botId}/tokens`, request);
      setCreatedToken(response.rawToken);
      setShowCreateToken(false);
      setTokenName('');
      setTokenPermissions('0');
      await loadTokens(botId);
    } catch (err: any) {
      setError(err?.message || 'Failed to create token');
    }
  };

  const handleRevokeToken = async (botUserId: string, tokenId: string) => {
    if (!confirm('Revoke this token? This cannot be undone.')) return;
    setError('');
    try {
      await api.delete(`/api/v1/admin/bots/${botUserId}/tokens/${tokenId}`);
      await loadTokens(botUserId);
    } catch (err: any) {
      setError(err?.message || 'Failed to revoke token');
    }
  };

  const handleDeleteBot = async (botUserId: string) => {
    if (!confirm('Delete this bot? All tokens will be revoked.')) return;
    setError('');
    try {
      await api.delete(`/api/v1/admin/bots/${botUserId}`);
      if (selectedBotUserId() === botUserId) {
        setSelectedBotUserId(null);
        setSelectedBotTokens([]);
      }
      await loadBots();
    } catch (err: any) {
      setError(err?.message || 'Failed to delete bot');
    }
  };

  return (
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold text-white">Bot Management</h2>
        <button
          onClick={() => setShowCreateBot(!showCreateBot())}
          class="px-4 py-2 bg-xcord-success text-white rounded text-sm font-medium hover:bg-xcord-success-hover transition-colors"
        >
          Create Bot
        </button>
      </div>

      {error() && (
        <div class="bg-xcord-danger/10 border border-xcord-danger/30 text-xcord-danger px-4 py-3 rounded text-sm mb-4">
          {error()}
        </div>
      )}

      {createdToken() && (
        <div class="bg-xcord-success/10 border border-xcord-success/30 px-4 py-3 rounded mb-4">
          <p class="font-semibold text-xcord-success mb-2">Token created successfully</p>
          <p class="text-xs text-xcord-text-secondary mb-2">Copy this token now -- it will not be shown again:</p>
          <code class="block bg-xcord-bg-tertiary p-3 rounded text-xs text-xcord-text-primary break-all font-mono select-all">
            {createdToken()}
          </code>
          <button
            onClick={() => setCreatedToken('')}
            class="mt-2 text-xs text-xcord-text-muted hover:text-xcord-text-primary underline"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Create Bot Form */}
      <Show when={showCreateBot()}>
        <form onSubmit={handleCreateBot} class="mb-6 p-4 bg-xcord-bg-secondary rounded-lg border border-xcord-border">
          <h3 class="text-sm font-semibold text-white mb-3">New Bot</h3>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Username</label>
              <input
                type="text"
                value={newBotUsername()}
                onInput={(e) => setNewBotUsername(e.currentTarget.value)}
                required
                maxLength={32}
                pattern="^[a-zA-Z0-9_]+$"
                class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
                placeholder="my_bot"
              />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Display Name</label>
              <input
                type="text"
                value={newBotDisplayName()}
                onInput={(e) => setNewBotDisplayName(e.currentTarget.value)}
                required
                maxLength={32}
                class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
                placeholder="My Bot"
              />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Initial Token Name</label>
              <input
                type="text"
                value={newBotTokenName()}
                onInput={(e) => setNewBotTokenName(e.currentTarget.value)}
                required
                maxLength={100}
                class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
                placeholder="default"
              />
            </div>
            <div>
              <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Permissions</label>
              <input
                type="number"
                value={newBotPermissions()}
                onInput={(e) => setNewBotPermissions(e.currentTarget.value)}
                required
                min={0}
                class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
              />
            </div>
          </div>
          <div class="flex gap-2 mt-4">
            <button
              type="submit"
              class="px-4 py-2 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand-hover transition-colors"
            >
              Create
            </button>
            <button
              type="button"
              onClick={() => setShowCreateBot(false)}
              class="px-4 py-2 bg-xcord-bg-input text-xcord-text-secondary rounded text-sm hover:text-white transition-colors"
            >
              Cancel
            </button>
          </div>
        </form>
      </Show>

      <Show when={!isLoading()} fallback={
        <div class="text-xcord-text-muted">Loading bots...</div>
      }>
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Bot list */}
          <div>
            <h3 class="text-sm font-semibold uppercase text-xcord-text-muted mb-3">Bots</h3>
            <Show when={botGroups().length > 0} fallback={
              <p class="text-xcord-text-muted text-sm">No bots created yet</p>
            }>
              <div class="space-y-2">
                <For each={botGroups()}>
                  {(group) => (
                    <div
                      class={`p-4 rounded-lg border cursor-pointer transition-colors ${
                        selectedBotUserId() === group.userId
                          ? 'border-xcord-brand bg-xcord-brand/10'
                          : 'border-xcord-border bg-xcord-bg-secondary hover:border-xcord-bg-input'
                      }`}
                    >
                      <div class="flex items-center justify-between">
                        <button
                          onClick={() => setSelectedBotUserId(group.userId)}
                          class="flex items-center gap-3 flex-1 text-left"
                        >
                          <div class="w-10 h-10 rounded-full bg-xcord-brand/30 flex items-center justify-center text-white font-bold text-sm">
                            {group.username.charAt(0).toUpperCase()}
                          </div>
                          <div>
                            <p class="font-medium text-white">{group.username}</p>
                            <p class="text-xs text-xcord-text-muted">
                              {group.tokens.length} token{group.tokens.length !== 1 ? 's' : ''} | ID: {group.userId}
                            </p>
                          </div>
                        </button>
                        <button
                          onClick={() => handleDeleteBot(group.userId)}
                          class="px-2 py-1 bg-xcord-danger/20 text-xcord-danger rounded text-xs hover:bg-xcord-danger/30 transition-colors"
                          title="Delete bot"
                        >
                          Delete
                        </button>
                      </div>
                    </div>
                  )}
                </For>
              </div>
            </Show>
          </div>

          {/* Token management for selected bot */}
          <div>
            <Show when={selectedBotUserId()}>
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-semibold uppercase text-xcord-text-muted">Tokens</h3>
                <button
                  onClick={() => setShowCreateToken(!showCreateToken())}
                  class="px-3 py-1.5 bg-xcord-brand text-white rounded text-xs font-medium hover:bg-xcord-brand-hover transition-colors"
                >
                  New Token
                </button>
              </div>

              {/* Create Token Form */}
              <Show when={showCreateToken()}>
                <form onSubmit={handleCreateToken} class="mb-4 p-4 bg-xcord-bg-secondary rounded-lg border border-xcord-border">
                  <div class="space-y-3">
                    <div>
                      <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Token Name</label>
                      <input
                        type="text"
                        value={tokenName()}
                        onInput={(e) => setTokenName(e.currentTarget.value)}
                        required
                        maxLength={100}
                        class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
                        placeholder="e.g., Production Token"
                      />
                    </div>
                    <div>
                      <label class="block text-xs font-semibold uppercase text-xcord-text-muted mb-1">Permissions</label>
                      <input
                        type="number"
                        value={tokenPermissions()}
                        onInput={(e) => setTokenPermissions(e.currentTarget.value)}
                        required
                        min={0}
                        class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary text-sm focus:outline-none focus:border-xcord-brand"
                      />
                    </div>
                    <div class="flex gap-2">
                      <button
                        type="submit"
                        class="px-3 py-2 bg-xcord-brand text-white rounded text-sm font-medium hover:bg-xcord-brand-hover transition-colors"
                      >
                        Create
                      </button>
                      <button
                        type="button"
                        onClick={() => setShowCreateToken(false)}
                        class="px-3 py-2 bg-xcord-bg-input text-xcord-text-secondary rounded text-sm hover:text-white transition-colors"
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                </form>
              </Show>

              <Show when={isLoadingTokens()}>
                <p class="text-xcord-text-muted text-sm">Loading tokens...</p>
              </Show>

              <Show when={!isLoadingTokens() && selectedBotTokens().length === 0}>
                <p class="text-xcord-text-muted text-sm">No tokens for this bot</p>
              </Show>

              <Show when={!isLoadingTokens() && selectedBotTokens().length > 0}>
                <div class="space-y-2">
                  <For each={selectedBotTokens()}>
                    {(token) => (
                      <div class="p-4 bg-xcord-bg-secondary rounded-lg border border-xcord-border">
                        <div class="flex items-start justify-between">
                          <div>
                            <div class="flex items-center gap-2">
                              <p class="font-medium text-white text-sm">{token.tokenName}</p>
                              {token.isRevoked && (
                                <span class="px-1.5 py-0.5 bg-xcord-danger/20 text-xcord-danger rounded text-[10px] font-semibold uppercase">
                                  Revoked
                                </span>
                              )}
                            </div>
                            <p class="text-xs text-xcord-text-muted mt-1">Permissions: {token.permissions}</p>
                            <p class="text-xs text-xcord-text-muted">Created: {new Date(token.createdAt).toLocaleString()}</p>
                            {token.lastUsedAt && (
                              <p class="text-xs text-xcord-text-muted">Last used: {new Date(token.lastUsedAt).toLocaleString()}</p>
                            )}
                          </div>
                          {!token.isRevoked && (
                            <button
                              onClick={() => handleRevokeToken(selectedBotUserId()!, token.tokenId)}
                              class="px-2 py-1 bg-xcord-danger text-white rounded text-xs hover:bg-xcord-danger-hover transition-colors"
                            >
                              Revoke
                            </button>
                          )}
                        </div>
                      </div>
                    )}
                  </For>
                </div>
              </Show>
            </Show>

            <Show when={!selectedBotUserId()}>
              <p class="text-xcord-text-muted text-sm">Select a bot to manage its tokens</p>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
}
