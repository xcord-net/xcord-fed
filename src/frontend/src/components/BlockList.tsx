import { For, Show, onMount, createSignal } from 'solid-js';
import { useBlocks } from '../stores/block.store';
import { getErrorMessage } from '../utils/errors';
import styles from './BlockList.module.css';

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
      setBlockMessage(getErrorMessage(err, 'Failed to block user'));
      setBlockError(true);
    }
  };

  return (
    <div class={styles.container} data-testid="block-list-container">
      <div class={styles.header}>
        <h2 class={styles.heading}>Blocked Users</h2>
        <div class={styles.inputRow}>
          <input
            id="block-user-input"
            data-testid="block-user-input"
            type="text"
            placeholder="Enter a username to block"
            value={blockUsername()}
            onInput={(e) => setBlockUsername(e.currentTarget.value)}
            class={styles.usernameInput}
          />
          <button
            id="block-user-submit"
            data-testid="block-user-submit-button"
            class={styles.blockButton}
            disabled={!blockUsername().trim()}
            onClick={handleBlockUser}
          >
            Block
          </button>
        </div>
        <Show when={blockMessage()}>
          <p id="block-user-status" data-testid="block-user-status" class={blockError() ? styles.statusError : styles.statusSuccess}>
            {blockMessage()}
          </p>
        </Show>
      </div>

      <div class={styles.listArea}>
        <Show when={blockStore.isLoading}>
          <div class={styles.loadingState}>
            <p class={styles.mutedText}>Loading...</p>
          </div>
        </Show>

        <Show when={!blockStore.isLoading && blockStore.blockedUsers.length === 0}>
          <div id="blocked-users-empty" data-testid="block-list-empty-state" class={styles.emptyState}>
            <p class={styles.mutedText}>No blocked users</p>
          </div>
        </Show>

        <For each={blockStore.blockedUsers}>
          {(user) => (
            <div class={styles.userRow} data-blocked-username={user.blockedUsername} data-testid={`blocked-user-item-${user.blockedUsername}`}>
              <div class={styles.avatar}>
                <Show when={user.blockedAvatarUrl} fallback={user.blockedUsername.charAt(0).toUpperCase()}>
                  <img
                    src={user.blockedAvatarUrl}
                    alt={user.blockedUsername}
                    class={styles.avatarImage}
                  />
                </Show>
              </div>

              <div class={styles.userInfo}>
                <h3 class={styles.username}>{user.blockedUsername}</h3>
                <p class={styles.blockedDate}>
                  Blocked {new Date(user.createdAt).toLocaleDateString()}
                </p>
              </div>

              <button
                class={styles.unblockButton}
                data-testid={`unblock-user-button-${user.blockedUsername}`}
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
