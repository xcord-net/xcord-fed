import { For, Show, onMount, createSignal } from 'solid-js';
import { useFriends } from '../stores/friend.store';
import { useAuth } from '../stores/auth.store';
import PresenceDot from './PresenceDot';
import { getErrorMessage } from '../utils/errors';

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
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold mb-3">Friends</h2>

        <div class="flex space-x-2">
          <button
            class={`px-3 py-1 rounded ${
              activeTab() === 'all'
                ? 'bg-xcord-brand text-white'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => setActiveTab('all')}
          >
            All
          </button>
          <button
            class={`px-3 py-1 rounded ${
              activeTab() === 'pending'
                ? 'bg-xcord-brand text-white'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => setActiveTab('pending')}
          >
            Pending
            <Show when={friendStore.incomingRequests.length > 0}>
              <span class="ml-1 bg-red-500 text-white text-xs px-1.5 py-0.5 rounded-full">
                {friendStore.incomingRequests.length}
              </span>
            </Show>
          </button>
        </div>

        {/* Add Friend section */}
        <div class="mt-3 flex space-x-2">
          <input
            id="add-friend-input"
            type="text"
            placeholder="Enter a username"
            value={friendUsername()}
            onInput={(e) => setFriendUsername(e.currentTarget.value)}
            class="flex-1 bg-xcord-bg-primary text-white px-3 py-1.5 rounded text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
          />
          <button
            class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50"
            disabled={!friendUsername().trim()}
            onClick={handleAddFriend}
          >
            Send Request
          </button>
        </div>
        <Show when={addFriendMessage()}>
          <p class={`text-sm mt-2 ${addFriendError() ? 'text-red-400' : 'text-green-400'}`}>
            {addFriendMessage()}
          </p>
        </Show>
      </div>

      <div class="flex-1 overflow-y-auto">
        <Show when={activeTab() === 'all'}>
          <Show when={friendStore.isLoading}>
            <div class="flex items-center justify-center h-32">
              <p class="text-xcord-text-muted">Loading...</p>
            </div>
          </Show>

          <Show when={!friendStore.isLoading && friendStore.friends.length === 0}>
            <div class="flex items-center justify-center h-32">
              <p class="text-xcord-text-muted">No friends yet</p>
            </div>
          </Show>

          <For each={friendStore.friends}>
            {(friend) => (
              <div class="px-4 py-3 flex items-center space-x-3 hover:bg-xcord-bg-primary/50 border-b border-xcord-border">
                <div class="relative">
                  <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                    <Show when={friend.avatarUrl} fallback={friend.username.charAt(0).toUpperCase()}>
                      <img
                        src={friend.avatarUrl}
                        alt={friend.username}
                        class="w-full h-full rounded-full object-cover"
                      />
                    </Show>
                  </div>
                  <PresenceDot userId={friend.userId} size="md" />
                </div>

                <div class="flex-1 min-w-0">
                  <h3 class="text-white font-medium truncate">{friend.displayName}</h3>
                  <p class="text-xs text-xcord-text-muted truncate">{friend.username}</p>
                </div>

                <button
                  class="text-red-500 hover:text-red-400 text-sm"
                  onClick={() => friendStore.removeFriend(friend.userId)}
                >
                  Remove
                </button>
              </div>
            )}
          </For>
        </Show>

        <Show when={activeTab() === 'pending'}>
          <div class="p-4">
            <h3 class="text-white font-semibold mb-3">Incoming Requests</h3>
            <For each={friendStore.incomingRequests}>
              {(request) => (
                <div class="flex items-center space-x-3 mb-3 p-3 bg-xcord-bg-primary rounded">
                  <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                    {request.fromUsername.charAt(0).toUpperCase()}
                  </div>

                  <div class="flex-1">
                    <p class="text-white">{request.fromUsername}</p>
                  </div>

                  <button
                    class="bg-green-600 text-white px-3 py-1 rounded hover:bg-green-700"
                    onClick={() => friendStore.acceptFriendRequest(request.id)}
                  >
                    Accept
                  </button>
                  <button
                    class="bg-red-600 text-white px-3 py-1 rounded hover:bg-red-700"
                    onClick={() => friendStore.declineFriendRequest(request.id)}
                  >
                    Decline
                  </button>
                </div>
              )}
            </For>

            <h3 class="text-white font-semibold mt-6 mb-3">Outgoing Requests</h3>
            <For each={friendStore.outgoingRequests}>
              {(request) => (
                <div class="flex items-center space-x-3 mb-3 p-3 bg-xcord-bg-primary rounded">
                  <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                    {request.toUsername.charAt(0).toUpperCase()}
                  </div>

                  <div class="flex-1">
                    <p class="text-white">{request.toUsername}</p>
                    <p class="text-xs text-xcord-text-muted">Pending</p>
                  </div>

                  <button
                    class="text-red-500 hover:text-red-400 text-sm"
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
