import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';

export interface Channel {
  id: string;
  name: string;
  type: string;
  serverId: string;
}

export interface Follow {
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

// ---- Exported pure API functions for testing ----

export async function fetchFollows(
  serverId: string,
  channelId: string,
): Promise<{ follows: Follow[]; error: string }> {
  try {
    const data = await api.get<Follow[]>(
      `/api/v1/servers/${serverId}/channels/${channelId}/followers`,
    );
    return { follows: data, error: '' };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { follows: [], error: errObj?.error || 'Failed to load follows' };
  }
}

export async function fetchAvailableChannels(
  serverId: string,
  sourceChannelId: string,
): Promise<{ channels: Channel[]; error: string }> {
  try {
    const data = await api.get<Channel[]>(`/api/v1/servers/${serverId}/channels`);
    return {
      channels: data.filter((c) => c.id !== sourceChannelId && c.type === 'Text'),
      error: '',
    };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { channels: [], error: errObj?.error || 'Failed to load channels' };
  }
}

export async function followChannel(
  serverId: string,
  channelId: string,
  targetChannelId: string,
): Promise<{ follow: Follow | null; error: string }> {
  if (!targetChannelId) {
    return { follow: null, error: 'Please select a channel' };
  }
  try {
    const follow = await api.post<Follow>(
      `/api/v1/servers/${serverId}/channels/${channelId}/followers`,
      { targetChannelId },
    );
    return { follow, error: '' };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { follow: null, error: errObj?.error || 'Failed to follow channel' };
  }
}

export async function unfollowChannel(
  serverId: string,
  channelId: string,
  subscriptionId: string,
): Promise<{ error: string; success: boolean }> {
  try {
    await api.delete(
      `/api/v1/servers/${serverId}/channels/${channelId}/followers/${subscriptionId}`,
    );
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Failed to unfollow channel', success: false };
  }
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
      // Silently ignore - user may not have permission
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
            class="bg-xcord-brand text-white px-3 py-1 rounded text-sm hover:bg-xcord-brand-hover transition"
            onClick={handleOpenFollowDialog}
          >
            Follow in another channel
          </button>
        </div>

        <Show when={error()}>
          <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
        </Show>

        <Show when={follows().length === 0}>
          <div class="flex flex-col items-center justify-center py-8 text-center">
            <p class="text-xcord-text-muted text-sm">No channels are following {props.channelName} yet.</p>
          </div>
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
        <Modal
          open={showFollowDialog()}
          onClose={() => { setShowFollowDialog(false); setError(''); }}
          title={"Follow #" + props.channelName}
          size="md"
        >
          <div class="p-6">
            <p class="text-xcord-text-muted text-sm mb-4">
              Select a channel in this server to receive crossposted messages.
            </p>

            <Show when={error()}>
              <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
            </Show>

            <div class="mb-4">
              <label class="text-xs text-xcord-text-muted block mb-1">
                Target Channel
              </label>
              <select
                class="w-full bg-xcord-bg-primary text-xcord-text-primary px-3 py-2 rounded text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
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
                class="flex-1 bg-xcord-brand text-white py-2 rounded hover:bg-xcord-brand-hover transition disabled:opacity-50"
                onClick={handleFollow}
                disabled={isLoading() || !selectedChannelId()}
              >
                {isLoading() ? 'Following...' : 'Follow Channel'}
              </button>
              <button
                class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                onClick={() => {
                  setShowFollowDialog(false);
                  setError('');
                }}
              >
                Cancel
              </button>
            </div>
          </div>
        </Modal>
      </div>
    </Show>
  );
}
