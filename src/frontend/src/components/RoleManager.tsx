import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';

interface Role {
  id: string;
  serverId: string;
  name: string;
  color: string;
  permissions: number;
  position: number;
  isHoisted: boolean;
  isMentionable: boolean;
}

interface RoleManagerProps {
  serverId: string;
}

// Common permission bit flags
const PERMISSION_FLAGS: { label: string; bit: number }[] = [
  { label: 'Administrator', bit: 1 << 0 },
  { label: 'Manage Server', bit: 1 << 1 },
  { label: 'Manage Roles', bit: 1 << 2 },
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
];

const PRESET_COLORS = [
  '#5865f2', '#57f287', '#fee75c', '#eb459e', '#ed4245',
  '#3498db', '#2ecc71', '#e67e22', '#9b59b6', '#1abc9c',
  '#e74c3c', '#f39c12', '#95a5a6', '#ffffff', '#000000',
];

export function hasPermission(perms: number, bit: number): boolean {
  return (perms & bit) !== 0;
}

export function togglePermission(perms: number, bit: number): number {
  return perms ^ bit;
}

export default function RoleManager(props: RoleManagerProps) {
  const [roles, setRoles] = createSignal<Role[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  // Selected role for editing
  const [selectedRoleId, setSelectedRoleId] = createSignal<string | null>(null);
  const [editName, setEditName] = createSignal('');
  const [editColor, setEditColor] = createSignal('#5865f2');
  const [editPermissions, setEditPermissions] = createSignal(0);
  const [isSaving, setIsSaving] = createSignal(false);
  const [saveSuccess, setSaveSuccess] = createSignal('');
  const [saveError, setSaveError] = createSignal('');

  // Create role form
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [newRoleName, setNewRoleName] = createSignal('');
  const [isCreating, setIsCreating] = createSignal(false);

  // Delete confirmation
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);
  const [isDeleting, setIsDeleting] = createSignal(false);

  const selectedRole = () => roles().find((r) => r.id === selectedRoleId());

  async function loadRoles() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<Role[]>(`/api/v1/servers/${props.serverId}/roles`);
      setRoles(result.map((r) => ({ ...r, id: String(r.id) })));
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string; message?: string };
      setError(e?.detail ?? e?.error ?? e?.message ?? 'Failed to load roles.');
    } finally {
      setIsLoading(false);
    }
  }

  function selectRole(role: Role) {
    setSelectedRoleId(role.id);
    setEditName(role.name);
    setEditColor(role.color || '#5865f2');
    setEditPermissions(role.permissions);
    setSaveSuccess('');
    setSaveError('');
  }

  async function handleSaveRole(e: Event) {
    e.preventDefault();
    const roleId = selectedRoleId();
    if (!roleId) return;

    setIsSaving(true);
    setSaveSuccess('');
    setSaveError('');

    try {
      const updated = await api.patch<Role>(
        `/api/v1/servers/${props.serverId}/roles/${roleId}`,
        { name: editName().trim(), color: editColor(), permissions: editPermissions() }
      );
      const updatedRole = { ...updated, id: String(updated.id) };
      setRoles(roles().map((r) => (r.id === roleId ? updatedRole : r)));
      setSaveSuccess('Role saved successfully.');
    } catch (err: unknown) {
      const e = err as { detail?: string; error?: string; message?: string };
      setSaveError(e?.detail ?? e?.error ?? e?.message ?? 'Failed to save role.');
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCreateRole(e: Event) {
    e.preventDefault();
    const name = newRoleName().trim();
    if (!name) return;

    setIsCreating(true);
    setError(null);

    try {
      const created = await api.post<Role>(`/api/v1/servers/${props.serverId}/roles`, { name });
      const newRole = { ...created, id: String(created.id) };
      setRoles([...roles(), newRole]);
      setNewRoleName('');
      setShowCreateForm(false);
      selectRole(newRole);
    } catch (err: unknown) {
      const e = err as { detail?: string; error?: string; message?: string };
      setError(e?.detail ?? e?.error ?? e?.message ?? 'Failed to create role.');
    } finally {
      setIsCreating(false);
    }
  }

  async function handleDeleteRole(roleId: string) {
    setIsDeleting(true);
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/roles/${roleId}`);
      setRoles(roles().filter((r) => r.id !== roleId));
      if (selectedRoleId() === roleId) {
        setSelectedRoleId(null);
      }
    } catch (err: unknown) {
      const e = err as { detail?: string; error?: string; message?: string };
      setError(e?.detail ?? e?.error ?? e?.message ?? 'Failed to delete role.');
    } finally {
      setIsDeleting(false);
    }
  }

  onMount(() => {
    loadRoles();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold text-lg">Roles</h2>
        <button
          type="button"
          onClick={() => { setShowCreateForm(true); setSaveSuccess(''); setSaveError(''); }}
          class="px-3 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
        >
          Create Role
        </button>
      </div>

      <Show when={error()}>
        <div role="alert" class="px-4 py-2 bg-red-500/20 text-red-400 text-sm border-b border-xcord-border">
          {error()}
        </div>
      </Show>

      <div class="flex flex-1 min-h-0">
        {/* Role list */}
        <div class="w-56 border-r border-xcord-border overflow-y-auto flex-shrink-0">
          <Show when={isLoading()}>
            <div class="flex items-center justify-center h-24">
              <p class="text-xcord-text-muted text-sm">Loading roles...</p>
            </div>
          </Show>

          <Show when={!isLoading() && roles().length === 0}>
            <p class="text-xcord-text-muted text-sm px-4 py-6 text-center">No roles yet.</p>
          </Show>

          <For each={roles()}>
            {(role) => (
              <button
                type="button"
                onClick={() => { selectRole(role); setShowCreateForm(false); }}
                class={`w-full flex items-center gap-2.5 px-3 py-2.5 text-left transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none ${
                  selectedRoleId() === role.id
                    ? 'bg-xcord-bg-primary text-white'
                    : 'text-xcord-text-secondary hover:bg-xcord-bg-primary/50 hover:text-white'
                }`}
                aria-pressed={selectedRoleId() === role.id}
              >
                {/* Color dot */}
                <span
                  class="w-3 h-3 rounded-full flex-shrink-0"
                  style={{ 'background-color': role.color || '#5865f2' }}
                  aria-hidden="true"
                />
                <span class="truncate text-sm">{role.name}</span>
              </button>
            )}
          </For>
        </div>

        {/* Editor pane */}
        <div class="flex-1 overflow-y-auto">
          {/* Create role form */}
          <Show when={showCreateForm()}>
            <form onSubmit={handleCreateRole} class="px-5 py-4 border-b border-xcord-border">
              <h3 class="text-white font-semibold mb-3">Create New Role</h3>
              <div class="mb-3">
                <label for="new-role-name" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Role Name <span class="text-red-400">*</span>
                </label>
                <input
                  id="new-role-name"
                  type="text"
                  required
                  maxlength="100"
                  value={newRoleName()}
                  onInput={(e) => setNewRoleName(e.currentTarget.value)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                  placeholder="New Role"
                />
              </div>
              <div class="flex gap-2">
                <button
                  type="submit"
                  disabled={isCreating()}
                  class="px-4 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded disabled:opacity-50 transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                >
                  {isCreating() ? 'Creating...' : 'Create'}
                </button>
                <button
                  type="button"
                  onClick={() => { setShowCreateForm(false); setNewRoleName(''); }}
                  class="px-4 py-1.5 text-xcord-text-muted hover:text-white text-sm rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                >
                  Cancel
                </button>
              </div>
            </form>
          </Show>

          {/* Role edit form */}
          <Show when={selectedRole() && !showCreateForm()}>
            {(role) => (
              <form onSubmit={handleSaveRole} class="px-5 py-4">
                <div class="flex items-center justify-between mb-5">
                  <h3 class="text-white font-semibold">Edit Role</h3>

                  {/* Delete button */}
                  <button
                    type="button"
                    onClick={() => setShowDeleteConfirm(true)}
                    class="text-red-400 hover:text-red-300 text-sm transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none rounded"
                  >
                    Delete Role
                  </button>
                </div>

                {/* Role name */}
                <div class="mb-5">
                  <label for="edit-role-name" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                    Role Name
                  </label>
                  <input
                    id="edit-role-name"
                    type="text"
                    required
                    maxlength="100"
                    value={editName()}
                    onInput={(e) => setEditName(e.currentTarget.value)}
                    class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                  />
                </div>

                {/* Color picker */}
                <div class="mb-5">
                  <label class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-2">
                    Role Color
                  </label>

                  {/* Preset color swatches */}
                  <div class="flex flex-wrap gap-2 mb-2" role="group" aria-label="Preset colors">
                    <For each={PRESET_COLORS}>
                      {(color) => (
                        <button
                          type="button"
                          aria-label={`Color ${color}`}
                          aria-pressed={editColor() === color}
                          onClick={() => setEditColor(color)}
                          class={`w-7 h-7 rounded-full transition-transform hover:scale-110 focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none ${
                            editColor() === color ? 'ring-2 ring-white ring-offset-1 ring-offset-xcord-bg-secondary' : ''
                          }`}
                          style={{ 'background-color': color }}
                        />
                      )}
                    </For>
                  </div>

                  {/* Hex input */}
                  <div class="flex items-center gap-2 mt-2">
                    <span
                      class="w-8 h-8 rounded border border-xcord-border flex-shrink-0"
                      style={{ 'background-color': editColor() }}
                      aria-hidden="true"
                    />
                    <label for="color-hex" class="text-xcord-text-muted text-xs sr-only">Hex color</label>
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
                      class="w-28 bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand font-mono"
                    />
                  </div>
                </div>

                {/* Permissions */}
                <div class="mb-5">
                  <label class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-2">
                    Permissions
                  </label>
                  <div class="space-y-2">
                    <For each={PERMISSION_FLAGS}>
                      {(perm) => (
                        <label class="flex items-center gap-3 cursor-pointer group">
                          <input
                            type="checkbox"
                            checked={hasPermission(editPermissions(), perm.bit)}
                            onChange={() => setEditPermissions(togglePermission(editPermissions(), perm.bit))}
                            class="w-4 h-4 rounded border-xcord-border bg-xcord-bg-tertiary text-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand cursor-pointer"
                          />
                          <span class="text-sm text-xcord-text-secondary group-hover:text-xcord-text-primary transition-colors">
                            {perm.label}
                          </span>
                        </label>
                      )}
                    </For>
                  </div>
                </div>

                {/* Status messages */}
                <Show when={saveSuccess()}>
                  <div role="status" class="mb-4 px-4 py-2 bg-green-500/20 border border-green-500/30 rounded text-green-400 text-sm">
                    {saveSuccess()}
                  </div>
                </Show>
                <Show when={saveError()}>
                  <div role="alert" class="mb-4 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">
                    {saveError()}
                  </div>
                </Show>

                {/* Save button */}
                <div class="flex justify-end">
                  <button
                    type="submit"
                    disabled={isSaving()}
                    class="px-5 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                  >
                    {isSaving() ? 'Saving...' : 'Save Changes'}
                  </button>
                </div>
              </form>
            )}
          </Show>

          {/* Empty state */}
          <Show when={!selectedRole() && !showCreateForm()}>
            <div class="flex flex-col items-center justify-center py-8 text-center">
              <p class="text-xcord-text-muted text-sm">Select a role to edit, or create a new one.</p>
            </div>
          </Show>
        </div>
      </div>

      <Modal
        open={showDeleteConfirm()}
        onClose={() => setShowDeleteConfirm(false)}
        title="Delete Role"
        size="sm"
        role="alertdialog"
      >
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-4">
            Are you sure you want to delete the role "{selectedRole()?.name}"? Members with this role will lose its permissions.
          </p>
          <div class="flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm text-xcord-text-primary bg-xcord-bg-primary hover:bg-xcord-bg-tertiary rounded transition-colors"
              onClick={() => setShowDeleteConfirm(false)}
            >
              Cancel
            </button>
            <button
              class="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded transition-colors disabled:opacity-50"
              disabled={isDeleting()}
              onClick={() => {
                handleDeleteRole(selectedRoleId()!);
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
