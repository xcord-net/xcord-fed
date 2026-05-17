import { Show, createSignal, onMount } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import GroupList from './GroupList';
import GroupCreateForm from './GroupCreateForm';
import GroupEditForm from './GroupEditForm';
import DeleteGroupModal from './DeleteGroupModal';
import Flexbox from '../ui/Flexbox';
import styles from './GroupManager.module.css';
import { DEFAULT_GROUP_COLOR } from '../../constants/colors';

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
  const [editColor, setEditColor] = createSignal(DEFAULT_GROUP_COLOR);
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
    setEditColor(group.color || DEFAULT_GROUP_COLOR);
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
    <Flexbox direction="vertical" class={styles.container}>
      {/* Header */}
      <Flexbox align="center" justify="between" class={styles.header}>
        <h2 data-testid="group-manager-heading" class={styles.heading}>Groups</h2>
        <button
          data-testid="create-group-button"
          type="button"
          onClick={() => { setShowCreateForm(true); setSaveSuccess(''); setSaveError(''); }}
          class={styles.createButton}
        >
          Create Group
        </button>
      </Flexbox>

      <Show when={error()}>
        <div role="alert" class={styles.errorBanner}>
          {error()}
        </div>
      </Show>

      <Flexbox class={styles.body}>
        {/* Group list */}
        <GroupList
          groups={groups()}
          isLoading={isLoading()}
          selectedGroupId={selectedGroupId()}
          onSelect={(group) => { selectGroup(group); setShowCreateForm(false); }}
        />

        {/* Editor pane */}
        <div class={styles.editorPane}>
          {/* Create group form */}
          <Show when={showCreateForm()}>
            <GroupCreateForm
              newGroupName={newGroupName()}
              isCreating={isCreating()}
              onNameInput={(value) => setNewGroupName(value)}
              onSubmit={handleCreateGroup}
              onCancel={() => { setShowCreateForm(false); setNewGroupName(''); }}
            />
          </Show>

          {/* Group edit form */}
          <Show when={selectedGroup() && !showCreateForm()}>
            <GroupEditForm
              editName={editName()}
              editColor={editColor()}
              editRoles={editRoles()}
              isSaving={isSaving()}
              saveSuccess={saveSuccess()}
              saveError={saveError()}
              onNameInput={(value) => setEditName(value)}
              onColorChange={(value) => setEditColor(value)}
              onRolesChange={(value) => setEditRoles(value)}
              onDeleteClick={() => setShowDeleteConfirm(true)}
              onSubmit={handleSaveGroup}
            />
          </Show>

          {/* Empty state */}
          <Show when={!selectedGroup() && !showCreateForm()}>
            <Flexbox direction="vertical" align="center" justify="center" class={styles.editorEmpty}>
              <p class={styles.mutedText}>Select a group to edit, or create a new one.</p>
            </Flexbox>
          </Show>
        </div>
      </Flexbox>

      <DeleteGroupModal
        open={showDeleteConfirm()}
        groupName={selectedGroup()?.name}
        isDeleting={isDeleting()}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={() => {
          handleDeleteGroup(selectedGroupId()!);
          setShowDeleteConfirm(false);
        }}
      />
    </Flexbox>
  );
}
