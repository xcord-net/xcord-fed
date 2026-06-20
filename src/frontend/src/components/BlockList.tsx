import { For, Show, onMount, createSignal } from 'solid-js';
import { useBlocks } from '../stores/block.store';
import { getErrorMessage } from '../utils/errors';
import Flexbox from './ui/Flexbox';
import styles from './BlockList.module.css';
import { formatDate } from '../utils/datetime';

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
    <Flexbox direction="vertical" class={styles.container} data-testid="block-list-container">
      <div class={styles.header}>
        <h2 class={styles.heading}>Blocked Users</h2>
        <Flexbox gap={0.5} class={styles.inputRow}>
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
        </Flexbox>
        <Show when={blockMessage()}>
          <p id="block-user-status" data-testid="block-user-status" class={blockError() ? styles.statusError : styles.statusSuccess}>
            {blockMessage()}
          </p>
        </Show>
      </div>

      <div class={styles.listArea}>
        <Show when={blockStore.isLoading}>
          <Flexbox align="center" justify="center" class={styles.loadingState}>
            <p class={styles.mutedText}>Loading...</p>
          </Flexbox>
        </Show>

        <Show when={!blockStore.isLoading && blockStore.blockedUsers.length === 0}>
          <Flexbox align="center" justify="center" id="blocked-users-empty" data-testid="block-list-empty-state" class={styles.emptyState}>
            <p class={styles.mutedText}>No blocked users</p>
          </Flexbox>
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
                  Blocked {formatDate(user.createdAt)}
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
    </Flexbox>
  );
}
