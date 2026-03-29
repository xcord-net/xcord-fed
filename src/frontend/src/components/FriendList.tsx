import { For, Show, onMount, createSignal } from 'solid-js';
import { useFriends } from '../stores/friend.store';
import { useAuth } from '../stores/auth.store';
import PresenceDot from './PresenceDot';
import { getErrorMessage } from '../utils/errors';
import styles from './FriendList.module.css';

export default function FriendList() {
  const friendStore = useFriends();
  const auth = useAuth();
  const [activeTab, setActiveTab] = createSignal<'all' | 'pending' | 'blocked'>('all');
  const [friendUsername, setFriendUsername] = createSignal('');
  const [addFriendMessage, setAddFriendMessage] = createSignal('');
  const [addFriendError, setAddFriendError] = createSignal(false);

  const handleAddFriend = async () => {
    try {
      await friendStore.sendFriendRequestByUsername(friendUsername());
      setAddFriendMessage('Friend request sent!');
      setAddFriendError(false);
      setFriendUsername('');
    } catch (err: unknown) {
      setAddFriendMessage(getErrorMessage(err, 'Failed to send request'));
      setAddFriendError(true);
    }
  };

  onMount(() => {
    if (auth.user?.id) {
      friendStore.setCurrentUserId(auth.user.id);
    }
    friendStore.loadFriends();
    friendStore.loadFriendRequests();
  });

  return (
    <div class={styles.container}>
      <div class={styles.topBar}>
        <div class={styles.tabRow}>
          <h2 data-testid="friends-heading" class={styles.heading}>Friends</h2>
          <div class={styles.divider} />
          <div class={styles.tabs}>
            <button
              data-testid="friends-tab-all"
              class={activeTab() === 'all' ? `${styles.tab} ${styles.tabActive}` : styles.tab}
              onClick={() => setActiveTab('all')}
            >
              All
            </button>
            <button
              data-testid="friends-tab-pending"
              class={activeTab() === 'pending' ? `${styles.tab} ${styles.tabActive}` : styles.tab}
              onClick={() => setActiveTab('pending')}
            >
              Pending
              <Show when={friendStore.incomingRequests.length > 0}>
                <span class={styles.pendingBadge}>
                  {friendStore.incomingRequests.length}
                </span>
              </Show>
            </button>
          </div>
        </div>

        {/* Add Friend section */}
        <div class={styles.addFriendRow}>
          <input
            data-testid="friend-request-input"
            id="add-friend-input"
            type="text"
            placeholder="Enter a username"
            value={friendUsername()}
            onInput={(e) => setFriendUsername(e.currentTarget.value)}
            class={styles.addFriendInput}
          />
          <button
            data-testid="friend-request-submit-button"
            class={styles.addFriendBtn}
            disabled={!friendUsername().trim()}
            onClick={handleAddFriend}
          >
            Send Request
          </button>
        </div>
        <Show when={addFriendMessage()}>
          <p class={addFriendError()
            ? `${styles.addFriendMessage} ${styles.addFriendMessageError}`
            : `${styles.addFriendMessage} ${styles.addFriendMessageSuccess}`}>
            {addFriendMessage()}
          </p>
        </Show>
      </div>

      <div class={styles.listArea}>
        <Show when={activeTab() === 'all'}>
          <Show when={friendStore.isLoading}>
            <div class={styles.loadingCenter}>
              <p class={styles.loadingText}>Loading...</p>
            </div>
          </Show>

          <Show when={!friendStore.isLoading && friendStore.friends.length === 0}>
            <div class={styles.emptyCenter}>
              <p data-testid="friends-empty-state" class={styles.emptyText}>No friends yet</p>
            </div>
          </Show>

          <For each={friendStore.friends}>
            {(friend) => (
              <div class={styles.friendItem}>
                <div class={styles.avatarWrapper}>
                  <div class={styles.avatar}>
                    <Show when={friend.avatarUrl} fallback={friend.username.charAt(0).toUpperCase()}>
                      <img
                        src={friend.avatarUrl}
                        alt={friend.username}
                        class={styles.avatarImg}
                      />
                    </Show>
                  </div>
                  <PresenceDot userId={friend.userId} size="md" />
                </div>

                <div class={styles.friendInfo}>
                  <h3 class={styles.friendDisplayName}>{friend.displayName}</h3>
                  <p class={styles.friendUsername}>{friend.username}</p>
                </div>

                <button
                  data-testid="friend-remove-button"
                  class={styles.removeBtn}
                  onClick={() => friendStore.removeFriend(friend.userId)}
                >
                  Remove
                </button>
              </div>
            )}
          </For>
        </Show>

        <Show when={activeTab() === 'pending'}>
          <div class={styles.pendingSection}>
            <h3 class={styles.pendingSectionTitle}>Incoming Requests</h3>
            <For each={friendStore.incomingRequests}>
              {(request) => (
                <div class={styles.requestCard}>
                  <div class={styles.requestAvatar}>
                    {request.fromUsername.charAt(0).toUpperCase()}
                  </div>

                  <div class={styles.requestInfo}>
                    <p class={styles.requestUsername}>{request.fromUsername}</p>
                  </div>

                  <button
                    data-testid="friend-accept-button"
                    class={styles.acceptBtn}
                    onClick={() => friendStore.acceptFriendRequest(request.id)}
                  >
                    Accept
                  </button>
                  <button
                    data-testid="friend-reject-button"
                    class={styles.rejectBtn}
                    onClick={() => friendStore.declineFriendRequest(request.id)}
                  >
                    Decline
                  </button>
                </div>
              )}
            </For>

            <h3 class={styles.pendingSectionTitleSpaced}>Outgoing Requests</h3>
            <For each={friendStore.outgoingRequests}>
              {(request) => (
                <div class={styles.requestCard}>
                  <div class={styles.requestAvatar}>
                    {request.toUsername.charAt(0).toUpperCase()}
                  </div>

                  <div class={styles.requestInfo}>
                    <p class={styles.requestUsername}>{request.toUsername}</p>
                    <p class={styles.requestStatus}>Pending</p>
                  </div>

                  <button
                    class={styles.cancelRequestBtn}
                    onClick={() => friendStore.cancelFriendRequest(request.id)}
                  >
                    Cancel
                  </button>
                </div>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}
