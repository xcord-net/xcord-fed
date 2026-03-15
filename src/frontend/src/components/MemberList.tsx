import { For, Show, createEffect, createMemo, createSignal } from 'solid-js';
import { useMembers } from '../stores/member.store';
import { useServers } from '../stores/server.store';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import PresenceDot from './PresenceDot';
import Modal from './ui/Modal';
import Menu from './ui/Menu';

interface ServerGroup {
  id: string;
  name: string;
  color: string;
  roles: number;
  position: number;
}

export default function MemberList() {
  const memberStore = useMembers();
  const serverStore = useServers();
  const auth = useAuth();
  const [contextMenuUserId, setContextMenuUserId] = createSignal<string | null>(null);
  const [contextMenuPos, setContextMenuPos] = createSignal<{ x: number; y: number }>({ x: 0, y: 0 });
  const [showGroupAssignment, setShowGroupAssignment] = createSignal(false);
  const [serverGroups, setServerGroups] = createSignal<ServerGroup[]>([]);
  const [groupAssignmentLoading, setGroupAssignmentLoading] = createSignal(false);
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

  // Only the server owner can ban members from the member list context menu.
  // (Moderators with the BanMembers permission would require a full permission
  // fetch per-server - owner check is sufficient for this UI guard.)
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
      // Pick the highest-position group (already sorted by backend, but sort defensively)
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
    try {
      const groups = await api.get<ServerGroup[]>(`/api/v1/servers/${serverId}/groups`);
      setServerGroups(groups.map((g) => ({ ...g, id: String(g.id) })));
    } catch {
      // Failed to load groups
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
    try {
      if (memberHasGroup(groupId)) {
        await memberStore.removeGroup(serverId, userId, groupId);
      } else {
        await memberStore.assignGroup(serverId, userId, groupId);
      }
      // Refresh members to reflect the change
      await memberStore.fetchMembers(serverId);
    } catch {
      // Group assignment failed
    } finally {
      setGroupAssignmentLoading(false);
    }
  };

  return (
    <div data-testid="member-list-heading" id="member-list-panel" class="w-60 bg-xcord-bg-secondary flex flex-col">
      <div class="flex-1 overflow-y-auto px-4 py-4">
        {/* Loading skeleton while fetching members */}
        <Show when={memberStore.isLoading && memberStore.members.length === 0}>
          <div class="space-y-2">
            <div class="h-3 w-24 rounded bg-xcord-bg-primary animate-pulse mb-3" />
            <For each={[0, 1, 2, 3]}>
              {() => (
                <div class="flex items-center space-x-3 px-2 py-1.5">
                  <div class="w-8 h-8 rounded-full bg-xcord-bg-primary animate-pulse flex-shrink-0" />
                  <div class="h-3 rounded bg-xcord-bg-primary animate-pulse flex-1" />
                </div>
              )}
            </For>
          </div>
        </Show>

        <For each={groupedMembers()}>
          {(section) => (
            <div class="mb-4">
              {/* Group header */}
              <h3 data-testid="member-group-heading" class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-2">
                {section.groupName} - {section.members.length}
              </h3>

              {/* Members */}
              <div class="space-y-1">
                <For each={section.members}>
                  {(member) => (
                    <button
                      data-testid={`member-item-${member.username}`}
                      class="w-full px-2 py-1.5 rounded flex items-center space-x-3 hover:bg-xcord-bg-primary transition-colors"
                      onContextMenu={(e) => handleContextMenu(e, member.userId)}
                    >
                      {/* Avatar with status indicator */}
                      <div class="relative flex-shrink-0">
                        <div class="w-8 h-8 rounded-full bg-xcord-brand flex items-center justify-center text-white text-sm font-semibold">
                          <Show when={member.avatarUrl} fallback={member.username.charAt(0).toUpperCase()}>
                            <img
                              src={member.avatarUrl}
                              alt={member.username}
                              class="w-full h-full rounded-full object-cover"
                            />
                          </Show>
                        </div>
                        {/* Online status indicator */}
                        <PresenceDot userId={member.userId} size="sm" />
                      </div>

                      {/* Username and display name - apply group color */}
                      <div class="flex-1 min-w-0 text-left">
                        <div
                          class="text-sm truncate"
                          style={{ color: member.groupColor ?? member.groups[0]?.color ?? undefined }}
                          classList={{ 'text-xcord-text-primary': !member.groupColor && !member.groups[0]?.color }}
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
            class="w-full px-3 py-2 text-left text-sm text-xcord-text-secondary hover:bg-xcord-bg-primary hover:text-white transition-colors"
            onClick={() => openGroupAssignment()}
          >
            Manage Groups
          </button>
          <button
            data-testid="member-context-ban"
            type="button"
            role="menuitem"
            class="w-full px-3 py-2 text-left text-sm text-red-400 hover:bg-red-600 hover:text-white transition-colors"
            onClick={() => handleBan(contextMenuUserId()!)}
          >
            Ban
          </button>
        </Show>

        {/* Group assignment panel */}
        <Show when={showGroupAssignment()}>
          <div ref={groupAssignmentPanelRef} class="border-t border-xcord-border mt-1 pt-1 px-2 pb-2 max-h-64 overflow-y-auto" aria-label="Assign groups">
            <p class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide px-1 py-1">Groups</p>
            <Show when={groupAssignmentLoading() && serverGroups().length === 0}>
              <p class="text-xs text-xcord-text-muted px-1 py-1">Loading...</p>
            </Show>
            <For each={serverGroups().filter((g) => g.name !== '@everyone' && g.name !== 'everyone')}>
              {(group) => (
                <label data-testid={`group-assign-item-${group.id}`} class="flex items-center gap-2 px-1 py-1.5 rounded hover:bg-xcord-bg-primary cursor-pointer">
                  <input
                    data-testid={`group-assign-checkbox-${group.id}`}
                    type="checkbox"
                    checked={memberHasGroup(group.id)}
                    disabled={groupAssignmentLoading()}
                    onChange={() => toggleMemberGroup(group.id)}
                    class="w-3.5 h-3.5 rounded border-xcord-border bg-xcord-bg-tertiary text-xcord-brand cursor-pointer"
                    aria-label={`Group: ${group.name}`}
                  />
                  <span
                    class="w-2.5 h-2.5 rounded-full flex-shrink-0"
                    style={{ 'background-color': group.color || '#d4943a' }}
                    aria-hidden="true"
                  />
                  <span class="text-sm text-xcord-text-secondary">{group.name}</span>
                </label>
              )}
            </For>
          </div>
        </Show>
      </Menu>

      {/* Ban confirmation */}
      <Modal data-testid="ban-confirm-dialog" open={showBanConfirm()} onClose={() => { setShowBanConfirm(false); setPendingBanUserId(null); }} title="Ban Member" size="sm" role="alertdialog">
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-6">Are you sure you want to ban this member?</p>
          <div class="flex justify-end gap-3">
            <button
              data-testid="ban-confirm-cancel-button"
              type="button"
              onClick={() => { setShowBanConfirm(false); setPendingBanUserId(null); }}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              Cancel
            </button>
            <button
              data-testid="ban-confirm-submit-button"
              type="button"
              onClick={confirmBan}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
            >
              Ban Member
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
