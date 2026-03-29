import { For, Show, onMount, createSignal } from 'solid-js';
import { useDms } from '../stores/dm.store';
import { useAuth } from '../stores/auth.store';
import PresenceDot from './PresenceDot';
import { getErrorMessage } from '../utils/errors';
import styles from './DmList.module.css';

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
      const dm = await dmStore.createDmByUsername(dmUsername());
      dmStore.selectDm(dm.id);
      setDmUsername('');
      setShowNewDm(false);
    } catch (err: unknown) {
      setDmError(getErrorMessage(err, 'Failed to open DM'));
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
      setGroupError(getErrorMessage(err, 'Failed to create group DM'));
    }
  };

  const handleAddMemberToGroup = async (groupId: string, username: string) => {
    setAddMemberError('');
    try {
      await dmStore.addGroupMemberByUsername(groupId, username);
      setAddMemberInput('');
    } catch (err: unknown) {
      setAddMemberError(getErrorMessage(err, 'Failed to add member'));
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
    <div class={styles.container}>
      <div class={styles.header}>
        <div class={styles.headerRow}>
          <h2 data-testid="dm-heading" class={styles.headerTitle}>Direct Messages</h2>
          <div class={styles.headerActions}>
            <button
              data-testid="dm-new-message-button"
              class={styles.headerActionBtn}
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
              data-testid="dm-new-group-button"
              id="new-group-dm-btn"
              class={styles.headerActionBtn}
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
          <div class={styles.dmForm}>
            <input
              id="new-dm-input"
              data-testid="dm-username-input"
              type="text"
              placeholder="Enter a username"
              value={dmUsername()}
              onInput={(e) => setDmUsername(e.currentTarget.value)}
              class={styles.dmInput}
            />
            <button
              data-testid="dm-start-button"
              class={styles.dmStartBtn}
              disabled={!dmUsername().trim()}
              onClick={handleCreateDm}
            >
              Start
            </button>
          </div>
          <Show when={dmError()}>
            <p class={styles.dmError} id="dm-error">{dmError()}</p>
          </Show>
        </Show>

        {/* Group DM creation */}
        <Show when={showNewDm() && dmMode() === 'group'}>
          <div class={styles.groupForm}>
            <input
              id="group-dm-name-input"
              type="text"
              placeholder="Group name (optional)"
              value={groupName()}
              onInput={(e) => setGroupName(e.currentTarget.value)}
              class={styles.groupInput}
            />
            <div class={styles.groupMemberRow}>
              <input
                id="group-dm-member-input"
                type="text"
                placeholder="Add member by username"
                value={groupMemberInput()}
                onInput={(e) => setGroupMemberInput(e.currentTarget.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAddGroupMember(); } }}
                class={styles.groupMemberInput}
              />
              <button
                id="group-dm-add-member-btn"
                class={styles.addMemberBtn}
                disabled={!groupMemberInput().trim()}
                onClick={handleAddGroupMember}
              >
                Add
              </button>
            </div>
            <Show when={groupMembers().length > 0}>
              <div id="group-dm-member-list" class={styles.memberChips}>
                <For each={groupMembers()}>
                  {(username) => (
                    <span class={styles.memberChip}>
                      {username}
                      <button
                        class={styles.memberChipRemove}
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
              class={styles.createGroupBtn}
              disabled={groupMembers().length < 2}
              onClick={handleCreateGroup}
            >
              Create Group
            </button>
            <Show when={groupError()}>
              <p class={styles.groupError} id="group-dm-error">{groupError()}</p>
            </Show>
          </div>
        </Show>
      </div>

      {/* Group management panel */}
      <Show when={managingGroup()}>
        {(group) => (
          <div id="group-dm-management" class={styles.managementPanel}>
            <div class={styles.managementHeader}>
              <h3 class={styles.managementTitle}>Manage: {group().name}</h3>
              <button
                class={styles.managementCloseBtn}
                onClick={() => setManagingGroupId(null)}
              >
                Close
              </button>
            </div>
            <p class={styles.managementMemberCount}>{group().memberIds.length} members</p>
            <div class={styles.managementAddRow}>
              <input
                id="group-add-member-input"
                type="text"
                placeholder="Add member by username"
                value={addMemberInput()}
                onInput={(e) => setAddMemberInput(e.currentTarget.value)}
                class={styles.managementAddInput}
              />
              <button
                id="group-add-member-btn"
                class={styles.managementAddBtn}
                disabled={!addMemberInput().trim()}
                onClick={() => handleAddMemberToGroup(group().id, addMemberInput())}
              >
                Add
              </button>
            </div>
            <Show when={addMemberError()}>
              <p class={styles.managementAddError} id="group-add-member-error">{addMemberError()}</p>
            </Show>
            <button
              id="leave-group-dm-btn"
              class={styles.leaveGroupBtn}
              onClick={() => handleLeaveGroup(group().id)}
            >
              Leave Group
            </button>
            <Show when={group().ownerId === auth.user?.id}>
              <div class={styles.removeMembersSection}>
                <p class={styles.removeMembersLabel}>Members (click to remove):</p>
                <For each={group().members}>
                  {(member) => (
                    <Show when={member.userId !== auth.user?.id}>
                      <button
                        class={styles.removeMemberBtn}
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

      <div class={styles.listArea}>
        <Show when={dmStore.isLoading}>
          <div class={styles.loadingCenter}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <For each={dmStore.dmChannels}>
          {(dm) => (
            <button
              data-testid={`dm-channel-item-${dm.recipientUsername}`}
              class={dmStore.selectedDmId === dm.id
                ? `${styles.dmChannelBtn} ${styles.dmChannelBtnActive}`
                : styles.dmChannelBtn}
              onClick={() => dmStore.selectDm(dm.id)}
            >
              <div class={styles.avatarWrapper}>
                <div class={styles.avatar}>
                  <Show when={dm.recipientAvatarUrl} fallback={dm.recipientUsername.charAt(0).toUpperCase()}>
                    <img
                      src={dm.recipientAvatarUrl}
                      alt={dm.recipientUsername}
                      class={styles.avatarImg}
                    />
                  </Show>
                </div>
                <PresenceDot userId={dm.recipientId} size="md" />
              </div>

              <div class={styles.dmInfo}>
                <div class={styles.dmInfoRow}>
                  <span class={styles.dmUsername}>{dm.recipientUsername}</span>
                  <span class={styles.dmTime}>{formatTime(dm.lastMessageAt)}</span>
                </div>
              </div>
            </button>
          )}
        </For>

        <For each={dmStore.dmGroups}>
          {(group) => (
            <div
              class={dmStore.selectedDmId === group.id
                ? `${styles.groupDmRow} ${styles.groupDmRowActive}`
                : styles.groupDmRow}
            >
              <button
                class={styles.groupDmBtn}
                onClick={() => dmStore.selectDm(group.id)}
              >
                <div class={styles.avatar}>
                  <Show when={group.iconUrl} fallback={group.name.charAt(0).toUpperCase()}>
                    <img
                      src={group.iconUrl}
                      alt={group.name}
                      class={styles.avatarImg}
                    />
                  </Show>
                </div>

                <div class={styles.groupDmInfo}>
                  <div class={styles.groupDmInfoRow}>
                    <span class={styles.groupDmName}>{group.name}</span>
                    <span class={styles.groupDmTime}>{formatTime(group.lastMessageAt)}</span>
                  </div>
                  <p class={styles.groupDmMemberCount}>{group.memberIds.length} members</p>
                </div>
              </button>

              {/* Manage button for group DM */}
              <button
                class={styles.manageGroupBtn}
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
