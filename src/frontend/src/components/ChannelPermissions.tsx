import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './ChannelPermissions.module.css';
import EmptyState from './ui/EmptyState';

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

export type OverrideSubjectType = 'Group' | 'Member';

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

function permOptionClass(state: PermissionState, option: PermissionState): string {
  if (state === option) {
    if (option === 'Allow') return `${styles.permOptionButton} ${styles.permOptionAllow}`;
    if (option === 'Deny') return `${styles.permOptionButton} ${styles.permOptionDeny}`;
    return `${styles.permOptionButton} ${styles.permOptionInherit}`;
  }
  return styles.permOptionButton;
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
      setError(getErrorMessage(err, 'Failed to load permissions'));
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
      setError(getErrorMessage(err, 'Failed to save permissions'));
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
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 data-testid="channel-permissions-heading" class={styles.heading}>Channel Permissions</h2>
        <p class={styles.subheading}>
          Configure per-group and per-member permission overrides for this channel.
        </p>
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <Show when={isLoading()}>
        <div class={styles.loadingState}>
          <p class={styles.loadingText}>Loading permissions...</p>
        </div>
      </Show>

      <Show when={!isLoading() && data() !== null}>
        <div class={styles.body}>
          {/* Override list (left panel) */}
          <div class={styles.overrideList}>
            <div data-testid="channel-permissions-groups-label" class={styles.panelLabel}>
              Groups
            </div>
            <For each={data()!.overrides.filter((o) => o.subjectType === 'Group')}>
              {(override) => (
                <button
                  class={`${styles.overrideButton} ${selectedOverrideId() === override.subjectId ? styles.overrideButtonActive : ''}`}
                  onClick={() => selectOverride(override)}
                >
                  {override.subjectName}
                </button>
              )}
            </For>

            <div class={styles.panelLabelBordered}>
              Members
            </div>
            <For each={data()!.overrides.filter((o) => o.subjectType === 'Member')}>
              {(override) => (
                <button
                  class={`${styles.overrideButton} ${selectedOverrideId() === override.subjectId ? styles.overrideButtonActive : ''}`}
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
              <EmptyState
                title="No overrides yet"
                body="An override changes what one role or member can do in this channel."
                dense
                data-testid="channel-permissions-empty"
              />
            </Show>
          </div>

          {/* Permission grid (right panel) */}
          <div class={styles.permissionGrid}>
            <Show
              when={selectedOverride() !== null && dirtyPermissions() !== null}
              fallback={
                <div class={styles.placeholderState}>
                  <p data-testid="channel-permissions-placeholder" class={styles.placeholderText}>Select a group or member to edit permissions.</p>
                </div>
              }
            >
              <div class={styles.permissionList}>
                <div class={styles.overrideHeader}>
                  <h3 class={styles.overrideName}>
                    {selectedOverride()?.subjectName}
                    <span class={styles.overrideType}>
                      ({selectedOverride()?.subjectType})
                    </span>
                  </h3>
                  <div class={styles.overrideActions}>
                    <button
                      class={styles.discardButton}
                      onClick={handleDiscard}
                    >
                      Discard
                    </button>
                    <button
                      data-testid="channel-permissions-save-button"
                      class={styles.saveButton}
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
                      <div data-testid={`channel-perm-row-${key}`} class={styles.permRow}>
                        <span class={styles.permLabel}>{PERMISSION_LABELS[key]}</span>
                        <div class={styles.permButtons}>
                          <For each={['Allow', 'Deny', 'Inherit'] as PermissionState[]}>
                            {(option) => (
                              <button
                                data-testid={`channel-perm-${key}-${option.toLowerCase()}`}
                                class={permOptionClass(state(), option)}
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
