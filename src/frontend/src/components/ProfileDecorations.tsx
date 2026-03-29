import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import styles from './ProfileDecorations.module.css';

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
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 class={styles.heading}>Profile Decorations</h2>
      </div>

      <div class={styles.content}>
        {/* Category tabs */}
        <div class={styles.tabRow}>
          <For each={TABS}>
            {(tab) => (
              <button
                class={`${styles.tabButton}${activeTab() === tab.id ? ` ${styles.tabButtonActive}` : ''}`}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            )}
          </For>
        </div>

        <Show when={isLoading()}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading decorations...</p>
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Decoration grid */}
          <Show when={filteredDecorations().length > 0}>
            <div class={styles.decorationGrid}>
              <For each={filteredDecorations()}>
                {(deco) => (
                  <button
                    class={`${styles.decorationItem}${isSelected(deco.id) ? ` ${styles.decorationItemSelected}` : ''}`}
                    aria-label={`Select ${deco.name}`}
                    aria-pressed={isSelected(deco.id)}
                    onClick={() => handleSelect(deco.id)}
                  >
                    <Show when={deco.previewUrl} fallback={
                      <span class={styles.decorationItemLabel}>{deco.name}</span>
                    }>
                      <img
                        src={deco.previewUrl}
                        alt={deco.name}
                        class={styles.decorationPreviewImage}
                      />
                    </Show>
                  </button>
                )}
              </For>
            </div>
          </Show>

          <Show when={filteredDecorations().length === 0}>
            <p class={styles.emptyText}>
              No decorations available in this category.
            </p>
          </Show>

          {/* Success message */}
          <Show when={successMessage()}>
            <p class={styles.successText}>{successMessage()}</p>
          </Show>

          {/* Save button */}
          <button
            class={styles.saveButton}
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
