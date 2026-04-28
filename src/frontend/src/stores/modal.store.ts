import { createSignal, createRoot } from 'solid-js';
import type { ForumPost } from '../types/forum';

export type SettingsTab = 'profile' | 'blocks' | 'notifications' | 'notes';
export type ServerSettingsTab = 'overview' | 'automod' | 'bans' | 'audit-log' | 'emoji' | 'stickers' | 'vanity-url' | 'templates' | 'boost' | 'insights' | 'invites' | 'app-directory' | 'bots' | 'welcome-screen' | 'updates';

const store = createRoot(() => {
  const [showSearch, setShowSearch] = createSignal(false);
  const [showPins, setShowPins] = createSignal(false);
  const [showThreads, setShowThreads] = createSignal(false);
  const [showSettings, setShowSettings] = createSignal<SettingsTab | null>(null);
  const [showChannelSettings, setShowChannelSettings] = createSignal(false);
  const [showGroupManager, setShowGroupManager] = createSignal(false);
  const [showEvents, setShowEvents] = createSignal(false);
  const [showScheduledMessages, setShowScheduledMessages] = createSignal(false);
  const [selectedForumPost, setSelectedForumPost] = createSignal<ForumPost | null>(null);
  const [showServerSettings, setShowServerSettings] = createSignal(false);
  const [serverSettingsTab, setServerSettingsTab] = createSignal<ServerSettingsTab>('overview');

  return {
    showSearch, setShowSearch,
    showPins, setShowPins,
    showThreads, setShowThreads,
    showSettings, setShowSettings,
    showChannelSettings, setShowChannelSettings,
    showGroupManager, setShowGroupManager,
    showEvents, setShowEvents,
    showScheduledMessages, setShowScheduledMessages,
    selectedForumPost, setSelectedForumPost,
    showServerSettings, setShowServerSettings,
    serverSettingsTab, setServerSettingsTab,
  };
});

export function useModals() {
  return {
    get showSearch() { return store.showSearch(); },
    get showPins() { return store.showPins(); },
    get showThreads() { return store.showThreads(); },
    get showSettings() { return store.showSettings(); },
    get showChannelSettings() { return store.showChannelSettings(); },
    get showGroupManager() { return store.showGroupManager(); },
    get showEvents() { return store.showEvents(); },
    get showScheduledMessages() { return store.showScheduledMessages(); },
    get selectedForumPost() { return store.selectedForumPost(); },
    get showServerSettings() { return store.showServerSettings(); },
    get serverSettingsTab() { return store.serverSettingsTab(); },

    toggleSearch() { store.setShowSearch(!store.showSearch()); },
    togglePins() { store.setShowPins(!store.showPins()); },
    toggleThreads() { store.setShowThreads(!store.showThreads()); },
    toggleChannelSettings() { store.setShowChannelSettings(!store.showChannelSettings()); },
    toggleGroupManager() { store.setShowGroupManager(!store.showGroupManager()); },
    toggleEvents() { store.setShowEvents(!store.showEvents()); },
    toggleScheduledMessages() { store.setShowScheduledMessages(!store.showScheduledMessages()); },
    toggleSettings() { store.setShowSettings(store.showSettings() ? null : 'profile'); },

    openSettings(tab: SettingsTab) { store.setShowSettings(tab); },
    closeSettings() { store.setShowSettings(null); },
    closeChannelSettings() { store.setShowChannelSettings(false); },
    closeGroupManager() { store.setShowGroupManager(false); },

    selectForumPost(post: ForumPost | null) { store.setSelectedForumPost(post); },

    toggleServerSettings() { store.setShowServerSettings(!store.showServerSettings()); },
    openServerSettings(tab?: ServerSettingsTab) {
      if (tab) store.setServerSettingsTab(tab);
      store.setShowServerSettings(true);
    },
    closeServerSettings() { store.setShowServerSettings(false); },

    /** Close every open modal. Called on logout / navigation events that should
     *  reset modal state so a stale modal backdrop doesn't block the destination. */
    closeAll() {
      store.setShowSearch(false);
      store.setShowPins(false);
      store.setShowThreads(false);
      store.setShowSettings(null);
      store.setShowChannelSettings(false);
      store.setShowGroupManager(false);
      store.setShowEvents(false);
      store.setShowScheduledMessages(false);
      store.setSelectedForumPost(null);
      store.setShowServerSettings(false);
    },

    reset(): void {
      store.setShowSearch(false);
      store.setShowPins(false);
      store.setShowThreads(false);
      store.setShowSettings(null);
      store.setShowChannelSettings(false);
      store.setShowGroupManager(false);
      store.setShowEvents(false);
      store.setShowScheduledMessages(false);
      store.setSelectedForumPost(null);
      store.setShowServerSettings(false);
      store.setServerSettingsTab('overview');
    },
  };
}
