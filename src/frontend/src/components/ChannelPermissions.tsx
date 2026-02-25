import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

export type PermissionState = 'Allow' | 'Deny' | 'Inherit';

export type PermissionKey =
  | 'ViewChannel'
  | 'SendMessages'
  | 'ManageMessages'
  | 'AttachFiles'
  | 'EmbedLinks'
  | 'MentionEveryone'
  | 'ManageChannel'
  | 'Connect'
  | 'Speak';

export const PERMISSION_LABELS: Record<PermissionKey, string> = {
  ViewChannel: 'View Channel',
  SendMessages: 'Send Messages',
  ManageMessages: 'Manage Messages',
  AttachFiles: 'Attach Files',
  EmbedLinks: 'Embed Links',
  MentionEveryone: 'Mention @everyone',
  ManageChannel: 'Manage Channel',
  Connect: 'Connect (Voice)',
  Speak: 'Speak (Voice)',
};

export const ALL_PERMISSIONS: PermissionKey[] = [
  'ViewChannel',
  'SendMessages',
  'ManageMessages',
  'AttachFiles',
  'EmbedLinks',
  'MentionEveryone',
  'ManageChannel',
  'Connect',
  'Speak',
];

export type OverrideSubjectType = 'Role' | 'Member';

export interface PermissionOverride {
  subjectType: OverrideSubjectType;
  subjectId: string;
  subjectName: string;
  permissions: Record<PermissionKey, PermissionState>;
}

export interface ChannelPermissionsData {
  overrides: PermissionOverride[];
}

interface ChannelPermissionsProps {
  serverId: string;
  channelId: string;
}

export function cyclePermissionState(current: PermissionState): PermissionState {
  if (current === 'Inherit') return 'Allow';
  if (current === 'Allow') return 'Deny';
  return 'Inherit';
}

export function permissionStateColor(state: PermissionState): string {
  if (state === 'Allow') return 'text-green-400 bg-green-400/10';
  if (state === 'Deny') return 'text-red-400 bg-red-400/10';
  return 'text-xcord-text-muted bg-xcord-bg-tertiary';
}

export function permissionStateLabel(state: PermissionState): string {
  return state;
}

export function createDefaultPermissions(): Record<PermissionKey, PermissionState> {
  const result = {} as Record<PermissionKey, PermissionState>;
  for (const key of ALL_PERMISSIONS) {
    result[key] = 'Inherit';
  }
  return result;
}

export default function ChannelPermissions(props: ChannelPermissionsProps) {
  const [data, setData] = createSignal<ChannelPermissionsData | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [selectedOverrideId, setSelectedOverrideId] = createSignal<string | null>(null);
  const [dirtyPermissions, setDirtyPermissions] = createSignal<
    Record<PermissionKey, PermissionState> | null
  >(null);

  async function loadPermissions() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<ChannelPermissionsData>(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/permissions`,
      );
      setData(result);
      setSelectedOverrideId(null);
      setDirtyPermissions(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load permissions');
    } finally {
      setIsLoading(false);
    }
  }

  function selectOverride(override: PermissionOverride) {
    setSelectedOverrideId(override.subjectId);
    setDirtyPermissions({ ...override.permissions });
  }

  function handleCyclePermission(key: PermissionKey) {
    const current = dirtyPermissions();
    if (!current) return;
    setDirtyPermissions({
      ...current,
      [key]: cyclePermissionState(current[key]),
    });
  }

  async function handleSave() {
    const overrideId = selectedOverrideId();
    const perms = dirtyPermissions();
    if (!overrideId || !perms) return;

    setIsSaving(true);
    setError(null);
    try {
      const updated = await api.put<ChannelPermissionsData>(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/permissions`,
        { subjectId: overrideId, permissions: perms },
      );
      setData(updated);
      setDirtyPermissions(null);
      setSelectedOverrideId(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to save permissions');
    } finally {
      setIsSaving(false);
    }
  }

  function handleDiscard() {
    setDirtyPermissions(null);
    setSelectedOverrideId(null);
  }

  onMount(() => {
    loadPermissions();
  });

  const selectedOverride = () =>
    data()?.overrides.find((o) => o.subjectId === selectedOverrideId()) ?? null;

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Channel Permissions</h2>
        <p class="text-xcord-text-muted text-xs mt-0.5">
          Configure per-role and per-member permission overrides for this channel.
        </p>
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <Show when={isLoading()}>
        <div class="flex items-center justify-center flex-1">
          <p class="text-xcord-text-muted">Loading permissions...</p>
        </div>
      </Show>

      <Show when={!isLoading() && data() !== null}>
        <div class="flex flex-1 overflow-hidden">
          {/* Override list (left panel) */}
          <div class="w-56 border-r border-xcord-border overflow-y-auto flex-shrink-0">
            <div class="px-3 py-2 text-xcord-text-muted text-xs uppercase font-semibold tracking-wide">
              Roles
            </div>
            <For each={data()!.overrides.filter((o) => o.subjectType === 'Role')}>
              {(override) => (
                <button
                  class={`w-full text-left px-3 py-2 text-sm transition-colors ${
                    selectedOverrideId() === override.subjectId
                      ? 'bg-xcord-brand/20 text-white'
                      : 'text-xcord-text-muted hover:bg-xcord-bg-primary/50 hover:text-white'
                  }`}
                  onClick={() => selectOverride(override)}
                >
                  {override.subjectName}
                </button>
              )}
            </For>

            <div class="px-3 py-2 mt-2 text-xcord-text-muted text-xs uppercase font-semibold tracking-wide border-t border-xcord-border">
              Members
            </div>
            <For each={data()!.overrides.filter((o) => o.subjectType === 'Member')}>
              {(override) => (
                <button
                  class={`w-full text-left px-3 py-2 text-sm transition-colors ${
                    selectedOverrideId() === override.subjectId
                      ? 'bg-xcord-brand/20 text-white'
                      : 'text-xcord-text-muted hover:bg-xcord-bg-primary/50 hover:text-white'
                  }`}
                  onClick={() => selectOverride(override)}
                >
                  {override.subjectName}
                </button>
              )}
            </For>

            <Show
              when={
                data()!.overrides.length === 0
              }
            >
              <p class="px-3 py-2 text-xcord-text-muted text-xs">No overrides configured.</p>
            </Show>
          </div>

          {/* Permission grid (right panel) */}
          <div class="flex-1 overflow-y-auto p-4">
            <Show
              when={selectedOverride() !== null && dirtyPermissions() !== null}
              fallback={
                <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
                  <p>Select a role or member to edit permissions.</p>
                </div>
              }
            >
              <div class="space-y-1">
                <div class="flex items-center justify-between mb-4">
                  <h3 class="text-white font-semibold text-sm">
                    {selectedOverride()?.subjectName}
                    <span class="ml-2 text-xcord-text-muted font-normal text-xs">
                      ({selectedOverride()?.subjectType})
                    </span>
                  </h3>
                  <div class="flex space-x-2">
                    <button
                      class="text-xs px-3 py-1.5 rounded bg-xcord-bg-tertiary text-xcord-text-muted hover:text-white transition-colors"
                      onClick={handleDiscard}
                    >
                      Discard
                    </button>
                    <button
                      class="text-xs px-3 py-1.5 rounded bg-xcord-brand text-white hover:bg-xcord-brand-hover disabled:opacity-50 transition-colors"
                      onClick={handleSave}
                      disabled={isSaving()}
                    >
                      {isSaving() ? 'Saving...' : 'Save Changes'}
                    </button>
                  </div>
                </div>

                {/* Permission grid */}
                <For each={ALL_PERMISSIONS}>
                  {(key) => {
                    const state = () => dirtyPermissions()?.[key] ?? 'Inherit';
                    return (
                      <div class="flex items-center justify-between py-2 border-b border-xcord-border/50">
                        <span class="text-xcord-text-primary text-sm">{PERMISSION_LABELS[key]}</span>
                        <div class="flex items-center space-x-1">
                          <For each={['Allow', 'Deny', 'Inherit'] as PermissionState[]}>
                            {(option) => (
                              <button
                                class={`text-xs px-2.5 py-1 rounded transition-colors ${
                                  state() === option
                                    ? permissionStateColor(option) + ' font-semibold'
                                    : 'text-xcord-text-muted bg-xcord-bg-tertiary hover:text-white'
                                }`}
                                onClick={() => {
                                  const curr = dirtyPermissions();
                                  if (curr) {
                                    setDirtyPermissions({ ...curr, [key]: option });
                                  }
                                }}
                              >
                                {option}
                              </button>
                            )}
                          </For>
                        </div>
                      </div>
                    );
                  }}
                </For>
              </div>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
}
