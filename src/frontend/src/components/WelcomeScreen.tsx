import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import styles from './WelcomeScreen.module.css';

// ---- Types ----

export interface WelcomeChannel {
  channelId: string;
  channelName?: string;
  description: string;
  emojiName?: string;
  position?: number;
}

export interface WelcomeScreenConfig {
  isEnabled: boolean;
  description: string;
  channels: WelcomeChannel[];
}

interface WelcomeScreenProps {
  serverId: string;
  isOwner?: boolean;
}

// ---- Pure helpers ----

export function validateWelcomeDescription(description: string): string | null {
  if (description.trim().length === 0) return 'Description is required.';
  if (description.trim().length > 500) return 'Description must be 500 characters or fewer.';
  return null;
}

export function validateWelcomeChannel(channel: WelcomeChannel): string | null {
  if (!channel.channelId.trim()) return 'Channel ID is required.';
  if (!channel.description.trim()) return 'Channel description is required.';
  if (channel.description.trim().length > 200) {
    return 'Channel description must be 200 characters or fewer.';
  }
  return null;
}

export function welcomeChannelCount(config: WelcomeScreenConfig): number {
  return config.channels.length;
}

// ---- Component ----

export default function WelcomeScreen(props: WelcomeScreenProps) {
  const [config, setConfig] = createSignal<WelcomeScreenConfig | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isEditing, setIsEditing] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);
  const [successMessage, setSuccessMessage] = createSignal<string | null>(null);

  // Edit form state
  const [editEnabled, setEditEnabled] = createSignal(false);
  const [editDescription, setEditDescription] = createSignal('');
  const [editChannels, setEditChannels] = createSignal<WelcomeChannel[]>([]);
  const [descError, setDescError] = createSignal<string | null>(null);

  const loadConfig = async (serverId: string) => {
    setIsLoading(true);
    try {
      const data = await api.get<WelcomeScreenConfig>(
        `/api/v1/servers/${serverId}/welcome-screen`,
      );
      setConfig(data);
    } catch {
      setConfig(null);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadConfig(serverId);
    }
  });

  const startEditing = () => {
    const current = config();
    setEditEnabled(current?.isEnabled ?? false);
    setEditDescription(current?.description ?? '');
    setEditChannels(
      current?.channels.map((ch) => ({ ...ch })) ?? [],
    );
    setDescError(null);
    setSubmitError(null);
    setIsEditing(true);
  };

  const handleAddChannel = () => {
    setEditChannels((prev) => [
      ...prev,
      { channelId: '', channelName: '', description: '', emojiName: '', position: prev.length },
    ]);
  };

  const handleRemoveChannel = (index: number) => {
    setEditChannels((prev) => prev.filter((_, i) => i !== index));
  };

  const handleChannelField = (
    index: number,
    field: keyof WelcomeChannel,
    value: string,
  ) => {
    setEditChannels((prev) =>
      prev.map((ch, i) => (i === index ? { ...ch, [field]: value } : ch)),
    );
  };

  const handleDescriptionInput = (value: string) => {
    setEditDescription(value);
    setDescError(validateWelcomeDescription(value));
  };

  const handleSave = async () => {
    const descErr = validateWelcomeDescription(editDescription());
    if (descErr) {
      setDescError(descErr);
      return;
    }

    // Validate each channel
    for (const ch of editChannels()) {
      const chErr = validateWelcomeChannel(ch);
      if (chErr) {
        setSubmitError(`Channel error: ${chErr}`);
        return;
      }
    }

    setIsSaving(true);
    setSubmitError(null);
    try {
      const channelNamesById = Object.fromEntries(
        editChannels().map((ch) => [ch.channelId.trim(), ch.channelName?.trim() || ''])
      );
      const updated = await api.put<WelcomeScreenConfig>(
        `/api/v1/servers/${props.serverId}/welcome-screen`,
        {
          isEnabled: editEnabled(),
          description: editDescription().trim(),
          channels: editChannels().map((ch, index) => ({
            channelId: ch.channelId.trim(),
            description: ch.description.trim(),
            emojiName: ch.emojiName?.trim() || undefined,
            position: ch.position ?? index,
          })),
        },
      );
      // Merge locally-entered channelNames back (backend doesn't store them)
      const mergedConfig: WelcomeScreenConfig = {
        ...updated,
        channels: (updated.channels ?? []).map((ch) => ({
          ...ch,
          channelName: channelNamesById[String(ch.channelId)] || undefined,
        })),
      };
      setConfig(mergedConfig);
      setIsEditing(false);
      setSuccessMessage('Welcome screen saved.');
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch {
      setSubmitError('Failed to save welcome screen. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    setIsEditing(false);
    setDescError(null);
    setSubmitError(null);
  };

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Welcome Screen</h2>
        <Show when={props.isOwner && !isEditing()}>
          <button
            class={styles.editLink}
            onClick={startEditing}
            aria-label="Edit Welcome Screen"
          >
            Edit
          </button>
        </Show>
      </div>

      <div class={styles.scrollArea}>
        <Show when={isLoading()}>
          <div class={styles.loadingCenter}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Success banner */}
          <Show when={successMessage()}>
            <div class={styles.successBanner} role="status">
              {successMessage()}
            </div>
          </Show>

          {/* View mode */}
          <Show when={!isEditing()}>
            <Show when={config()}>
              <div class={styles.editSection}>
                <div class={styles.statusRow}>
                  <span
                    class={config()!.isEnabled
                      ? `${styles.statusBadge} ${styles.statusEnabled}`
                      : `${styles.statusBadge} ${styles.statusDisabled}`}
                  >
                    {config()!.isEnabled ? 'Enabled' : 'Disabled'}
                  </span>
                </div>

                <div class={styles.fieldGroup}>
                  <p class={styles.fieldLabel}>
                    Description
                  </p>
                  <p class={styles.fieldValue}>
                    {config()!.description || (
                      <span class={styles.fieldValueMuted}>No description set</span>
                    )}
                  </p>
                </div>

                <Show when={config()!.channels.length > 0}>
                  <div>
                    <p class={styles.channelsLabel}>
                      Recommended Channels ({welcomeChannelCount(config()!)})
                    </p>
                    <div class={styles.channelsList}>
                      <For each={config()!.channels}>
                        {(ch) => (
                          <div class={styles.channelCard}>
                            <Show when={ch.emojiName}>
                              <span class={styles.channelEmoji}>{ch.emojiName}</span>
                            </Show>
                            <div class={styles.channelInfo}>
                              <p class={styles.channelName}>
                                #{ch.channelName || ch.channelId}
                              </p>
                              <p class={styles.channelDesc}>{ch.description}</p>
                            </div>
                          </div>
                        )}
                      </For>
                    </div>
                  </div>
                </Show>

                <Show when={config()!.channels.length === 0}>
                  <p class={styles.noChannelsText}>No recommended channels configured.</p>
                </Show>
              </div>
            </Show>

            <Show when={!config()}>
              <p class={styles.noConfigText}>
                No welcome screen configured.{' '}
                <Show when={props.isOwner}>
                  <button
                    class={styles.inlineLink}
                    onClick={startEditing}
                  >
                    Set one up
                  </button>
                </Show>
              </p>
            </Show>
          </Show>

          {/* Edit mode */}
          <Show when={isEditing() && props.isOwner}>
            <div class={styles.editSection}>
              {/* Enabled toggle */}
              <div class={styles.toggleRow}>
                <label class={styles.toggleLabel}>
                  Enable Welcome Screen
                </label>
                <button
                  class={editEnabled()
                    ? `${styles.toggleSwitch} ${styles.toggleSwitchOn}`
                    : `${styles.toggleSwitch} ${styles.toggleSwitchOff}`}
                  onClick={() => setEditEnabled((prev) => !prev)}
                  role="switch"
                  aria-checked={editEnabled()}
                  aria-label="Toggle welcome screen"
                >
                  <span
                    class={editEnabled()
                      ? `${styles.toggleThumb} ${styles.toggleThumbOn}`
                      : `${styles.toggleThumb} ${styles.toggleThumbOff}`}
                  />
                </button>
              </div>

              {/* Description */}
              <div>
                <label class={styles.editFieldLabel}>
                  Description *
                </label>
                <textarea
                  class={styles.editTextarea}
                  placeholder="Welcome to our server! Here's what we're about..."
                  rows={4}
                  value={editDescription()}
                  onInput={(e) => handleDescriptionInput(e.currentTarget.value)}
                  aria-label="Welcome screen description"
                />
                <Show when={descError()}>
                  <p class={styles.fieldError} role="alert">
                    {descError()}
                  </p>
                </Show>
              </div>

              {/* Recommended channels */}
              <div class={styles.channelEditSection}>
                <div class={styles.channelEditHeader}>
                  <p class={styles.channelEditLabel}>
                    Recommended Channels
                  </p>
                  <button
                    class={styles.addChannelBtn}
                    onClick={handleAddChannel}
                    aria-label="Add channel"
                  >
                    + Add Channel
                  </button>
                </div>

                <For each={editChannels()}>
                  {(ch, index) => (
                    <div class={styles.channelEditCard}>
                      <div class={styles.channelEditCardHeader}>
                        <span class={styles.channelIndexLabel}>
                          Channel {index() + 1}
                        </span>
                        <button
                          class={styles.removeChannelBtn}
                          onClick={() => handleRemoveChannel(index())}
                          aria-label={`Remove channel ${index() + 1}`}
                        >
                          Remove
                        </button>
                      </div>
                      <div class={styles.channelFieldGrid}>
                        <input
                          type="text"
                          class={styles.channelFieldInput}
                          placeholder="Channel ID"
                          value={ch.channelId}
                          onInput={(e) => handleChannelField(index(), 'channelId', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} ID`}
                        />
                        <input
                          type="text"
                          class={styles.channelFieldInput}
                          placeholder="Channel name"
                          value={ch.channelName ?? ''}
                          onInput={(e) => handleChannelField(index(), 'channelName', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} name`}
                        />
                      </div>
                      <div class={styles.channelFieldGrid}>
                        <input
                          type="text"
                          class={styles.channelFieldInput}
                          placeholder="Description"
                          value={ch.description}
                          onInput={(e) => handleChannelField(index(), 'description', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} description`}
                        />
                        <input
                          type="text"
                          class={styles.channelFieldInput}
                          placeholder="Emoji (optional)"
                          value={ch.emojiName ?? ''}
                          onInput={(e) => handleChannelField(index(), 'emojiName', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} emoji`}
                        />
                      </div>
                    </div>
                  )}
                </For>
              </div>

              <Show when={submitError()}>
                <p class={styles.submitError} role="alert">
                  {submitError()}
                </p>
              </Show>

              <div class={styles.editButtons}>
                <button
                  class={styles.primaryBtn}
                  onClick={handleSave}
                  disabled={isSaving()}
                  aria-label="Save Welcome Screen"
                >
                  {isSaving() ? 'Saving...' : 'Save Welcome Screen'}
                </button>
                <button
                  class={styles.secondaryBtn}
                  onClick={handleCancel}
                >
                  Cancel
                </button>
              </div>
            </div>
          </Show>
        </Show>
      </div>
    </div>
  );
}

export { WelcomeScreen };
