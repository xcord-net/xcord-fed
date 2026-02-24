import { For, Show, createMemo, createSignal } from 'solid-js';
import { useMembers } from '../stores/member.store';
import { useServers } from '../stores/server.store';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import PresenceDot from './PresenceDot';

interface ServerRole {
  id: string;
  name: string;
  color: string;
  permissions: number;
  position: number;
}

export default function MemberList() {
  const memberStore = useMembers();
  const serverStore = useServers();
  const auth = useAuth();
  const [contextMenuUserId, setContextMenuUserId] = createSignal<string | null>(null);
  const [contextMenuPos, setContextMenuPos] = createSignal<{ x: number; y: number }>({ x: 0, y: 0 });
  const [showRoleAssignment, setShowRoleAssignment] = createSignal(false);
  const [serverRoles, setServerRoles] = createSignal<ServerRole[]>([]);
  const [roleAssignmentLoading, setRoleAssignmentLoading] = createSignal(false);

  // Only the server owner can ban members from the member list context menu.
  // (Moderators with the BanMembers permission would require a full permission
  // fetch per-server — owner check is sufficient for this UI guard.)
  const canBan = () => {
    const currentServer = serverStore.servers.find(
      (s) => s.id === serverStore.selectedServerId,
    );
    const userId = auth.user?.id;
    return !!currentServer && !!userId && currentServer.ownerId === userId;
  };

  // Group members by role
  const groupedMembers = createMemo(() => {
    const groups: Record<string, typeof memberStore.members> = {};

    memberStore.members.forEach((member) => {
      const role = member.roles[0]?.name || 'Members';
      if (!groups[role]) {
        groups[role] = [];
      }
      groups[role].push(member);
    });

    return Object.entries(groups).map(([role, members]) => ({ role, members }));
  });

  const handleContextMenu = (e: MouseEvent, userId: string) => {
    e.preventDefault();
    setContextMenuUserId(userId);
    setContextMenuPos({ x: e.clientX, y: e.clientY });
  };

  const handleBan = async (userId: string) => {
    const serverId = serverStore.selectedServerId;
    if (!serverId) return;
    setContextMenuUserId(null);
    if (confirm('Are you sure you want to ban this member?')) {
      await memberStore.banMember(serverId, userId);
    }
  };

  const openRoleAssignment = async () => {
    const serverId = serverStore.selectedServerId;
    if (!serverId) return;
    setRoleAssignmentLoading(true);
    try {
      const roles = await api.get<ServerRole[]>(`/api/v1/servers/${serverId}/roles`);
      setServerRoles(roles.map((r) => ({ ...r, id: String(r.id) })));
    } catch {
      // Failed to load roles
    } finally {
      setRoleAssignmentLoading(false);
    }
    setShowRoleAssignment(true);
  };

  const memberHasRole = (roleId: string): boolean => {
    const userId = contextMenuUserId();
    if (!userId) return false;
    const member = memberStore.members.find((m) => m.userId === userId);
    return member?.roles.some((r) => r.id === roleId) ?? false;
  };

  const toggleMemberRole = async (roleId: string) => {
    const serverId = serverStore.selectedServerId;
    const userId = contextMenuUserId();
    if (!serverId || !userId) return;
    setRoleAssignmentLoading(true);
    try {
      if (memberHasRole(roleId)) {
        await memberStore.removeRole(serverId, userId, roleId);
      } else {
        await memberStore.assignRole(serverId, userId, roleId);
      }
      // Refresh members to reflect the change
      await memberStore.fetchMembers(serverId);
    } catch {
      // Role assignment failed
    } finally {
      setRoleAssignmentLoading(false);
    }
  };

  return (
    <div id="member-list-panel" class="w-60 bg-xcord-bg-secondary flex flex-col">
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
          {(group) => (
            <div class="mb-4">
              {/* Role header */}
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-2">
                {group.role} — {group.members.length}
              </h3>

              {/* Members */}
              <div class="space-y-1">
                <For each={group.members}>
                  {(member) => (
                    <button
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

                      {/* Username and display name — Card 170: apply role color */}
                      <div class="flex-1 min-w-0 text-left">
                        <div
                          class="text-sm truncate"
                          style={{ color: member.roleColor ?? member.roles[0]?.color ?? undefined }}
                          classList={{ 'text-xcord-text-primary': !member.roleColor && !member.roles[0]?.color }}
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
      <Show when={contextMenuUserId()}>
        {/* Click-outside overlay */}
        <div
          class="fixed inset-0 z-40"
          onClick={() => { setContextMenuUserId(null); setShowRoleAssignment(false); }}
          aria-hidden="true"
        />
        <div
          class="fixed z-50 bg-xcord-bg-tertiary rounded shadow-lg border border-xcord-border py-1 min-w-[180px]"
          style={{ left: `${contextMenuPos().x}px`, top: `${contextMenuPos().y}px` }}
          role="menu"
        >
          <Show when={canBan()}>
            <button
              type="button"
              role="menuitem"
              class="w-full px-3 py-2 text-left text-sm text-xcord-text-secondary hover:bg-xcord-bg-primary hover:text-white transition-colors"
              onClick={() => openRoleAssignment()}
            >
              Manage Roles
            </button>
            <button
              type="button"
              role="menuitem"
              class="w-full px-3 py-2 text-left text-sm text-red-400 hover:bg-red-600 hover:text-white transition-colors"
              onClick={() => handleBan(contextMenuUserId()!)}
            >
              Ban
            </button>
          </Show>

          {/* Role assignment panel */}
          <Show when={showRoleAssignment()}>
            <div class="border-t border-xcord-border mt-1 pt-1 px-2 pb-2" aria-label="Assign roles">
              <p class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide px-1 py-1">Roles</p>
              <Show when={roleAssignmentLoading() && serverRoles().length === 0}>
                <p class="text-xs text-xcord-text-muted px-1 py-1">Loading...</p>
              </Show>
              <For each={serverRoles().filter((r) => r.name !== '@everyone' && r.name !== 'everyone')}>
                {(role) => (
                  <label class="flex items-center gap-2 px-1 py-1.5 rounded hover:bg-xcord-bg-primary cursor-pointer">
                    <input
                      type="checkbox"
                      checked={memberHasRole(role.id)}
                      disabled={roleAssignmentLoading()}
                      onChange={() => toggleMemberRole(role.id)}
                      class="w-3.5 h-3.5 rounded border-xcord-border bg-xcord-bg-tertiary text-xcord-brand cursor-pointer"
                      aria-label={`Role: ${role.name}`}
                    />
                    <span
                      class="w-2.5 h-2.5 rounded-full flex-shrink-0"
                      style={{ 'background-color': role.color || '#5865f2' }}
                      aria-hidden="true"
                    />
                    <span class="text-sm text-xcord-text-secondary">{role.name}</span>
                  </label>
                )}
              </For>
            </div>
          </Show>
        </div>
      </Show>
    </div>
  );
}
