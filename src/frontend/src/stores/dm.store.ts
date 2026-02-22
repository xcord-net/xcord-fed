import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import { useAuth } from './auth.store';
import type { DmChannel, DmGroup, DmGroupMember } from '../types/dm';

// Raw DTO types returned by the backend — internal to the store.
interface RawDmMemberDto {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  joinedAt: string;
}

interface RawMessagePreviewDto {
  messageId: string;
  authorId?: string;
  content: string;
  createdAt: string;
}

interface RawDmChannelDto {
  id: string;
  conversationId: string;
  isGroup: boolean;
  name?: string;
  ownerId?: string;
  members: RawDmMemberDto[];
  lastMessage?: RawMessagePreviewDto;
}

interface RawCreateDmByUsernameResponse {
  id: string;
  conversationId: string;
  recipientId: string;
  recipientUsername: string;
  recipientAvatarUrl?: string;
  createdAt: string;
}

const store = createRoot(() => {
  const [dmChannels, setDmChannels] = createSignal<DmChannel[]>([]);
  const [dmGroups, setDmGroups] = createSignal<DmGroup[]>([]);
  const [selectedDmId, setSelectedDmId] = createSignal<string | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);

  return {
    dmChannels,
    setDmChannels,
    dmGroups,
    setDmGroups,
    selectedDmId,
    setSelectedDmId,
    isLoading,
    setIsLoading,
  };
});

export function useDms() {
  return {
    get dmChannels() { return store.dmChannels(); },
    get dmGroups() { return store.dmGroups(); },
    get selectedDmId() { return store.selectedDmId(); },
    get isLoading() { return store.isLoading(); },

    async loadDms(): Promise<void> {
      store.setIsLoading(true);
      try {
        // Backend returns DmChannelDto[] — a flat array with a Members property.
        // We map each entry to DmChannel (1:1) or DmGroup (group) based on isGroup.
        const raw = await api.get<RawDmChannelDto[]>('/api/v1/users/@me/dms');
        // Get current user ID from the auth store.
        const currentUserId = useAuth().user?.id;
        const channels: DmChannel[] = [];
        const groups: DmGroup[] = [];
        for (const dto of raw) {
          if (dto.isGroup) {
            groups.push({
              id: dto.id,
              conversationId: dto.conversationId,
              name: dto.name ?? 'Group DM',
              iconUrl: undefined,
              ownerId: dto.ownerId ?? '',
              memberIds: dto.members.map((m) => m.userId),
              members: dto.members.map((m): DmGroupMember => ({ userId: m.userId, username: m.username })),
              lastMessageAt: dto.lastMessage?.createdAt,
              createdAt: dto.lastMessage?.createdAt ?? new Date(0).toISOString(),
            });
          } else {
            // For 1:1 DMs, pick the other member as recipient.
            const recipient = dto.members.find((m) => m.userId !== currentUserId) ?? dto.members[0];
            channels.push({
              id: dto.id,
              conversationId: dto.conversationId,
              recipientId: recipient?.userId ?? '',
              recipientUsername: recipient?.username ?? '',
              recipientAvatarUrl: recipient?.avatarUrl,
              lastMessageAt: dto.lastMessage?.createdAt,
              createdAt: dto.lastMessage?.createdAt ?? new Date(0).toISOString(),
            });
          }
        }
        store.setDmChannels(channels);
        store.setDmGroups(groups);
      } finally {
        store.setIsLoading(false);
      }
    },

    async createDm(recipientId: string): Promise<DmChannel> {
      const dm = await api.post<DmChannel>('/api/v1/users/@me/dms', { recipientId });
      store.setDmChannels([...store.dmChannels(), dm]);
      return dm;
    },

    async createDmByUsername(username: string): Promise<DmChannel> {
      // Backend returns CreateDmByUsernameResponse which maps directly to DmChannel.
      const raw = await api.post<RawCreateDmByUsernameResponse>('/api/v1/dms', { username });
      const dm: DmChannel = {
        id: raw.id,
        conversationId: raw.conversationId,
        recipientId: raw.recipientId,
        recipientUsername: raw.recipientUsername,
        recipientAvatarUrl: raw.recipientAvatarUrl,
        lastMessageAt: undefined,
        createdAt: raw.createdAt,
      };
      // Avoid duplicate if already in list (backend returns existing DM idempotently)
      if (!store.dmChannels().some((c) => c.id === dm.id)) {
        store.setDmChannels([...store.dmChannels(), dm]);
      }
      return dm;
    },

    async createDmGroup(memberIds: string[], name: string): Promise<DmGroup> {
      const group = await api.post<DmGroup>('/api/v1/users/@me/dms', { memberIds, name });
      store.setDmGroups([...store.dmGroups(), group]);
      return group;
    },

    async createDmGroupByUsernames(usernames: string[], name: string): Promise<DmGroup> {
      const raw = await api.post<{
        id: string;
        conversationId: string;
        isGroup: boolean;
        name?: string;
        ownerId?: string;
        members: RawDmMemberDto[];
      }>('/api/v1/dms/group', { usernames, name });
      const group: DmGroup = {
        id: raw.id,
        conversationId: raw.conversationId,
        name: raw.name ?? name,
        iconUrl: undefined,
        ownerId: raw.ownerId ?? '',
        memberIds: raw.members.map((m) => m.userId),
        members: raw.members.map((m): DmGroupMember => ({ userId: m.userId, username: m.username })),
        lastMessageAt: undefined,
        createdAt: new Date().toISOString(),
      };
      if (!store.dmGroups().some((g) => g.id === group.id)) {
        store.setDmGroups([...store.dmGroups(), group]);
      }
      return group;
    },

    async addGroupMemberByUsername(dmChannelId: string, username: string): Promise<void> {
      await api.put(`/api/v1/users/@me/dms/${dmChannelId}/members`, { username });
      // Reload DMs to get updated member list
      await this.loadDms();
    },

    async closeDm(dmId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/dms/${dmId}`);
      store.setDmChannels(store.dmChannels().filter((dm) => dm.id !== dmId));
    },

    async leaveDm(dmChannelId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/dms/${dmChannelId}`);
      store.setDmChannels(store.dmChannels().filter((dm) => dm.id !== dmChannelId));
      store.setDmGroups(store.dmGroups().filter((g) => g.id !== dmChannelId));
      if (store.selectedDmId() === dmChannelId) {
        store.setSelectedDmId(null);
      }
    },

    async addGroupMember(dmChannelId: string, userId: string): Promise<void> {
      await api.put(`/api/v1/users/@me/dms/${dmChannelId}/members/${userId}`, {});
      // Update local state: add userId to the group's memberIds
      store.setDmGroups(
        store.dmGroups().map((g) =>
          g.id === dmChannelId && !g.memberIds.includes(userId)
            ? { ...g, memberIds: [...g.memberIds, userId] }
            : g
        )
      );
    },

    async removeGroupMember(dmChannelId: string, userId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/dms/${dmChannelId}/members/${userId}`);
      // Update local state: remove userId from the group's memberIds and members
      store.setDmGroups(
        store.dmGroups().map((g) =>
          g.id === dmChannelId
            ? {
                ...g,
                memberIds: g.memberIds.filter((id) => id !== userId),
                members: g.members.filter((m) => m.userId !== userId),
              }
            : g
        )
      );
    },

    selectDm(id: string | null): void {
      store.setSelectedDmId(id);
    },

    updateDmLastMessage(dmId: string, lastMessageAt: string): void {
      store.setDmChannels(
        store.dmChannels().map((dm) =>
          dm.id === dmId ? { ...dm, lastMessageAt } : dm
        )
      );
    },

    reset(): void {
      store.setDmChannels([]);
      store.setDmGroups([]);
      store.setSelectedDmId(null);
      store.setIsLoading(false);
    },
  };
}
