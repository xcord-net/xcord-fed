import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

type DecorationCategory = 'banners' | 'frames' | 'effects';

interface Decoration {
  id: string;
  name: string;
  category: DecorationCategory;
  previewUrl?: string;
}

interface UserDecorations {
  bannerId?: string;
  frameId?: string;
  effectId?: string;
}

// ---- Component ----

export default function ProfileDecorations() {
  const [activeTab, setActiveTab] = createSignal<DecorationCategory>('banners');
  const [decorations, setDecorations] = createSignal<Decoration[]>([]);
  const [userDecorations, setUserDecorations] = createSignal<UserDecorations>({});
  const [isLoading, setIsLoading] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [successMessage, setSuccessMessage] = createSignal<string | null>(null);

  // Pending selections (applied on save)
  const [selectedBanner, setSelectedBanner] = createSignal<string | undefined>(undefined);
  const [selectedFrame, setSelectedFrame] = createSignal<string | undefined>(undefined);
  const [selectedEffect, setSelectedEffect] = createSignal<string | undefined>(undefined);

  const loadDecorations = async () => {
    setIsLoading(true);
    try {
      const [decos, userDecos] = await Promise.all([
        api.get<Decoration[]>('/api/v1/profile-decorations').catch(() => [] as Decoration[]),
        api.get<UserDecorations>('/api/v1/users/@me/decorations').catch(() => ({} as UserDecorations)),
      ]);
      setDecorations(decos ?? []);
      setUserDecorations(userDecos ?? {});
      setSelectedBanner(userDecos?.bannerId);
      setSelectedFrame(userDecos?.frameId);
      setSelectedEffect(userDecos?.effectId);
    } catch {
      setDecorations([]);
      setUserDecorations({});
    } finally {
      setIsLoading(false);
    }
  };

  onMount(() => {
    loadDecorations();
  });

  const filteredDecorations = () =>
    decorations().filter((d) => d.category === activeTab());

  const isSelected = (id: string) => {
    switch (activeTab()) {
      case 'banners': return selectedBanner() === id;
      case 'frames': return selectedFrame() === id;
      case 'effects': return selectedEffect() === id;
    }
  };

  const handleSelect = (id: string) => {
    switch (activeTab()) {
      case 'banners':
        setSelectedBanner(selectedBanner() === id ? undefined : id);
        break;
      case 'frames':
        setSelectedFrame(selectedFrame() === id ? undefined : id);
        break;
      case 'effects':
        setSelectedEffect(selectedEffect() === id ? undefined : id);
        break;
    }
  };

  const handleSave = async () => {
    setIsSaving(true);
    setSuccessMessage(null);
    try {
      await api.put('/api/v1/users/@me/decorations', {
        bannerId: selectedBanner() || null,
        frameId: selectedFrame() || null,
        effectId: selectedEffect() || null,
      });
      setSuccessMessage('Saved!');
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch {
      // Keep current state on error
    } finally {
      setIsSaving(false);
    }
  };

  const TABS: { id: DecorationCategory; label: string }[] = [
    { id: 'banners', label: 'Profile Banners' },
    { id: 'frames', label: 'Avatar Frames' },
    { id: 'effects', label: 'Profile Effects' },
  ];

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Profile Decorations</h2>
      </div>

      <div class="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Category tabs */}
        <div class="flex gap-2">
          <For each={TABS}>
            {(tab) => (
              <button
                class={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  activeTab() === tab.id
                    ? 'bg-xcord-brand text-white'
                    : 'bg-xcord-bg-primary text-xcord-text-muted hover:text-white'
                }`}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            )}
          </For>
        </div>

        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading decorations...</p>
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Decoration grid */}
          <Show when={filteredDecorations().length > 0}>
            <div class="grid grid-cols-4 gap-3">
              <For each={filteredDecorations()}>
                {(deco) => (
                  <button
                    class={`aspect-square rounded-lg border-2 transition-colors flex items-center justify-center text-sm ${
                      isSelected(deco.id)
                        ? 'border-xcord-brand bg-xcord-brand/10'
                        : 'border-xcord-border bg-xcord-bg-primary hover:border-xcord-brand/40'
                    }`}
                    aria-label={`Select ${deco.name}`}
                    aria-pressed={isSelected(deco.id)}
                    onClick={() => handleSelect(deco.id)}
                  >
                    <Show when={deco.previewUrl} fallback={
                      <span class="text-xcord-text-muted text-xs">{deco.name}</span>
                    }>
                      <img
                        src={deco.previewUrl}
                        alt={deco.name}
                        class="w-full h-full object-cover rounded-lg"
                      />
                    </Show>
                  </button>
                )}
              </For>
            </div>
          </Show>

          <Show when={filteredDecorations().length === 0}>
            <p class="text-xcord-text-muted text-sm text-center py-8">
              No decorations available in this category.
            </p>
          </Show>

          {/* Success message */}
          <Show when={successMessage()}>
            <p class="text-green-400 text-sm">{successMessage()}</p>
          </Show>

          {/* Save button */}
          <button
            class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand/80 transition-colors disabled:opacity-50"
            onClick={handleSave}
            disabled={isSaving()}
            aria-label="Save decorations"
          >
            {isSaving() ? 'Saving...' : 'Save Changes'}
          </button>
        </Show>
      </div>
    </div>
  );
}
