import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

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
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Welcome Screen</h2>
        <Show when={props.isOwner && !isEditing()}>
          <button
            class="text-xcord-brand hover:underline text-sm"
            onClick={startEditing}
            aria-label="Edit Welcome Screen"
          >
            Edit
          </button>
        </Show>
      </div>

      <div class="flex-1 overflow-y-auto px-4 py-4 space-y-4">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Success banner */}
          <Show when={successMessage()}>
            <div class="bg-green-600/20 text-green-400 text-sm px-3 py-2 rounded" role="status">
              {successMessage()}
            </div>
          </Show>

          {/* View mode */}
          <Show when={!isEditing()}>
            <Show when={config()}>
              <div class="space-y-4">
                <div class="flex items-center gap-2">
                  <span
                    class={`text-xs font-semibold px-2 py-0.5 rounded ${
                      config()!.isEnabled
                        ? 'bg-green-500/20 text-green-400'
                        : 'bg-xcord-bg-tertiary text-xcord-text-muted'
                    }`}
                  >
                    {config()!.isEnabled ? 'Enabled' : 'Disabled'}
                  </span>
                </div>

                <div>
                  <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                    Description
                  </p>
                  <p class="text-xcord-text-primary text-sm whitespace-pre-wrap">
                    {config()!.description || (
                      <span class="text-xcord-text-muted italic">No description set</span>
                    )}
                  </p>
                </div>

                <Show when={config()!.channels.length > 0}>
                  <div>
                    <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-2">
                      Recommended Channels ({welcomeChannelCount(config()!)})
                    </p>
                    <div class="space-y-2">
                      <For each={config()!.channels}>
                        {(ch) => (
                          <div class="flex items-start gap-2 bg-xcord-bg-primary rounded px-3 py-2">
                            <Show when={ch.emojiName}>
                              <span class="text-lg flex-shrink-0">{ch.emojiName}</span>
                            </Show>
                            <div class="flex-1 min-w-0">
                              <p class="text-xcord-text-primary text-sm font-medium">
                                #{ch.channelName || ch.channelId}
                              </p>
                              <p class="text-xcord-text-muted text-xs mt-0.5">{ch.description}</p>
                            </div>
                          </div>
                        )}
                      </For>
                    </div>
                  </div>
                </Show>

                <Show when={config()!.channels.length === 0}>
                  <p class="text-xcord-text-muted text-sm">No recommended channels configured.</p>
                </Show>
              </div>
            </Show>

            <Show when={!config()}>
              <p class="text-xcord-text-muted text-sm">
                No welcome screen configured.{' '}
                <Show when={props.isOwner}>
                  <button
                    class="text-xcord-brand hover:underline"
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
            <div class="space-y-4">
              {/* Enabled toggle */}
              <div class="flex items-center gap-3">
                <label class="text-xcord-text-primary text-sm font-medium">
                  Enable Welcome Screen
                </label>
                <button
                  class={`w-10 h-6 rounded-full transition-colors flex-shrink-0 ${
                    editEnabled() ? 'bg-xcord-brand' : 'bg-xcord-bg-tertiary'
                  }`}
                  onClick={() => setEditEnabled((prev) => !prev)}
                  role="switch"
                  aria-checked={editEnabled()}
                  aria-label="Toggle welcome screen"
                >
                  <span
                    class={`block w-4 h-4 rounded-full bg-white shadow transition-transform mx-1 ${
                      editEnabled() ? 'translate-x-4' : 'translate-x-0'
                    }`}
                  />
                </button>
              </div>

              {/* Description */}
              <div>
                <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                  Description *
                </label>
                <textarea
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand resize-none"
                  placeholder="Welcome to our server! Here's what we're about..."
                  rows={4}
                  value={editDescription()}
                  onInput={(e) => handleDescriptionInput(e.currentTarget.value)}
                  aria-label="Welcome screen description"
                />
                <Show when={descError()}>
                  <p class="text-red-400 text-xs mt-1" role="alert">
                    {descError()}
                  </p>
                </Show>
              </div>

              {/* Recommended channels */}
              <div class="space-y-3">
                <div class="flex items-center justify-between">
                  <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide">
                    Recommended Channels
                  </p>
                  <button
                    class="text-xcord-brand hover:underline text-xs"
                    onClick={handleAddChannel}
                    aria-label="Add channel"
                  >
                    + Add Channel
                  </button>
                </div>

                <For each={editChannels()}>
                  {(ch, index) => (
                    <div class="bg-xcord-bg-primary rounded p-3 space-y-2">
                      <div class="flex items-center justify-between">
                        <span class="text-xcord-text-muted text-xs">
                          Channel {index() + 1}
                        </span>
                        <button
                          class="text-red-400 hover:text-red-300 text-xs"
                          onClick={() => handleRemoveChannel(index())}
                          aria-label={`Remove channel ${index() + 1}`}
                        >
                          Remove
                        </button>
                      </div>
                      <div class="grid grid-cols-2 gap-2">
                        <input
                          type="text"
                          class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                          placeholder="Channel ID"
                          value={ch.channelId}
                          onInput={(e) => handleChannelField(index(), 'channelId', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} ID`}
                        />
                        <input
                          type="text"
                          class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                          placeholder="Channel name"
                          value={ch.channelName ?? ''}
                          onInput={(e) => handleChannelField(index(), 'channelName', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} name`}
                        />
                      </div>
                      <div class="grid grid-cols-2 gap-2">
                        <input
                          type="text"
                          class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                          placeholder="Description"
                          value={ch.description}
                          onInput={(e) => handleChannelField(index(), 'description', e.currentTarget.value)}
                          aria-label={`Channel ${index() + 1} description`}
                        />
                        <input
                          type="text"
                          class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
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
                <p class="text-red-400 text-xs" role="alert">
                  {submitError()}
                </p>
              </Show>

              <div class="flex gap-2 pt-1">
                <button
                  class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  onClick={handleSave}
                  disabled={isSaving()}
                  aria-label="Save Welcome Screen"
                >
                  {isSaving() ? 'Saving...' : 'Save Welcome Screen'}
                </button>
                <button
                  class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
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
