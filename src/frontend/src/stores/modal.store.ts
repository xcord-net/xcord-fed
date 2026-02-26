import { createSignal, createRoot } from 'solid-js';
import type { ForumPost } from '../types/forum';

export type SettingsTab = 'profile' | 'blocks' | 'notifications' | 'notes' | 'connected-accounts' | 'profile-decorations';

const store = createRoot(() => {
  const [showSearch, setShowSearch] = createSignal(false);
  const [showPins, setShowPins] = createSignal(false);
  const [showThreads, setShowThreads] = createSignal(false);
  const [showSettings, setShowSettings] = createSignal<SettingsTab | null>(null);
  const [showChannelSettings, setShowChannelSettings] = createSignal(false);
  const [showRoleManager, setShowRoleManager] = createSignal(false);
  const [showEvents, setShowEvents] = createSignal(false);
  const [selectedForumPost, setSelectedForumPost] = createSignal<ForumPost | null>(null);

  return {
    showSearch, setShowSearch,
    showPins, setShowPins,
    showThreads, setShowThreads,
    showSettings, setShowSettings,
    showChannelSettings, setShowChannelSettings,
    showRoleManager, setShowRoleManager,
    showEvents, setShowEvents,
    selectedForumPost, setSelectedForumPost,
  };
});

export function useModals() {
  return {
    get showSearch() { return store.showSearch(); },
    get showPins() { return store.showPins(); },
    get showThreads() { return store.showThreads(); },
    get showSettings() { return store.showSettings(); },
    get showChannelSettings() { return store.showChannelSettings(); },
    get showRoleManager() { return store.showRoleManager(); },
    get showEvents() { return store.showEvents(); },
    get selectedForumPost() { return store.selectedForumPost(); },

    toggleSearch() { store.setShowSearch(!store.showSearch()); },
    togglePins() { store.setShowPins(!store.showPins()); },
    toggleThreads() { store.setShowThreads(!store.showThreads()); },
    toggleChannelSettings() { store.setShowChannelSettings(!store.showChannelSettings()); },
    toggleRoleManager() { store.setShowRoleManager(!store.showRoleManager()); },
    toggleEvents() { store.setShowEvents(!store.showEvents()); },
    toggleSettings() { store.setShowSettings(store.showSettings() ? null : 'profile'); },

    openSettings(tab: SettingsTab) { store.setShowSettings(tab); },
    closeSettings() { store.setShowSettings(null); },
    closeChannelSettings() { store.setShowChannelSettings(false); },
    closeRoleManager() { store.setShowRoleManager(false); },

    selectForumPost(post: ForumPost | null) { store.setSelectedForumPost(post); },
  };
}
