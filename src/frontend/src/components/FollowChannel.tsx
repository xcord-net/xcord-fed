import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';

interface Channel {
  id: string;
  name: string;
  type: string;
  serverId: string;
}

interface Follow {
  id: string;
  targetChannelId: string;
  targetServerId: string;
  targetChannelName: string;
  createdAt: string;
}

interface FollowChannelProps {
  serverId: string;
  channelId: string;
  channelType: string;
  channelName: string;
}

export default function FollowChannel(props: FollowChannelProps) {
  const [showFollowDialog, setShowFollowDialog] = createSignal(false);
  const [follows, setFollows] = createSignal<Follow[]>([]);
  const [channels, setChannels] = createSignal<Channel[]>([]);
  const [selectedChannelId, setSelectedChannelId] = createSignal('');
  const [error, setError] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);
  const [isUnfollowing, setIsUnfollowing] = createSignal<string | null>(null);

  const isAnnouncementChannel = () => props.channelType === 'Announcement';

  const loadFollows = async () => {
    if (!isAnnouncementChannel()) return;
    try {
      const data = await api.get<Follow[]>(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/followers`
      );
      setFollows(data);
    } catch {
      // Silently ignore — user may not have permission
    }
  };

  const loadAvailableChannels = async () => {
    try {
      const data = await api.get<Channel[]>(
        `/api/v1/servers/${props.serverId}/channels`
      );
      // Exclude source channel itself
      setChannels(data.filter((c) => c.id !== props.channelId && c.type === 'Text'));
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to load channels');
    }
  };

  createEffect(() => {
    if (isAnnouncementChannel()) {
      void loadFollows();
    }
  });

  const handleOpenFollowDialog = async () => {
    setError('');
    setSelectedChannelId('');
    await loadAvailableChannels();
    setShowFollowDialog(true);
  };

  const handleFollow = async () => {
    if (!selectedChannelId()) {
      setError('Please select a channel');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const follow = await api.post<Follow>(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/followers`,
        { targetChannelId: selectedChannelId() }
      );
      setFollows((prev) => [...prev, follow]);
      setShowFollowDialog(false);
      setSelectedChannelId('');
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to follow channel');
    } finally {
      setIsLoading(false);
    }
  };

  const handleUnfollow = async (subscriptionId: string) => {
    setIsUnfollowing(subscriptionId);
    try {
      await api.delete(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/followers/${subscriptionId}`
      );
      setFollows((prev) => prev.filter((f) => f.id !== subscriptionId));
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to unfollow channel');
    } finally {
      setIsUnfollowing(null);
    }
  };

  return (
    <Show when={isAnnouncementChannel()}>
      <div class="p-4 bg-xcord-bg-secondary rounded-lg">
        <div class="flex items-center justify-between mb-3">
          <h3 class="text-white font-semibold text-sm">
            Channel Followers
          </h3>
          <button
            class="bg-xcord-brand text-white px-3 py-1 rounded text-sm hover:bg-xcord-brand/80 transition"
            onClick={handleOpenFollowDialog}
          >
            Follow in another channel
          </button>
        </div>

        <Show when={error()}>
          <p class="text-red-400 text-sm mb-3">{error()}</p>
        </Show>

        <Show when={follows().length === 0}>
          <p class="text-xcord-text-muted text-sm">
            No channels are following {props.channelName} yet.
          </p>
        </Show>

        <div class="space-y-2">
          <For each={follows()}>
            {(follow) => (
              <div class="flex items-center justify-between p-3 bg-xcord-bg-primary rounded">
                <div>
                  <p class="text-white text-sm font-medium">
                    #{follow.targetChannelName}
                  </p>
                  <p class="text-xcord-text-muted text-xs">
                    Following since {new Date(follow.createdAt).toLocaleDateString()}
                  </p>
                </div>
                <button
                  class="text-red-400 hover:text-red-300 text-sm disabled:opacity-50"
                  onClick={() => handleUnfollow(follow.id)}
                  disabled={isUnfollowing() === follow.id}
                >
                  {isUnfollowing() === follow.id ? 'Removing...' : 'Unfollow'}
                </button>
              </div>
            )}
          </For>
        </div>

        {/* Follow dialog */}
        <Show when={showFollowDialog()}>
          <div class="fixed inset-0 bg-black/60 flex items-center justify-center z-50">
            <div class="bg-xcord-bg-secondary rounded-lg p-6 w-full max-w-md shadow-xl">
              <h3 class="text-white font-semibold text-lg mb-2">
                Follow #{props.channelName}
              </h3>
              <p class="text-xcord-text-muted text-sm mb-4">
                Select a channel in this server to receive crossposted messages.
              </p>

              <Show when={error()}>
                <p class="text-red-400 text-sm mb-3">{error()}</p>
              </Show>

              <div class="mb-4">
                <label class="text-xs text-xcord-text-muted block mb-1">
                  Target Channel
                </label>
                <select
                  class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand"
                  value={selectedChannelId()}
                  onChange={(e) => setSelectedChannelId(e.currentTarget.value)}
                >
                  <option value="">Select a channel...</option>
                  <For each={channels()}>
                    {(channel) => (
                      <option value={channel.id}>#{channel.name}</option>
                    )}
                  </For>
                </select>
              </div>

              <div class="flex gap-3">
                <button
                  class="flex-1 bg-xcord-brand text-white py-2 rounded hover:bg-xcord-brand/80 transition disabled:opacity-50"
                  onClick={handleFollow}
                  disabled={isLoading() || !selectedChannelId()}
                >
                  {isLoading() ? 'Following...' : 'Follow Channel'}
                </button>
                <button
                  class="flex-1 bg-xcord-bg-primary text-xcord-text-primary py-2 rounded hover:bg-xcord-bg-primary/80 transition"
                  onClick={() => {
                    setShowFollowDialog(false);
                    setError('');
                  }}
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </Show>
      </div>
    </Show>
  );
}
