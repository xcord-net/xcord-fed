import { createSignal, createEffect, For, Show } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';
import Flexbox from './ui/Flexbox';
import styles from './FollowChannel.module.css';
import { formatDate } from '../utils/datetime';
import EmptyState from './ui/EmptyState';

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
    return { follows: [], error: getErrorMessage(err, 'Failed to load follows') };
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
    return { channels: [], error: getErrorMessage(err, 'Failed to load channels') };
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
    return { follow: null, error: getErrorMessage(err, 'Failed to follow channel') };
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
    return { error: getErrorMessage(err, 'Failed to unfollow channel'), success: false };
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
      setError(getErrorMessage(err, 'Failed to load channels'));
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
      setError(getErrorMessage(err, 'Failed to follow channel'));
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
      setError(getErrorMessage(err, 'Failed to unfollow channel'));
    } finally {
      setIsUnfollowing(null);
    }
  };

  return (
    <Show when={isAnnouncementChannel()}>
      <div class={styles.container}>
        <Flexbox align="center" justify="between" class={styles.header}>
          <h3 class={styles.headerTitle}>
            Channel Followers
          </h3>
          <button
            class={styles.followButton}
            onClick={handleOpenFollowDialog}
          >
            Follow in another channel
          </button>
        </Flexbox>

        <Show when={error()}>
          <div class={styles.errorBanner}>{error()}</div>
        </Show>

        <Show when={follows().length === 0}>
          <EmptyState
            title={`Nothing follows ${props.channelName} yet`}
            body="Channels that follow this one get a copy of every announcement posted here."
            dense
            data-testid="follow-channel-empty"
          />
        </Show>

        <div class={styles.followList}>
          <For each={follows()}>
            {(follow) => (
              <Flexbox align="center" justify="between" class={styles.followEntry}>
                <div>
                  <p class={styles.followChannelName}>
                    #{follow.targetChannelName}
                  </p>
                  <p class={styles.followSince}>
                    Following since {formatDate(follow.createdAt)}
                  </p>
                </div>
                <button
                  class={styles.unfollowButton}
                  onClick={() => handleUnfollow(follow.id)}
                  disabled={isUnfollowing() === follow.id}
                >
                  {isUnfollowing() === follow.id ? 'Removing...' : 'Unfollow'}
                </button>
              </Flexbox>
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
          <div class={styles.modalBody}>
            <p class={styles.modalDescription}>
              Select a channel in this server to receive crossposted messages.
            </p>

            <Show when={error()}>
              <div class={styles.errorBanner}>{error()}</div>
            </Show>

            <div class={styles.fieldGroup}>
              <label class={styles.fieldLabel}>
                Target Channel
              </label>
              <select
                class={styles.channelSelect}
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

            <Flexbox gap={0.75}>
              <button
                class={styles.confirmButton}
                onClick={handleFollow}
                disabled={isLoading() || !selectedChannelId()}
              >
                {isLoading() ? 'Following...' : 'Follow Channel'}
              </button>
              <button
                class={styles.cancelButton}
                onClick={() => {
                  setShowFollowDialog(false);
                  setError('');
                }}
              >
                Cancel
              </button>
            </Flexbox>
          </div>
        </Modal>
      </div>
    </Show>
  );
}
