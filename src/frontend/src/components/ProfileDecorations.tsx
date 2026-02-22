import { For, Show, createSignal, onMount, createMemo } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export type DecorationType = 'Banner' | 'AvatarFrame' | 'ProfileEffect';

export interface Decoration {
  id: string;
  type: DecorationType;
  name: string;
  previewUrl: string;
  animated: boolean;
  rarity?: 'Common' | 'Rare' | 'Epic' | 'Legendary';
}

export interface UserProfileDecorations {
  bannerId?: string;
  avatarFrameId?: string;
  profileEffectId?: string;
  bannerColor?: string;
}

interface ProfileDecorationsProps {
  userId?: string;
  onSaved?: (decorations: UserProfileDecorations) => void;
}

// ---- Helpers ----

export function groupDecorationsByType(
  decorations: Decoration[],
): Record<DecorationType, Decoration[]> {
  const result: Record<DecorationType, Decoration[]> = {
    Banner: [],
    AvatarFrame: [],
    ProfileEffect: [],
  };
  for (const d of decorations) {
    result[d.type].push(d);
  }
  return result;
}

export function buildProfilePayload(
  decorations: UserProfileDecorations,
): Record<string, unknown> {
  return {
    bannerId: decorations.bannerId ?? null,
    avatarFrameId: decorations.avatarFrameId ?? null,
    profileEffectId: decorations.profileEffectId ?? null,
    bannerColor: decorations.bannerColor ?? null,
  };
}

export function countAnimatedDecorations(decorations: Decoration[]): number {
  return decorations.filter((d) => d.animated).length;
}

export function filterByType(
  decorations: Decoration[],
  type: DecorationType,
): Decoration[] {
  return decorations.filter((d) => d.type === type);
}

const rarityColors: Record<NonNullable<Decoration['rarity']>, string> = {
  Common: 'text-gray-400',
  Rare: 'text-blue-400',
  Epic: 'text-purple-400',
  Legendary: 'text-yellow-400',
};

export function getRarityColor(rarity: Decoration['rarity']): string {
  if (!rarity) return 'text-xcord-text-muted';
  return rarityColors[rarity] ?? 'text-xcord-text-muted';
}

// ---- Component ----

export default function ProfileDecorations(props: ProfileDecorationsProps) {
  const [availableDecorations, setAvailableDecorations] = createSignal<Decoration[]>([]);
  const [selected, setSelected] = createSignal<UserProfileDecorations>({});
  const [activeTab, setActiveTab] = createSignal<DecorationType>('Banner');
  const [isLoading, setIsLoading] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [saveSuccess, setSaveSuccess] = createSignal(false);

  const grouped = createMemo(() => groupDecorationsByType(availableDecorations()));
  const tabItems = () => grouped()[activeTab()] ?? [];

  const loadDecorations = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const decorations = await api.get<Decoration[]>('/api/v1/decorations');
      setAvailableDecorations(decorations);
    } catch {
      setError('Failed to load decorations.');
      setAvailableDecorations([]);
    } finally {
      setIsLoading(false);
    }
  };

  onMount(() => {
    loadDecorations();
  });

  const isSelected = (decoration: Decoration): boolean => {
    const sel = selected();
    switch (decoration.type) {
      case 'Banner':
        return sel.bannerId === decoration.id;
      case 'AvatarFrame':
        return sel.avatarFrameId === decoration.id;
      case 'ProfileEffect':
        return sel.profileEffectId === decoration.id;
      default:
        return false;
    }
  };

  const selectDecoration = (decoration: Decoration) => {
    setSelected((prev) => {
      const next = { ...prev };
      // Toggle off if already selected
      switch (decoration.type) {
        case 'Banner':
          next.bannerId = prev.bannerId === decoration.id ? undefined : decoration.id;
          break;
        case 'AvatarFrame':
          next.avatarFrameId = prev.avatarFrameId === decoration.id ? undefined : decoration.id;
          break;
        case 'ProfileEffect':
          next.profileEffectId =
            prev.profileEffectId === decoration.id ? undefined : decoration.id;
          break;
      }
      return next;
    });
  };

  const saveDecorations = async () => {
    setIsSaving(true);
    setError(null);
    setSaveSuccess(false);
    try {
      const payload = buildProfilePayload(selected());
      await api.put('/api/v1/users/@me/profile', payload);
      setSaveSuccess(true);
      props.onSaved?.(selected());
      setTimeout(() => setSaveSuccess(false), 2500);
    } catch {
      setError('Failed to save decorations. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const tabs: DecorationType[] = ['Banner', 'AvatarFrame', 'ProfileEffect'];
  const tabLabels: Record<DecorationType, string> = {
    Banner: 'Profile Banners',
    AvatarFrame: 'Avatar Frames',
    ProfileEffect: 'Profile Effects',
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Profile Decorations</h2>
        <p class="text-xcord-text-muted text-xs mt-0.5">
          Customize your profile with banners, avatar frames, and effects.
        </p>
      </div>

      {/* Tabs */}
      <div class="flex border-b border-xcord-bg-tertiary flex-shrink-0">
        <For each={tabs}>
          {(tab) => (
            <button
              class={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
                activeTab() === tab
                  ? 'border-xcord-brand text-xcord-brand'
                  : 'border-transparent text-xcord-text-muted hover:text-xcord-text-primary'
              }`}
              onClick={() => setActiveTab(tab)}
              aria-label={tabLabels[tab]}
            >
              {tabLabels[tab]}
            </button>
          )}
        </For>
      </div>

      {/* Loading */}
      <Show when={isLoading()}>
        <div class="flex items-center justify-center flex-1">
          <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      <Show when={error()}>
        <p class="text-red-400 text-xs px-4 py-2 flex-shrink-0">{error()}</p>
      </Show>

      {/* Decoration grid */}
      <Show when={!isLoading()}>
        <div class="flex-1 overflow-y-auto p-4">
          <Show when={tabItems().length === 0}>
            <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
              <p class="text-sm">No {tabLabels[activeTab()].toLowerCase()} available</p>
            </div>
          </Show>

          <div class="grid grid-cols-3 gap-3">
            <For each={tabItems()}>
              {(decoration) => (
                <button
                  class={`relative rounded-lg overflow-hidden border-2 transition-all aspect-square cursor-pointer ${
                    isSelected(decoration)
                      ? 'border-xcord-brand ring-2 ring-xcord-brand/50'
                      : 'border-xcord-bg-tertiary hover:border-xcord-text-muted'
                  }`}
                  onClick={() => selectDecoration(decoration)}
                  aria-label={`Select ${decoration.name}`}
                  aria-pressed={isSelected(decoration)}
                >
                  {/* Preview image */}
                  <img
                    src={decoration.previewUrl}
                    alt={decoration.name}
                    class="w-full h-full object-cover"
                  />

                  {/* Animated badge */}
                  <Show when={decoration.animated}>
                    <span class="absolute top-1 right-1 bg-xcord-brand text-white text-xs px-1 rounded">
                      GIF
                    </span>
                  </Show>

                  {/* Selected checkmark */}
                  <Show when={isSelected(decoration)}>
                    <div class="absolute inset-0 bg-xcord-brand/20 flex items-center justify-center">
                      <span class="text-white text-2xl">&#10003;</span>
                    </div>
                  </Show>

                  {/* Decoration name + rarity */}
                  <div class="absolute bottom-0 inset-x-0 bg-black/60 px-1.5 py-1">
                    <p class="text-white text-xs font-medium truncate">{decoration.name}</p>
                    <Show when={decoration.rarity}>
                      <p class={`text-xs ${getRarityColor(decoration.rarity)}`}>
                        {decoration.rarity}
                      </p>
                    </Show>
                  </div>
                </button>
              )}
            </For>
          </div>
        </div>

        {/* Save button */}
        <div class="px-4 py-3 border-t border-xcord-bg-tertiary flex items-center gap-3 flex-shrink-0">
          <button
            class="bg-xcord-brand text-white px-4 py-2 rounded text-sm font-medium hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            onClick={saveDecorations}
            disabled={isSaving()}
            aria-label="Save decorations"
          >
            {isSaving() ? 'Saving...' : 'Save Changes'}
          </button>

          <Show when={saveSuccess()}>
            <span class="text-green-400 text-sm">Saved!</span>
          </Show>
        </div>
      </Show>
    </div>
  );
}

export { rarityColors };
