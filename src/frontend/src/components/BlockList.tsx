import { For, Show, onMount, createSignal } from 'solid-js';
import { useBlocks } from '../stores/block.store';

export default function BlockList() {
  const blockStore = useBlocks();
  const [blockUsername, setBlockUsername] = createSignal('');
  const [blockMessage, setBlockMessage] = createSignal('');
  const [blockError, setBlockError] = createSignal(false);

  onMount(() => {
    blockStore.loadBlockedUsers();
  });

  const handleBlockUser = async () => {
    try {
      await blockStore.blockUserByUsername(blockUsername());
      setBlockMessage('User blocked.');
      setBlockError(false);
      setBlockUsername('');
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setBlockMessage(e?.detail || e?.message || 'Failed to block user');
      setBlockError(true);
    }
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Blocked Users</h2>
        <div class="mt-3 flex space-x-2">
          <input
            id="block-user-input"
            type="text"
            placeholder="Enter a username to block"
            value={blockUsername()}
            onInput={(e) => setBlockUsername(e.currentTarget.value)}
            class="flex-1 bg-xcord-bg-primary text-white px-3 py-1.5 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
          />
          <button
            class="bg-red-600 text-white px-3 py-1.5 rounded text-sm hover:bg-red-700 disabled:opacity-50"
            disabled={!blockUsername().trim()}
            onClick={handleBlockUser}
          >
            Block
          </button>
        </div>
        <Show when={blockMessage()}>
          <p class={`text-sm mt-2 ${blockError() ? 'text-red-400' : 'text-green-400'}`}>
            {blockMessage()}
          </p>
        </Show>
      </div>

      <div class="flex-1 overflow-y-auto">
        <Show when={blockStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={!blockStore.isLoading && blockStore.blockedUsers.length === 0}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">No blocked users</p>
          </div>
        </Show>

        <For each={blockStore.blockedUsers}>
          {(user) => (
            <div class="px-4 py-3 flex items-center space-x-3 hover:bg-xcord-bg-primary/50 border-b border-xcord-border">
              <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                <Show when={user.blockedAvatarUrl} fallback={user.blockedUsername.charAt(0).toUpperCase()}>
                  <img
                    src={user.blockedAvatarUrl}
                    alt={user.blockedUsername}
                    class="w-full h-full rounded-full object-cover"
                  />
                </Show>
              </div>

              <div class="flex-1 min-w-0">
                <h3 class="text-white font-medium truncate">{user.blockedUsername}</h3>
                <p class="text-xs text-xcord-text-muted">
                  Blocked {new Date(user.createdAt).toLocaleDateString()}
                </p>
              </div>

              <button
                class="bg-xcord-brand text-white px-3 py-1 rounded hover:bg-xcord-brand/80"
                onClick={() => blockStore.unblockUser(user.blockedId)}
              >
                Unblock
              </button>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
