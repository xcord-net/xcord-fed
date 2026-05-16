import { For, Show, createEffect, createMemo, createSignal } from 'solid-js';
import { useMembers } from '../stores/member.store';
import { useServers } from '../stores/server.store';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import PresenceDot from './PresenceDot';
import Modal from './ui/Modal';
import Menu from './ui/Menu';
import styles from './MemberList.module.css';
import { DEFAULT_GROUP_COLOR } from '../constants/colors';

interface ServerGroup {
  id: string;
  name: string;
  color: string;
  roles: number;
  position: number;
}

interface MemberListProps {
  open: boolean;
  onClose: () => void;
}

export default function MemberList(props: MemberListProps) {
  const memberStore = useMembers();
  const serverStore = useServers();
  const auth = useAuth();
  const [contextMenuUserId, setContextMenuUserId] = createSignal<string | null>(null);
  const [contextMenuPos, setContextMenuPos] = createSignal<{ x: number; y: number }>({ x: 0, y: 0 });
  const [showGroupAssignment, setShowGroupAssignment] = createSignal(false);
  const [serverGroups, setServerGroups] = createSignal<ServerGroup[]>([]);
  const [groupAssignmentLoading, setGroupAssignmentLoading] = createSignal(false);
  const [groupActionError, setGroupActionError] = createSignal('');
  const [showBanConfirm, setShowBanConfirm] = createSignal(false);
  const [pendingBanUserId, setPendingBanUserId] = createSignal<string | null>(null);
  let groupAssignmentPanelRef!: HTMLDivElement;

  // Move focus into the group assignment panel when it appears
  createEffect(() => {
    if (showGroupAssignment() && groupAssignmentPanelRef) {
      requestAnimationFrame(() => {
        const firstInput = groupAssignmentPanelRef.querySelector<HTMLElement>('input, button, [tabindex]');
        firstInput?.focus();
      });
    }
  });

  const canBan = () => {
    const currentServer = serverStore.servers.find(
      (s) => s.id === serverStore.selectedServerId,
    );
    const userId = auth.user?.id;
    return !!currentServer && !!userId && currentServer.ownerId === userId;
  };

  // Group members by their highest-position custom group
  const groupedMembers = createMemo(() => {
    const sections: Record<string, typeof memberStore.members> = {};

    memberStore.members.forEach((member) => {
      const topGroup = [...member.groups]
        .filter((g) => g.name !== '@everyone' && g.name !== 'everyone')
        .sort((a, b) => b.position - a.position)[0];
      const groupName = topGroup?.name || 'Members';
      if (!sections[groupName]) {
        sections[groupName] = [];
      }
      sections[groupName].push(member);
    });

    return Object.entries(sections).map(([groupName, members]) => ({ groupName, members }));
  });

  const handleContextMenu = (e: MouseEvent, userId: string) => {
    e.preventDefault();
    setContextMenuUserId(userId);
    setContextMenuPos({ x: e.clientX, y: e.clientY });
  };

  const handleBan = (userId: string) => {
    setContextMenuUserId(null);
    setPendingBanUserId(userId);
    setShowBanConfirm(true);
  };

  const confirmBan = async () => {
    const serverId = serverStore.selectedServerId;
    const userId = pendingBanUserId();
    if (!serverId || !userId) return;
    setShowBanConfirm(false);
    setPendingBanUserId(null);
    await memberStore.banMember(serverId, userId);
  };

  const openGroupAssignment = async () => {
    const serverId = serverStore.selectedServerId;
    if (!serverId) return;
    setGroupAssignmentLoading(true);
    setGroupActionError('');
    try {
      const groups = await api.get<ServerGroup[]>(`/api/v1/servers/${serverId}/groups`);
      setServerGroups(groups.map((g) => ({ ...g, id: String(g.id) })));
    } catch (err) {
      console.error('[MemberList] failed to load groups for server', serverId, err);
      setGroupActionError('Failed to load groups. Please try again.');
    } finally {
      setGroupAssignmentLoading(false);
    }
    setShowGroupAssignment(true);
  };

  const memberHasGroup = (groupId: string): boolean => {
    const userId = contextMenuUserId();
    if (!userId) return false;
    const member = memberStore.members.find((m) => m.userId === userId);
    return member?.groups.some((g) => g.id === groupId) ?? false;
  };

  const toggleMemberGroup = async (groupId: string) => {
    const serverId = serverStore.selectedServerId;
    const userId = contextMenuUserId();
    if (!serverId || !userId) return;
    setGroupAssignmentLoading(true);
    setGroupActionError('');
    try {
      if (memberHasGroup(groupId)) {
        await memberStore.removeGroup(serverId, userId, groupId);
      } else {
        await memberStore.assignGroup(serverId, userId, groupId);
      }
      await memberStore.fetchMembers(serverId);
    } catch (err) {
      console.error('[MemberList] group assignment failed for user', userId, 'group', groupId, err);
      setGroupActionError('Failed to update group assignment. Please try again.');
    } finally {
      setGroupAssignmentLoading(false);
    }
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      props.onClose();
    }
  };

  return (
    <>
      <Show when={props.open}>
        <div
          data-testid="member-list-heading"
          id="member-list-panel"
          class={styles.panel}
          style={{ "max-height": "min(400px, 60vh)" }}
          onKeyDown={handleKeyDown}
        >
          <div class={styles.panelScroll} style={{ "max-height": "min(400px, 60vh)" }}>
            {/* Loading skeleton while fetching members */}
            <Show when={memberStore.isLoading && memberStore.members.length === 0}>
              <div class={styles.skeletonList}>
                <div class={styles.skeletonHeading} />
                <For each={[0, 1, 2, 3]}>
                  {() => (
                    <div class={styles.skeletonRow}>
                      <div class={styles.skeletonAvatar} />
                      <div class={styles.skeletonName} />
                    </div>
                  )}
                </For>
              </div>
            </Show>

            <For each={groupedMembers()}>
              {(section) => (
                <div class={styles.groupSection}>
                  {/* Group header */}
                  <h3 data-testid="member-group-heading" class={styles.groupHeading}>
                    {section.groupName} - {section.members.length}
                  </h3>

                  {/* Members */}
                  <div class={styles.memberRows}>
                    <For each={section.members}>
                      {(member) => (
                        <button
                          data-testid={`member-item-${member.username}`}
                          class={styles.memberButton}
                          onContextMenu={(e) => handleContextMenu(e, member.userId)}
                        >
                          {/* Avatar with status indicator */}
                          <div class={styles.avatarWrap}>
                            <div class={styles.memberAvatar}>
                              <Show when={member.avatarUrl} fallback={member.username.charAt(0).toUpperCase()}>
                                <img
                                  src={member.avatarUrl}
                                  alt={member.username}
                                  class={styles.memberAvatarImg}
                                />
                              </Show>
                            </div>
                            <PresenceDot userId={member.userId} size="sm" />
                          </div>

                          {/* Username */}
                          <div class={styles.memberName}>
                            <div
                              class={styles.memberNameText}
                              classList={{ [styles.memberNameTextDefault]: !member.groupColor && !member.groups[0]?.color }}
                              style={{ color: member.groupColor ?? member.groups[0]?.color ?? undefined }}
                            >
                              {member.displayName || member.username}
                            </div>
                          </div>
                        </button>
                      )}
                    </For>
                  </div>
                </div>
              )}
            </For>
          </div>

          {/* Context menu */}
          <Menu
            open={contextMenuUserId() !== null}
            onClose={() => { setContextMenuUserId(null); setShowGroupAssignment(false); }}
            position={contextMenuPos()}
          >
            <Show when={canBan()}>
              <button
                data-testid="member-context-manage-groups"
                type="button"
                role="menuitem"
                class={styles.contextManageGroups}
                onClick={() => openGroupAssignment()}
              >
                Manage Groups
              </button>
              <button
                data-testid="member-context-ban"
                type="button"
                role="menuitem"
                class={styles.contextBan}
                onClick={() => handleBan(contextMenuUserId()!)}
              >
                Ban
              </button>
            </Show>

            {/* Group assignment panel */}
            <Show when={showGroupAssignment()}>
              <div ref={groupAssignmentPanelRef} class={styles.groupAssignPanel} aria-label="Assign groups">
                <p class={styles.groupAssignHeading}>Groups</p>
                <Show when={groupActionError()}>
                  <div class={styles.groupAssignError} role="alert">
                    {groupActionError()}
                  </div>
                </Show>
                <Show when={groupAssignmentLoading() && serverGroups().length === 0}>
                  <p class={styles.groupAssignLoading}>Loading...</p>
                </Show>
                <For each={serverGroups().filter((g) => g.name !== '@everyone' && g.name !== 'everyone')}>
                  {(group) => (
                    <label data-testid={`group-assign-item-${group.id}`} class={styles.groupAssignItem}>
                      <input
                        data-testid={`group-assign-checkbox-${group.id}`}
                        type="checkbox"
                        checked={memberHasGroup(group.id)}
                        disabled={groupAssignmentLoading()}
                        onChange={() => toggleMemberGroup(group.id)}
                        class={styles.groupAssignCheckbox}
                        aria-label={`Group: ${group.name}`}
                      />
                      <span
                        class={styles.groupColorDot}
                        style={{ 'background-color': group.color || DEFAULT_GROUP_COLOR }}
                        aria-hidden="true"
                      />
                      <span class={styles.groupAssignName}>{group.name}</span>
                    </label>
                  )}
                </For>
              </div>
            </Show>
          </Menu>
        </div>
      </Show>

      {/* Ban confirmation */}
      <Modal data-testid="ban-confirm-dialog" open={showBanConfirm()} onClose={() => { setShowBanConfirm(false); setPendingBanUserId(null); }} title="Ban Member" size="sm" role="alertdialog">
        <div class={styles.banModalBody}>
          <p class={styles.banModalText}>Are you sure you want to ban this member?</p>
          <div class={styles.banModalActions}>
            <button
              data-testid="ban-confirm-cancel-button"
              type="button"
              onClick={() => { setShowBanConfirm(false); setPendingBanUserId(null); }}
              class={styles.cancelButton}
            >
              Cancel
            </button>
            <button
              data-testid="ban-confirm-submit-button"
              type="button"
              onClick={confirmBan}
              class={styles.banButton}
            >
              Ban Member
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
