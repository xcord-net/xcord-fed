import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export type BotCategory =
  | 'Moderation'
  | 'Music'
  | 'Games'
  | 'Utility'
  | 'Fun'
  | 'Economy'
  | 'Social'
  | 'Productivity';

export interface BotListing {
  id: string;
  name: string;
  description: string;
  shortDescription: string;
  avatarUrl?: string;
  category: BotCategory;
  installCount: number;
  permissions: string[];
  isVerified: boolean;
  developerName: string;
  tags: string[];
}

export interface AppDirectoryResponse {
  bots: BotListing[];
  total: number;
}

interface AppDirectoryProps {
  availableServerIds: string[];
  serverNames: Record<string, string>;
}

// ---- Helpers ----

export function formatInstallCount(count: number): string {
  if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1)}M`;
  if (count >= 1_000) return `${(count / 1_000).toFixed(1)}K`;
  return String(count);
}

export function filterBots(bots: BotListing[] | undefined | null, query: string, category: BotCategory | ''): BotListing[] {
  if (!bots || !Array.isArray(bots)) return [];
  let result = bots;
  if (category) {
    result = result.filter((b) => b.category === category);
  }
  if (query.trim()) {
    const lower = query.toLowerCase();
    result = result.filter(
      (b) =>
        b.name.toLowerCase().includes(lower) ||
        (b.description ?? '').toLowerCase().includes(lower) ||
        (b.tags ?? []).some((t) => t.toLowerCase().includes(lower)),
    );
  }
  return result;
}

export function sortBotsByInstalls(bots: BotListing[] | undefined | null): BotListing[] {
  if (!bots || !Array.isArray(bots)) return [];
  return [...bots].sort((a, b) => b.installCount - a.installCount);
}

const ALL_CATEGORIES: BotCategory[] = [
  'Moderation',
  'Music',
  'Games',
  'Utility',
  'Fun',
  'Economy',
  'Social',
  'Productivity',
];

// ---- Component ----

export default function AppDirectory(props: AppDirectoryProps) {
  const [bots, setBots] = createSignal<BotListing[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [searchQuery, setSearchQuery] = createSignal('');
  const [selectedCategory, setSelectedCategory] = createSignal<BotCategory | ''>('');
  const [selectedBot, setSelectedBot] = createSignal<BotListing | null>(null);
  const [installTargetServerId, setInstallTargetServerId] = createSignal('');
  const [installing, setInstalling] = createSignal(false);
  const [installError, setInstallError] = createSignal<string | null>(null);
  const [installSuccess, setInstallSuccess] = createSignal<string | null>(null);

  const displayedBots = () =>
    sortBotsByInstalls(filterBots(bots(), searchQuery(), selectedCategory()));

  // Backend response type for a single app listing
  type BackendAppListing = {
    id: string;
    name: string;
    shortDescription?: string;
    iconUrl?: string;
    category?: string;
    installCount: number;
    isVerified: boolean;
    averageRating?: number;
  };

  // Load directory on mount — backend returns an array directly
  createEffect(() => {
    setIsLoading(true);
    api
      .get<BackendAppListing[] | AppDirectoryResponse>('/api/v1/app-directory')
      .then((data) => {
        // Backend returns either a plain array or a wrapped { bots: [] } object
        const rawList: BackendAppListing[] = Array.isArray(data)
          ? (data as BackendAppListing[])
          : ((data as AppDirectoryResponse)?.bots as unknown as BackendAppListing[] ?? []);

        const mapped: BotListing[] = rawList.map((app) => ({
          id: String(app.id),
          name: app.name,
          description: app.shortDescription ?? '',
          shortDescription: app.shortDescription ?? '',
          avatarUrl: app.iconUrl,
          category: (app.category ?? 'Utility') as BotCategory,
          installCount: app.installCount ?? 0,
          permissions: [],
          isVerified: app.isVerified ?? false,
          developerName: '',
          tags: [],
        }));
        setBots(mapped);
      })
      .catch(() => setBots([]))
      .finally(() => setIsLoading(false));
  });

  const handleInstall = async () => {
    const bot = selectedBot();
    const serverId = installTargetServerId();
    if (!bot || !serverId) return;

    setInstalling(true);
    setInstallError(null);
    setInstallSuccess(null);

    try {
      await api.post(`/api/v1/app-directory/${bot.id}/install`, { serverId });
      setInstallSuccess(`${bot.name} was added to ${props.serverNames[serverId] ?? serverId}!`);
    } catch {
      setInstallError('Failed to add bot. Please try again.');
    } finally {
      setInstalling(false);
    }
  };

  const handleSelectBot = (bot: BotListing) => {
    setSelectedBot(bot);
    setInstallTargetServerId(props.availableServerIds[0] ?? '');
    setInstallError(null);
    setInstallSuccess(null);
  };

  const handleBackToList = () => {
    setSelectedBot(null);
    setInstallError(null);
    setInstallSuccess(null);
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-primary flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold text-lg mb-1">App Directory</h2>
        <p class="text-xcord-text-muted text-xs">Discover bots to add to your server</p>
      </div>

      {/* Bot detail view */}
      <Show when={selectedBot()}>
        {(bot) => (
          <div class="flex-1 overflow-y-auto p-4 space-y-4">
            {/* Back */}
            <button
              class="text-xcord-brand hover:underline text-sm flex items-center gap-1"
              onClick={handleBackToList}
            >
              &larr; Back to directory
            </button>

            {/* Bot header */}
            <div class="flex items-start gap-4">
              <Show when={bot().avatarUrl}>
                <img
                  src={bot().avatarUrl}
                  alt={`${bot().name} avatar`}
                  class="w-16 h-16 rounded-full bg-xcord-bg-primary flex-shrink-0"
                />
              </Show>
              <Show when={!bot().avatarUrl}>
                <div class="w-16 h-16 rounded-full bg-xcord-bg-primary flex-shrink-0 flex items-center justify-center text-2xl text-xcord-text-muted">
                  {bot().name.charAt(0).toUpperCase()}
                </div>
              </Show>

              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <h3 class="text-xcord-text-primary font-bold text-xl">{bot().name}</h3>
                  <Show when={bot().isVerified}>
                    <span
                      class="bg-xcord-brand/20 text-xcord-brand text-xs px-2 py-0.5 rounded-full"
                      title="Verified bot"
                    >
                      Verified
                    </span>
                  </Show>
                  <span class="bg-xcord-bg-primary text-xcord-text-muted text-xs px-2 py-0.5 rounded-full">
                    {bot().category}
                  </span>
                </div>
                <p class="text-xcord-text-muted text-sm mt-0.5">by {bot().developerName}</p>
                <p class="text-xcord-text-muted text-xs mt-0.5">
                  {formatInstallCount(bot().installCount)} installs
                </p>
              </div>
            </div>

            {/* Description */}
            <div>
              <h4 class="text-xcord-text-primary font-semibold text-sm mb-1">About</h4>
              <p class="text-xcord-text-secondary text-sm leading-relaxed">{bot().description}</p>
            </div>

            {/* Tags */}
            <Show when={bot().tags.length > 0}>
              <div class="flex flex-wrap gap-1.5">
                <For each={bot().tags}>
                  {(tag) => (
                    <span class="bg-xcord-bg-primary text-xcord-text-muted text-xs px-2 py-0.5 rounded">
                      {tag}
                    </span>
                  )}
                </For>
              </div>
            </Show>

            {/* Permissions */}
            <Show when={bot().permissions.length > 0}>
              <div>
                <h4 class="text-xcord-text-primary font-semibold text-sm mb-1">
                  Required Permissions
                </h4>
                <ul class="space-y-1">
                  <For each={bot().permissions}>
                    {(perm) => (
                      <li class="text-xcord-text-muted text-xs flex items-center gap-1.5">
                        <span class="w-1 h-1 rounded-full bg-xcord-brand flex-shrink-0" />
                        {perm}
                      </li>
                    )}
                  </For>
                </ul>
              </div>
            </Show>

            {/* Add to server */}
            <div class="bg-xcord-bg-primary rounded-lg p-4 space-y-3">
              <h4 class="text-xcord-text-primary font-semibold text-sm">Add to Server</h4>

              <Show when={props.availableServerIds.length === 0}>
                <p class="text-xcord-text-muted text-sm">
                  You need to be an admin of at least one server to add bots.
                </p>
              </Show>

              <Show when={props.availableServerIds.length > 0}>
                <div>
                  <label class="block text-xcord-text-muted text-xs font-medium mb-1">
                    Select Server
                  </label>
                  <select
                    class="w-full bg-xcord-bg-secondary text-xcord-text-primary text-sm rounded px-3 py-2 outline-none focus:ring-1 focus:ring-xcord-brand"
                    value={installTargetServerId()}
                    onChange={(e) => {
                      setInstallTargetServerId(e.currentTarget.value);
                      setInstallError(null);
                      setInstallSuccess(null);
                    }}
                  >
                    <For each={props.availableServerIds}>
                      {(id) => (
                        <option value={id}>{props.serverNames[id] ?? id}</option>
                      )}
                    </For>
                  </select>
                </div>

                <Show when={installError()}>
                  <p class="text-red-400 text-xs">{installError()}</p>
                </Show>

                <Show when={installSuccess()}>
                  <p class="text-green-400 text-xs">{installSuccess()}</p>
                </Show>

                <button
                  class="w-full px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  disabled={installing() || !installTargetServerId()}
                  onClick={handleInstall}
                  aria-label={`Add ${bot().name} to server`}
                >
                  {installing() ? 'Adding...' : `Add ${bot().name}`}
                </button>
              </Show>
            </div>
          </div>
        )}
      </Show>

      {/* Bot list view */}
      <Show when={!selectedBot()}>
        {/* Search + filter bar */}
        <div class="px-4 py-3 border-b border-xcord-bg-primary space-y-2 flex-shrink-0">
          <input
            id="app-directory-search"
            type="search"
            class="w-full bg-xcord-bg-primary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
            placeholder="Search bots..."
            value={searchQuery()}
            onInput={(e) => setSearchQuery(e.currentTarget.value)}
            aria-label="Search bots"
          />

          {/* Category filter */}
          <div class="flex gap-1.5 overflow-x-auto pb-0.5">
            <button
              class={`flex-shrink-0 px-3 py-1 rounded-full text-xs transition-colors ${
                selectedCategory() === ''
                  ? 'bg-xcord-brand text-white'
                  : 'bg-xcord-bg-primary text-xcord-text-muted hover:text-xcord-text-primary'
              }`}
              onClick={() => setSelectedCategory('')}
            >
              All
            </button>
            <For each={ALL_CATEGORIES}>
              {(cat) => (
                <button
                  class={`flex-shrink-0 px-3 py-1 rounded-full text-xs transition-colors ${
                    selectedCategory() === cat
                      ? 'bg-xcord-brand text-white'
                      : 'bg-xcord-bg-primary text-xcord-text-muted hover:text-xcord-text-primary'
                  }`}
                  onClick={() => setSelectedCategory(cat)}
                >
                  {cat}
                </button>
              )}
            </For>
          </div>
        </div>

        {/* Loading */}
        <Show when={isLoading()}>
          <div class="flex items-center justify-center flex-1">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        {/* Empty state */}
        <Show when={!isLoading() && displayedBots().length === 0}>
          <div id="app-directory-empty" class="flex flex-col items-center justify-center flex-1 space-y-3">
            <div class="text-4xl text-xcord-text-muted">🤖</div>
            <p class="text-xcord-text-muted text-sm">No bots found</p>
            <Show when={searchQuery() || selectedCategory()}>
              <button
                class="text-xcord-brand hover:underline text-sm"
                onClick={() => {
                  setSearchQuery('');
                  setSelectedCategory('');
                }}
              >
                Clear filters
              </button>
            </Show>
          </div>
        </Show>

        {/* Bot grid */}
        <Show when={!isLoading() && displayedBots().length > 0}>
          <div class="flex-1 overflow-y-auto p-4">
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <For each={displayedBots()}>
                {(bot) => (
                  <div
                    class="bg-xcord-bg-primary rounded-lg p-4 flex gap-3 cursor-pointer hover:bg-xcord-bg-tertiary transition-colors"
                    onClick={() => handleSelectBot(bot)}
                    role="button"
                    tabIndex={0}
                    aria-label={`View ${bot.name} details`}
                    onKeyDown={(e) => e.key === 'Enter' && handleSelectBot(bot)}
                  >
                    {/* Avatar */}
                    <Show when={bot.avatarUrl}>
                      <img
                        src={bot.avatarUrl}
                        alt={`${bot.name} avatar`}
                        class="w-12 h-12 rounded-full flex-shrink-0"
                      />
                    </Show>
                    <Show when={!bot.avatarUrl}>
                      <div class="w-12 h-12 rounded-full bg-xcord-bg-secondary flex-shrink-0 flex items-center justify-center text-lg text-xcord-text-muted">
                        {bot.name.charAt(0).toUpperCase()}
                      </div>
                    </Show>

                    {/* Info */}
                    <div class="flex-1 min-w-0">
                      <div class="flex items-center gap-1.5 flex-wrap">
                        <span class="text-xcord-text-primary font-semibold text-sm truncate">
                          {bot.name}
                        </span>
                        <Show when={bot.isVerified}>
                          <span class="text-xcord-brand text-xs">&#10003;</span>
                        </Show>
                      </div>
                      <p class="text-xcord-text-muted text-xs mt-0.5 line-clamp-2">
                        {bot.shortDescription}
                      </p>
                      <div class="flex items-center gap-2 mt-1.5">
                        <span class="text-xcord-text-muted text-xs">
                          {formatInstallCount(bot.installCount)} installs
                        </span>
                        <span class="text-xcord-text-muted text-xs">·</span>
                        <span class="text-xcord-text-muted text-xs">{bot.category}</span>
                      </div>
                    </div>
                  </div>
                )}
              </For>
            </div>
          </div>
        </Show>
      </Show>
    </div>
  );
}
