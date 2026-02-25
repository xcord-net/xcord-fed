import { For, Show, onMount, createSignal } from 'solid-js';
import { useDms } from '../stores/dm.store';
import { useAuth } from '../stores/auth.store';
import PresenceDot from './PresenceDot';

export default function DmList() {
  const dmStore = useDms();
  const auth = useAuth();
  const [showNewDm, setShowNewDm] = createSignal(false);
  const [dmMode, setDmMode] = createSignal<'single' | 'group'>('single');
  const [dmUsername, setDmUsername] = createSignal('');
  const [dmError, setDmError] = createSignal('');

  // Group DM creation state
  const [groupName, setGroupName] = createSignal('');
  const [groupMembers, setGroupMembers] = createSignal<string[]>([]);
  const [groupMemberInput, setGroupMemberInput] = createSignal('');
  const [groupError, setGroupError] = createSignal('');

  // Group DM management panel
  const [managingGroupId, setManagingGroupId] = createSignal<string | null>(null);
  const [addMemberInput, setAddMemberInput] = createSignal('');
  const [addMemberError, setAddMemberError] = createSignal('');

  onMount(() => {
    dmStore.loadDms();
  });

  const handleCreateDm = async () => {
    setDmError('');
    try {
      await dmStore.createDmByUsername(dmUsername());
      setDmUsername('');
      setShowNewDm(false);
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setDmError(e?.detail || e?.message || 'Failed to open DM');
    }
  };

  const handleAddGroupMember = () => {
    const username = groupMemberInput().trim();
    if (!username) return;
    if (groupMembers().includes(username)) {
      setGroupError('Already added');
      return;
    }
    setGroupMembers([...groupMembers(), username]);
    setGroupMemberInput('');
    setGroupError('');
  };

  const handleRemoveGroupMember = (username: string) => {
    setGroupMembers(groupMembers().filter((m) => m !== username));
  };

  const handleCreateGroup = async () => {
    setGroupError('');
    if (groupMembers().length < 2) {
      setGroupError('Add at least 2 other members');
      return;
    }
    try {
      // Resolve usernames to IDs via backend
      await dmStore.createDmGroupByUsernames(groupMembers(), groupName() || 'Group DM');
      setGroupMembers([]);
      setGroupName('');
      setGroupMemberInput('');
      setShowNewDm(false);
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setGroupError(e?.detail || e?.message || 'Failed to create group DM');
    }
  };

  const handleAddMemberToGroup = async (groupId: string, username: string) => {
    setAddMemberError('');
    try {
      await dmStore.addGroupMemberByUsername(groupId, username);
      setAddMemberInput('');
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setAddMemberError(e?.detail || e?.message || 'Failed to add member');
    }
  };

  const handleRemoveMemberFromGroup = async (groupId: string, userId: string) => {
    try {
      await dmStore.removeGroupMember(groupId, userId);
    } catch (err: unknown) {
      console.error('Failed to remove member', err);
    }
  };

  const handleLeaveGroup = async (groupId: string) => {
    try {
      await dmStore.leaveDm(groupId);
      setManagingGroupId(null);
    } catch (err: unknown) {
      console.error('Failed to leave group', err);
    }
  };

  const formatTime = (dateString?: string) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diff = now.getTime() - date.getTime();

    if (diff < 60000) return 'Just now';
    if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`;
    if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`;
    return date.toLocaleDateString();
  };

  const managingGroup = () =>
    managingGroupId() ? dmStore.dmGroups.find((g) => g.id === managingGroupId()) : null;

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <div class="flex items-center justify-between">
          <h2 class="text-white font-semibold">Direct Messages</h2>
          <div class="flex space-x-2">
            <button
              class="text-xcord-text-muted hover:text-white text-sm"
              onClick={() => {
                if (showNewDm() && dmMode() === 'single') {
                  setShowNewDm(false);
                } else {
                  setDmMode('single');
                  setShowNewDm(true);
                }
              }}
            >
              {showNewDm() && dmMode() === 'single' ? 'Cancel' : 'New Message'}
            </button>
            <button
              id="new-group-dm-btn"
              class="text-xcord-text-muted hover:text-white text-sm"
              onClick={() => {
                if (showNewDm() && dmMode() === 'group') {
                  setShowNewDm(false);
                } else {
                  setDmMode('group');
                  setShowNewDm(true);
                }
              }}
            >
              {showNewDm() && dmMode() === 'group' ? 'Cancel' : 'New Group'}
            </button>
          </div>
        </div>

        {/* 1:1 DM creation */}
        <Show when={showNewDm() && dmMode() === 'single'}>
          <div class="mt-2 flex space-x-2">
            <input
              id="new-dm-input"
              type="text"
              placeholder="Enter a username"
              value={dmUsername()}
              onInput={(e) => setDmUsername(e.currentTarget.value)}
              class="flex-1 bg-xcord-bg-primary text-white px-3 py-1.5 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <button
              class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50"
              disabled={!dmUsername().trim()}
              onClick={handleCreateDm}
            >
              Start
            </button>
          </div>
          <Show when={dmError()}>
            <p class="text-red-400 text-sm mt-2" id="dm-error">{dmError()}</p>
          </Show>
        </Show>

        {/* Group DM creation */}
        <Show when={showNewDm() && dmMode() === 'group'}>
          <div class="mt-2 flex flex-col space-y-2">
            <input
              id="group-dm-name-input"
              type="text"
              placeholder="Group name (optional)"
              value={groupName()}
              onInput={(e) => setGroupName(e.currentTarget.value)}
              class="bg-xcord-bg-primary text-white px-3 py-1.5 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <div class="flex space-x-2">
              <input
                id="group-dm-member-input"
                type="text"
                placeholder="Add member by username"
                value={groupMemberInput()}
                onInput={(e) => setGroupMemberInput(e.currentTarget.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAddGroupMember(); } }}
                class="flex-1 bg-xcord-bg-primary text-white px-3 py-1.5 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
              />
              <button
                id="group-dm-add-member-btn"
                class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50"
                disabled={!groupMemberInput().trim()}
                onClick={handleAddGroupMember}
              >
                Add
              </button>
            </div>
            <Show when={groupMembers().length > 0}>
              <div id="group-dm-member-list" class="flex flex-wrap gap-1">
                <For each={groupMembers()}>
                  {(username) => (
                    <span class="bg-xcord-bg-primary text-white text-xs px-2 py-1 rounded flex items-center gap-1">
                      {username}
                      <button
                        class="text-xcord-text-muted hover:text-white"
                        onClick={() => handleRemoveGroupMember(username)}
                        aria-label={`Remove ${username}`}
                      >
                        ✕
                      </button>
                    </span>
                  )}
                </For>
              </div>
            </Show>
            <button
              id="create-group-dm-btn"
              class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50"
              disabled={groupMembers().length < 2}
              onClick={handleCreateGroup}
            >
              Create Group
            </button>
            <Show when={groupError()}>
              <p class="text-red-400 text-sm" id="group-dm-error">{groupError()}</p>
            </Show>
          </div>
        </Show>
      </div>

      {/* Group management panel */}
      <Show when={managingGroup()}>
        {(group) => (
          <div id="group-dm-management" class="px-4 py-3 border-b border-xcord-border bg-xcord-bg-primary">
            <div class="flex items-center justify-between mb-2">
              <h3 class="text-white text-sm font-semibold">Manage: {group().name}</h3>
              <button
                class="text-xcord-text-muted hover:text-white text-xs"
                onClick={() => setManagingGroupId(null)}
              >
                Close
              </button>
            </div>
            <p class="text-xcord-text-muted text-xs mb-2">{group().memberIds.length} members</p>
            <div class="flex space-x-2 mb-2">
              <input
                id="group-add-member-input"
                type="text"
                placeholder="Add member by username"
                value={addMemberInput()}
                onInput={(e) => setAddMemberInput(e.currentTarget.value)}
                class="flex-1 bg-xcord-bg-secondary text-white px-2 py-1 rounded text-xs focus:outline-none focus:ring-1 focus:ring-xcord-brand"
              />
              <button
                id="group-add-member-btn"
                class="bg-xcord-brand text-white px-2 py-1 rounded text-xs hover:bg-xcord-brand-hover disabled:opacity-50"
                disabled={!addMemberInput().trim()}
                onClick={() => handleAddMemberToGroup(group().id, addMemberInput())}
              >
                Add
              </button>
            </div>
            <Show when={addMemberError()}>
              <p class="text-red-400 text-xs mb-2" id="group-add-member-error">{addMemberError()}</p>
            </Show>
            <button
              id="leave-group-dm-btn"
              class="w-full bg-red-600 text-white px-3 py-1 rounded text-xs hover:bg-red-700 mt-1"
              onClick={() => handleLeaveGroup(group().id)}
            >
              Leave Group
            </button>
            <Show when={group().ownerId === auth.user?.id}>
              <div class="mt-2">
                <p class="text-xcord-text-muted text-xs mb-1">Members (click to remove):</p>
                <For each={group().members}>
                  {(member) => (
                    <Show when={member.userId !== auth.user?.id}>
                      <button
                        class="text-xs text-red-400 hover:text-red-300 block"
                        data-member-id={member.userId}
                        data-testid={`remove-member-${member.username}`}
                        onClick={() => handleRemoveMemberFromGroup(group().id, member.userId)}
                      >
                        Remove {member.username}
                      </button>
                    </Show>
                  )}
                </For>
              </div>
            </Show>
          </div>
        )}
      </Show>

      <div class="flex-1 overflow-y-auto">
        <Show when={dmStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <For each={dmStore.dmChannels}>
          {(dm) => (
            <button
              class={`w-full px-4 py-2 flex items-center space-x-3 hover:bg-xcord-bg-primary/50 transition ${
                dmStore.selectedDmId === dm.id ? 'bg-xcord-bg-primary' : ''
              }`}
              onClick={() => dmStore.selectDm(dm.id)}
            >
              <div class="relative flex-shrink-0">
                <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold">
                  <Show when={dm.recipientAvatarUrl} fallback={dm.recipientUsername.charAt(0).toUpperCase()}>
                    <img
                      src={dm.recipientAvatarUrl}
                      alt={dm.recipientUsername}
                      class="w-full h-full rounded-full object-cover"
                    />
                  </Show>
                </div>
                <PresenceDot userId={dm.recipientId} size="md" />
              </div>

              <div class="flex-1 text-left min-w-0">
                <div class="flex items-center justify-between">
                  <span class="text-white font-medium truncate">{dm.recipientUsername}</span>
                  <span class="text-xs text-xcord-text-muted">{formatTime(dm.lastMessageAt)}</span>
                </div>
              </div>
            </button>
          )}
        </For>

        <For each={dmStore.dmGroups}>
          {(group) => (
            <div
              class={`w-full px-4 py-2 flex items-center space-x-3 hover:bg-xcord-bg-primary/50 transition cursor-pointer ${
                dmStore.selectedDmId === group.id ? 'bg-xcord-bg-primary' : ''
              }`}
            >
              <button
                class="flex items-center space-x-3 flex-1 min-w-0 text-left"
                onClick={() => dmStore.selectDm(group.id)}
              >
                <div class="w-10 h-10 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold flex-shrink-0">
                  <Show when={group.iconUrl} fallback={group.name.charAt(0).toUpperCase()}>
                    <img
                      src={group.iconUrl}
                      alt={group.name}
                      class="w-full h-full rounded-full object-cover"
                    />
                  </Show>
                </div>

                <div class="flex-1 text-left min-w-0">
                  <div class="flex items-center justify-between">
                    <span class="text-white font-medium truncate">{group.name}</span>
                    <span class="text-xs text-xcord-text-muted">{formatTime(group.lastMessageAt)}</span>
                  </div>
                  <p class="text-xs text-xcord-text-muted">{group.memberIds.length} members</p>
                </div>
              </button>

              {/* Manage button for group DM */}
              <button
                class="text-xcord-text-muted hover:text-white text-xs flex-shrink-0 px-1"
                title="Manage group"
                aria-label={`Manage ${group.name}`}
                onClick={(e) => {
                  e.stopPropagation();
                  setManagingGroupId(managingGroupId() === group.id ? null : group.id);
                  setAddMemberInput('');
                  setAddMemberError('');
                }}
              >
                ...
              </button>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
