import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import Flexbox from './ui/Flexbox';
import styles from './AppDirectory.module.css';

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

  // Load directory on mount - backend returns an array directly
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
    <Flexbox direction="vertical" class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>App Directory</h2>
        <p class={styles.headerSubtitle}>Discover bots to add to your server</p>
      </div>

      {/* Bot detail view */}
      <Show when={selectedBot()}>
        {(bot) => (
          <Flexbox direction="vertical" gap={1} class={styles.detailArea}>
            {/* Back */}
            <Flexbox as="button" align="center" gap={0.25} class={styles.backBtn} onClick={handleBackToList}>
              &larr; Back to directory
            </Flexbox>

            {/* Bot header */}
            <Flexbox align="start" gap={1}>
              <Show when={bot().avatarUrl}>
                <img
                  src={bot().avatarUrl}
                  alt={`${bot().name} avatar`}
                  class={styles.botAvatarImg}
                />
              </Show>
              <Show when={!bot().avatarUrl}>
                <div class={styles.botAvatarPlaceholder}>
                  {bot().name.charAt(0).toUpperCase()}
                </div>
              </Show>

              <div class={styles.botHeaderInfo}>
                <Flexbox align="center" gap={0.5} wrap="wrap">
                  <h3 class={styles.botDetailName}>{bot().name}</h3>
                  <Show when={bot().isVerified}>
                    <span
                      class={styles.verifiedBadge}
                      title="Verified bot"
                    >
                      Verified
                    </span>
                  </Show>
                  <span class={styles.categoryBadge}>
                    {bot().category}
                  </span>
                </Flexbox>
                <p class={styles.botDeveloper}>by {bot().developerName}</p>
                <p class={styles.botInstallCount}>
                  {formatInstallCount(bot().installCount)} installs
                </p>
              </div>
            </Flexbox>

            {/* Description */}
            <div>
              <h4 class={styles.sectionHeading}>About</h4>
              <p class={styles.botDescription}>{bot().description}</p>
            </div>

            {/* Tags */}
            <Show when={bot().tags.length > 0}>
              <Flexbox wrap="wrap" gap={0.375}>
                <For each={bot().tags}>
                  {(tag) => (
                    <span class={styles.tag}>
                      {tag}
                    </span>
                  )}
                </For>
              </Flexbox>
            </Show>

            {/* Permissions */}
            <Show when={bot().permissions.length > 0}>
              <div>
                <h4 class={styles.sectionHeading}>
                  Required Permissions
                </h4>
                <Flexbox as="ul" direction="vertical" gap={0.25} class={styles.permissionList}>
                  <For each={bot().permissions}>
                    {(perm) => (
                      <Flexbox as="li" align="center" gap={0.375} class={styles.permissionItem}>
                        <span class={styles.permissionDot} />
                        {perm}
                      </Flexbox>
                    )}
                  </For>
                </Flexbox>
              </div>
            </Show>

            {/* Add to server */}
            <Flexbox direction="vertical" gap={0.75} class={styles.addToServerBox}>
              <h4 class={styles.sectionHeading}>Add to Server</h4>

              <Show when={props.availableServerIds.length === 0}>
                <p class={styles.noAdminText}>
                  You need to be an admin of at least one server to add bots.
                </p>
              </Show>

              <Show when={props.availableServerIds.length > 0}>
                <div>
                  <label class={styles.selectLabel}>
                    Select Server
                  </label>
                  <select
                    class={styles.serverSelect}
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
                  <p class={styles.installError}>{installError()}</p>
                </Show>

                <Show when={installSuccess()}>
                  <p class={styles.installSuccess}>{installSuccess()}</p>
                </Show>

                <button
                  class={styles.installBtn}
                  disabled={installing() || !installTargetServerId()}
                  onClick={handleInstall}
                  aria-label={`Add ${bot().name} to server`}
                >
                  {installing() ? 'Adding...' : `Add ${bot().name}`}
                </button>
              </Show>
            </Flexbox>
          </Flexbox>
        )}
      </Show>

      {/* Bot list view */}
      <Show when={!selectedBot()}>
        {/* Search + filter bar */}
        <Flexbox direction="vertical" gap={0.5} class={styles.filterBar}>
          <input
            id="app-directory-search"
            type="search"
            class={styles.searchInput}
            placeholder="Search bots..."
            value={searchQuery()}
            onInput={(e) => setSearchQuery(e.currentTarget.value)}
            aria-label="Search bots"
          />

          {/* Category filter */}
          <Flexbox gap={0.375} class={styles.categoryBar}>
            <button
              class={selectedCategory() === '' ? `${styles.categoryBtn} ${styles.categoryBtnActive}` : styles.categoryBtn}
              onClick={() => setSelectedCategory('')}
            >
              All
            </button>
            <For each={ALL_CATEGORIES}>
              {(cat) => (
                <button
                  class={selectedCategory() === cat ? `${styles.categoryBtn} ${styles.categoryBtnActive}` : styles.categoryBtn}
                  onClick={() => setSelectedCategory(cat)}
                >
                  {cat}
                </button>
              )}
            </For>
          </Flexbox>
        </Flexbox>

        {/* Loading */}
        <Show when={isLoading()}>
          <Flexbox align="center" justify="center" class={styles.loadingCenter}>
            <div class={styles.spinner} />
          </Flexbox>
        </Show>

        {/* Empty state */}
        <Show when={!isLoading() && displayedBots().length === 0}>
          <Flexbox id="app-directory-empty" direction="vertical" align="center" justify="center" gap={0.75} class={styles.emptyState}>
            <div class={styles.emptyIcon}>🤖</div>
            <p class={styles.emptyText}>No bots found</p>
            <Show when={searchQuery() || selectedCategory()}>
              <button
                class={styles.clearFiltersBtn}
                onClick={() => {
                  setSearchQuery('');
                  setSelectedCategory('');
                }}
              >
                Clear filters
              </button>
            </Show>
          </Flexbox>
        </Show>

        {/* Bot grid */}
        <Show when={!isLoading() && displayedBots().length > 0}>
          <div class={styles.gridArea}>
            <div class={styles.botGrid}>
              <For each={displayedBots()}>
                {(bot) => (
                  <Flexbox
                    gap={0.75}
                    class={styles.botCard}
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
                        class={styles.botCardAvatarImg}
                      />
                    </Show>
                    <Show when={!bot.avatarUrl}>
                      <div class={styles.botCardAvatarPlaceholder}>
                        {bot.name.charAt(0).toUpperCase()}
                      </div>
                    </Show>

                    {/* Info */}
                    <div class={styles.botCardInfo}>
                      <Flexbox align="center" gap={0.375} wrap="wrap">
                        <span class={styles.botCardName}>
                          {bot.name}
                        </span>
                        <Show when={bot.isVerified}>
                          <span class={styles.verifiedCheck}>&#10003;</span>
                        </Show>
                      </Flexbox>
                      <p class={styles.botCardDesc}>
                        {bot.shortDescription}
                      </p>
                      <Flexbox align="center" gap={0.5} class={styles.botCardMeta}>
                        <span class={styles.botCardMetaText}>
                          {formatInstallCount(bot.installCount)} installs
                        </span>
                        <span class={styles.botCardMetaText}>·</span>
                        <span class={styles.botCardMetaText}>{bot.category}</span>
                      </Flexbox>
                    </div>
                  </Flexbox>
                )}
              </For>
            </div>
          </div>
        </Show>
      </Show>
    </Flexbox>
  );
}
