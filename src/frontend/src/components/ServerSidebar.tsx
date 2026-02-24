import { createSignal, For, Show } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useAuth } from '../stores/auth.store';
import { useProfiles } from '../stores/profile.store';
import CreateServerModal from './CreateServerModal';
import StatusPicker from './StatusPicker';

export default function ServerSidebar() {
  const navigate = useNavigate();
  const serverStore = useServers();
  const channelStore = useChannels();
  const authStore = useAuth();
  const profileStore = useProfiles();
  const [showCreateModal, setShowCreateModal] = createSignal(false);
  const [focusedIndex, setFocusedIndex] = createSignal(-1);

  const navigateToServer = async (serverId: string) => {
    serverStore.selectServer(serverId);
    // Fetch channels for the target server and navigate to the first one
    await channelStore.fetchChannels(serverId);
    const channels = channelStore.channels;
    if (channels.length > 0) {
      navigate(`/channels/${serverId}/${channels[0].id}`);
    } else {
      navigate(`/channels/${serverId}`);
    }
  };

  const handleServerListKeyDown = (e: KeyboardEvent) => {
    const servers = serverStore.servers;
    if (servers.length === 0) return;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setFocusedIndex((i) => Math.min(i + 1, servers.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setFocusedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Home') {
      e.preventDefault();
      setFocusedIndex(0);
    } else if (e.key === 'End') {
      e.preventDefault();
      setFocusedIndex(servers.length - 1);
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      const idx = focusedIndex();
      if (idx >= 0 && idx < servers.length) {
        serverStore.selectServer(servers[idx].id);
      }
    }
  };

  return (
    <div class="w-16 bg-xcord-bg-tertiary flex flex-col items-center py-3 space-y-2">
      {/* Home button */}
      <button
        class="w-12 h-12 rounded-full bg-xcord-brand hover:bg-xcord-brand-hover flex items-center justify-center text-white font-semibold transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
        aria-label="Home"
        onClick={() => { serverStore.selectServer(null); navigate('/channels/me'); }}
        onFocus={() => setFocusedIndex(-1)}
      >
        H
      </button>

      <div class="w-8 h-0.5 bg-xcord-border" role="separator" />

      {/* Loading skeleton — show while fetching servers */}
      <Show when={serverStore.isLoading && serverStore.servers.length === 0}>
        <For each={[0, 1, 2]}>
          {() => (
            <div class="w-12 h-12 rounded-full bg-xcord-bg-primary animate-pulse" aria-hidden="true" />
          )}
        </For>
      </Show>

      {/* Server icons list */}
      <div
        role="listbox"
        aria-label="Servers"
        aria-orientation="vertical"
        class="flex flex-col items-center space-y-2 w-full"
        onKeyDown={handleServerListKeyDown}
      >
        <For each={serverStore.servers}>
          {(server, index) => (
            <button
              role="option"
              aria-selected={serverStore.selectedServerId === server.id}
              aria-label={server.name}
              tabindex={focusedIndex() === index() ? 0 : -1}
              class={`w-12 h-12 rounded-full flex items-center justify-center font-semibold transition-all relative group focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none ${
                serverStore.selectedServerId === server.id
                  ? 'bg-xcord-brand text-white rounded-2xl'
                  : 'bg-xcord-bg-primary hover:bg-xcord-brand hover:rounded-2xl text-xcord-text-primary'
              }`}
              onClick={() => {
                setFocusedIndex(index());
                void navigateToServer(server.id);
              }}
              onFocus={() => setFocusedIndex(index())}
              title={server.name}
            >
              <Show when={server.iconUrl} fallback={server.name.charAt(0).toUpperCase()}>
                <img src={server.iconUrl} alt={server.name} class="w-full h-full rounded-full object-cover" />
              </Show>
              {serverStore.selectedServerId === server.id && (
                <div class="absolute left-0 w-1 h-10 bg-white rounded-r" aria-hidden="true" />
              )}
            </button>
          )}
        </For>
      </div>

      {/* Add server button */}
      <button
        class="w-12 h-12 rounded-full bg-xcord-bg-primary hover:bg-green-600 hover:rounded-2xl flex items-center justify-center text-green-500 hover:text-white text-2xl font-semibold transition-all focus-visible:ring-2 focus-visible:ring-green-400 focus-visible:outline-none"
        title="Add a Server"
        aria-label="Add a Server"
        onClick={() => setShowCreateModal(true)}
        onFocus={() => setFocusedIndex(-1)}
      >
        +
      </button>

      {/* Current user avatar with status picker */}
      <Show when={profileStore.userProfile}>
        {(profile) => (
          <div
            id="current-user-bar"
            class="mt-auto relative"
            aria-label={`Current user: ${profile().username}`}
            title={profile().displayName || profile().username}
          >
            <div class="w-12 h-12 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold overflow-hidden">
              <Show when={profile().avatarUrl} fallback={<span>{profile().username.charAt(0).toUpperCase()}</span>}>
                <img
                  src={profile().avatarUrl}
                  alt={profile().username}
                  class="w-full h-full object-cover"
                />
              </Show>
            </div>
            <StatusPicker />
          </div>
        )}
      </Show>

      {/* Log out button */}
      <button
        class="w-12 h-12 rounded-full bg-xcord-bg-primary hover:bg-red-600 flex items-center justify-center text-xcord-text-muted hover:text-white transition-all focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
        aria-label="Log Out"
        title="Log Out"
        onClick={() => authStore.logout()}
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="w-5 h-5" aria-hidden="true">
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
          <polyline points="16 17 21 12 16 7" />
          <line x1="21" y1="12" x2="9" y2="12" />
        </svg>
      </button>

      <Show when={showCreateModal()}>
        <CreateServerModal onClose={() => setShowCreateModal(false)} />
      </Show>
    </div>
  );
}
