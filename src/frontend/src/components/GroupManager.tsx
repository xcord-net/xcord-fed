import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';
import { getErrorMessage } from '../utils/errors';
import styles from './GroupManager.module.css';

interface Group {
  id: string;
  serverId: string;
  name: string;
  color: string;
  roles: number;
  position: number;
  isHoisted: boolean;
  isMentionable: boolean;
  limitsJson?: string;
}

interface GroupManagerProps {
  serverId: string;
}

// Common role bit flags (matching the backend Role enum)
const ROLE_FLAGS: { label: string; bit: number }[] = [
  { label: 'View Channels', bit: 1 << 0 },
  { label: 'Manage Server', bit: 1 << 1 },
  { label: 'Manage Groups', bit: 1 << 2 },
  { label: 'Manage Channels', bit: 1 << 3 },
  { label: 'Kick Members', bit: 1 << 4 },
  { label: 'Ban Members', bit: 1 << 5 },
  { label: 'Create Invites', bit: 1 << 6 },
  { label: 'Manage Messages', bit: 1 << 7 },
  { label: 'Send Messages', bit: 1 << 8 },
  { label: 'Embed Links', bit: 1 << 9 },
  { label: 'Attach Files', bit: 1 << 10 },
  { label: 'Read Message History', bit: 1 << 11 },
  { label: 'Use External Emojis', bit: 1 << 12 },
  { label: 'Connect to Voice', bit: 1 << 13 },
  { label: 'Speak in Voice', bit: 1 << 14 },
  { label: 'Mute Members', bit: 1 << 15 },
  { label: 'Deafen Members', bit: 1 << 16 },
  { label: 'Move Members', bit: 1 << 17 },
  { label: 'Video', bit: 1 << 18 },
  { label: 'Share Screen', bit: 1 << 19 },
  { label: 'Send Messages in Threads', bit: 1 << 20 },
  { label: 'Create Public Threads', bit: 1 << 21 },
  { label: 'Create Private Threads', bit: 1 << 22 },
  { label: 'Add Reactions', bit: 1 << 23 },
  { label: 'Mention Everyone', bit: 1 << 24 },
  { label: 'Change Nickname', bit: 1 << 25 },
  { label: 'Manage Nicknames', bit: 1 << 26 },
  { label: 'Timeout Members', bit: 1 << 27 },
  { label: 'Manage Emojis', bit: 1 << 28 },
  { label: 'Manage Stickers', bit: 1 << 29 },
  { label: 'Manage Webhooks', bit: 1 << 30 },
];

const PRESET_COLORS = [
  '#d4943a', '#3ba55d', '#f0b232', '#e06a8a', '#ed4245',
  '#3a8fd4', '#2ecc71', '#c47a2e', '#8a5db8', '#1abc9c',
  '#cf5050', '#e0a44a', '#8a8ea0', '#ffffff', '#000000',
];

export function hasRole(roles: number, bit: number): boolean {
  return (roles & bit) !== 0;
}

export function toggleRole(roles: number, bit: number): number {
  return roles ^ bit;
}

export default function GroupManager(props: GroupManagerProps) {
  const [groups, setGroups] = createSignal<Group[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  // Selected group for editing
  const [selectedGroupId, setSelectedGroupId] = createSignal<string | null>(null);
  const [editName, setEditName] = createSignal('');
  const [editColor, setEditColor] = createSignal('#d4943a');
  const [editRoles, setEditRoles] = createSignal(0);
  const [isSaving, setIsSaving] = createSignal(false);
  const [saveSuccess, setSaveSuccess] = createSignal('');
  const [saveError, setSaveError] = createSignal('');

  // Create group form
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [newGroupName, setNewGroupName] = createSignal('');
  const [isCreating, setIsCreating] = createSignal(false);

  // Delete confirmation
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);
  const [isDeleting, setIsDeleting] = createSignal(false);

  const selectedGroup = () => groups().find((g) => g.id === selectedGroupId());

  async function loadGroups() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<Group[]>(`/api/v1/servers/${props.serverId}/groups`);
      setGroups(result.map((g) => ({ ...g, id: String(g.id) })));
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load groups.'));
    } finally {
      setIsLoading(false);
    }
  }

  function selectGroup(group: Group) {
    setSelectedGroupId(group.id);
    setEditName(group.name);
    setEditColor(group.color || '#d4943a');
    setEditRoles(group.roles);
    setSaveSuccess('');
    setSaveError('');
  }

  async function handleSaveGroup(e: Event) {
    e.preventDefault();
    const groupId = selectedGroupId();
    if (!groupId) return;

    setIsSaving(true);
    setSaveSuccess('');
    setSaveError('');

    try {
      const updated = await api.patch<Group>(
        `/api/v1/servers/${props.serverId}/groups/${groupId}`,
        { name: editName().trim(), color: editColor(), roles: editRoles() }
      );
      const updatedGroup = { ...updated, id: String(updated.id) };
      setGroups(groups().map((g) => (g.id === groupId ? updatedGroup : g)));
      setSaveSuccess('Group saved successfully.');
    } catch (err: unknown) {
      setSaveError(getErrorMessage(err, 'Failed to save group.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCreateGroup(e: Event) {
    e.preventDefault();
    const name = newGroupName().trim();
    if (!name) return;

    setIsCreating(true);
    setError(null);

    try {
      const created = await api.post<Group>(`/api/v1/servers/${props.serverId}/groups`, { name });
      const newGroup = { ...created, id: String(created.id) };
      setGroups([...groups(), newGroup]);
      setNewGroupName('');
      setShowCreateForm(false);
      selectGroup(newGroup);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to create group.'));
    } finally {
      setIsCreating(false);
    }
  }

  async function handleDeleteGroup(groupId: string) {
    setIsDeleting(true);
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/groups/${groupId}`);
      setGroups(groups().filter((g) => g.id !== groupId));
      if (selectedGroupId() === groupId) {
        setSelectedGroupId(null);
      }
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to delete group.'));
    } finally {
      setIsDeleting(false);
    }
  }

  onMount(() => {
    loadGroups();
  });

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 data-testid="group-manager-heading" class={styles.heading}>Groups</h2>
        <button
          data-testid="create-group-button"
          type="button"
          onClick={() => { setShowCreateForm(true); setSaveSuccess(''); setSaveError(''); }}
          class={styles.createButton}
        >
          Create Group
        </button>
      </div>

      <Show when={error()}>
        <div role="alert" class={styles.errorBanner}>
          {error()}
        </div>
      </Show>

      <div class={styles.body}>
        {/* Group list */}
        <div data-testid="group-list-sidebar" class={styles.sidebar}>
          <Show when={isLoading()}>
            <div class={styles.sidebarLoading}>
              <p class={styles.mutedText}>Loading groups...</p>
            </div>
          </Show>

          <Show when={!isLoading() && groups().length === 0}>
            <div class={styles.sidebarEmpty}>
              <p class={styles.mutedText}>No groups yet.</p>
            </div>
          </Show>

          <For each={groups()}>
            {(group) => (
              <button
                data-testid={group.name === '@everyone' ? 'group-item-everyone' : `group-item-${group.id}`}
                type="button"
                onClick={() => { selectGroup(group); setShowCreateForm(false); }}
                class={`${styles.groupItem} ${selectedGroupId() === group.id ? styles.groupItemActive : ''}`}
                aria-pressed={selectedGroupId() === group.id}
              >
                {/* Color dot */}
                <span
                  class={styles.groupColorDot}
                  style={{ 'background-color': group.color || '#d4943a' }}
                  aria-hidden="true"
                />
                <span class={styles.groupName}>{group.name}</span>
              </button>
            )}
          </For>
        </div>

        {/* Editor pane */}
        <div class={styles.editorPane}>
          {/* Create group form */}
          <Show when={showCreateForm()}>
            <form onSubmit={handleCreateGroup} class={styles.createForm}>
              <h3 class={styles.formHeading}>Create New Group</h3>
              <div class={styles.fieldGroup}>
                <label for="new-group-name" class={styles.fieldLabel}>
                  Group Name <span class={styles.required}>*</span>
                </label>
                <input
                  id="new-group-name"
                  type="text"
                  required
                  maxlength="100"
                  value={newGroupName()}
                  onInput={(e) => setNewGroupName(e.currentTarget.value)}
                  class={styles.textInput}
                  placeholder="New Group"
                />
              </div>
              <div class={styles.formActions}>
                <button
                  type="submit"
                  data-testid="create-group-submit-button"
                  disabled={isCreating()}
                  class={styles.submitButton}
                >
                  {isCreating() ? 'Creating...' : 'Create'}
                </button>
                <button
                  type="button"
                  onClick={() => { setShowCreateForm(false); setNewGroupName(''); }}
                  class={styles.cancelFormButton}
                >
                  Cancel
                </button>
              </div>
            </form>
          </Show>

          {/* Group edit form */}
          <Show when={selectedGroup() && !showCreateForm()}>
            {(group) => (
              <form onSubmit={handleSaveGroup} class={styles.editForm}>
                <div class={styles.editFormHeader}>
                  <h3 class={styles.editFormTitle}>Edit Group</h3>

                  {/* Delete button */}
                  <button
                    type="button"
                    data-testid="delete-group-button"
                    onClick={() => setShowDeleteConfirm(true)}
                    class={styles.deleteGroupButton}
                  >
                    Delete Group
                  </button>
                </div>

                {/* Group name */}
                <div class={styles.fieldGroup}>
                  <label for="edit-group-name" class={styles.fieldLabel}>
                    Group Name
                  </label>
                  <input
                    id="edit-group-name"
                    type="text"
                    required
                    maxlength="100"
                    value={editName()}
                    onInput={(e) => setEditName(e.currentTarget.value)}
                    class={styles.textInput}
                  />
                </div>

                {/* Color picker */}
                <div class={styles.fieldGroup}>
                  <label class={styles.fieldLabel}>
                    Group Color
                  </label>

                  {/* Preset color swatches */}
                  <div class={styles.colorSwatches} role="group" aria-label="Preset colors">
                    <For each={PRESET_COLORS}>
                      {(color) => (
                        <button
                          type="button"
                          aria-label={`Color ${color}`}
                          aria-pressed={editColor() === color}
                          onClick={() => setEditColor(color)}
                          class={`${styles.colorSwatch} ${editColor() === color ? styles.colorSwatchSelected : ''}`}
                          style={{ 'background-color': color }}
                        />
                      )}
                    </For>
                  </div>

                  {/* Hex input */}
                  <div class={styles.hexRow}>
                    <span
                      class={styles.colorPreview}
                      style={{ 'background-color': editColor() }}
                      aria-hidden="true"
                    />
                    <label for="color-hex" class="sr-only">Hex color</label>
                    <input
                      id="color-hex"
                      type="text"
                      maxlength="7"
                      value={editColor()}
                      onInput={(e) => {
                        const val = e.currentTarget.value;
                        if (/^#[0-9a-fA-F]{0,6}$/.test(val)) setEditColor(val);
                      }}
                      aria-label="Hex color value"
                      class={styles.hexInput}
                    />
                  </div>
                </div>

                {/* Roles (permission flags) */}
                <div class={styles.fieldGroup}>
                  <label class={styles.fieldLabel}>
                    Roles
                  </label>
                  <div class={styles.rolesList}>
                    <For each={ROLE_FLAGS}>
                      {(flag) => (
                        <label data-testid={`permission-${flag.label.toLowerCase().replace(/\s+/g, '-')}`} class={styles.roleItem}>
                          <input
                            type="checkbox"
                            checked={hasRole(editRoles(), flag.bit)}
                            onChange={() => setEditRoles(toggleRole(editRoles(), flag.bit))}
                            class={styles.roleCheckbox}
                          />
                          <span class={styles.roleLabel}>
                            {flag.label}
                          </span>
                        </label>
                      )}
                    </For>
                  </div>
                </div>

                {/* Status messages */}
                <Show when={saveSuccess()}>
                  <div role="status" class={styles.successMsg}>
                    {saveSuccess()}
                  </div>
                </Show>
                <Show when={saveError()}>
                  <div role="alert" class={styles.errorMsgInline}>
                    {saveError()}
                  </div>
                </Show>

                {/* Save button */}
                <div class={styles.saveRow}>
                  <button
                    data-testid="group-save-changes-button"
                    type="submit"
                    disabled={isSaving()}
                    class={styles.saveButton}
                  >
                    {isSaving() ? 'Saving...' : 'Save Changes'}
                  </button>
                </div>
              </form>
            )}
          </Show>

          {/* Empty state */}
          <Show when={!selectedGroup() && !showCreateForm()}>
            <div class={styles.editorEmpty}>
              <p class={styles.mutedText}>Select a group to edit, or create a new one.</p>
            </div>
          </Show>
        </div>
      </div>

      <Modal
        open={showDeleteConfirm()}
        onClose={() => setShowDeleteConfirm(false)}
        title="Delete Group"
        size="sm"
        role="alertdialog"
      >
        <div class={styles.dialogBody}>
          <p class={styles.dialogText}>
            Are you sure you want to delete the group "{selectedGroup()?.name}"? Members with this group will lose its roles.
          </p>
          <div class={styles.dialogActions}>
            <button
              class={styles.dialogCancelButton}
              onClick={() => setShowDeleteConfirm(false)}
            >
              Cancel
            </button>
            <button
              class={styles.dialogDeleteButton}
              disabled={isDeleting()}
              onClick={() => {
                handleDeleteGroup(selectedGroupId()!);
                setShowDeleteConfirm(false);
              }}
            >
              {isDeleting() ? 'Deleting...' : 'Delete'}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
