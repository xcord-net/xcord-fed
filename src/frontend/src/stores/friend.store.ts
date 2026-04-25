import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { Friend, FriendRequest } from '../types/friend';

interface FriendshipDto {
  id: string;
  senderId: string;
  senderUsername: string;
  senderDisplayName: string;
  senderAvatarUrl?: string;
  receiverId: string;
  receiverUsername: string;
  receiverDisplayName: string;
  receiverAvatarUrl?: string;
  status: 'Pending' | 'Accepted';
  createdAt: string;
}

const store = createRoot(() => {
  const [friends, setFriends] = createSignal<Friend[]>([]);
  const [incomingRequests, setIncomingRequests] = createSignal<FriendRequest[]>([]);
  const [outgoingRequests, setOutgoingRequests] = createSignal<FriendRequest[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [currentUserId, setCurrentUserId] = createSignal<string | null>(null);

  return {
    friends,
    setFriends,
    incomingRequests,
    setIncomingRequests,
    outgoingRequests,
    setOutgoingRequests,
    isLoading,
    setIsLoading,
    currentUserId,
    setCurrentUserId,
  };
});

function friendshipToFriend(dto: FriendshipDto, myUserId: string): Friend {
  const isReceiver = dto.senderId === myUserId;
  return {
    friendshipId: dto.id,
    userId: isReceiver ? dto.receiverId : dto.senderId,
    username: isReceiver ? dto.receiverUsername : dto.senderUsername,
    displayName: isReceiver ? dto.receiverDisplayName : dto.senderDisplayName,
    avatarUrl: isReceiver ? dto.receiverAvatarUrl : dto.senderAvatarUrl,
    status: 'Online',
    createdAt: dto.createdAt,
  };
}

function friendshipToRequest(dto: FriendshipDto): FriendRequest {
  return {
    id: dto.id,
    fromUserId: dto.senderId,
    fromUsername: dto.senderUsername,
    fromAvatarUrl: dto.senderAvatarUrl,
    toUserId: dto.receiverId,
    toUsername: dto.receiverUsername,
    status: dto.status,
    createdAt: dto.createdAt,
  };
}

export function useFriends() {
  return {
    get friends() { return store.friends(); },
    get incomingRequests() { return store.incomingRequests(); },
    get outgoingRequests() { return store.outgoingRequests(); },
    get isLoading() { return store.isLoading(); },

    setCurrentUserId(userId: string): void {
      store.setCurrentUserId(userId);
    },

    async loadFriends(): Promise<void> {
      store.setIsLoading(true);
      try {
        const response = await api.get<{ friendships: FriendshipDto[]; nextCursor?: string | null }>(
          '/api/v1/users/@me/friends?status=Accepted',
        );
        const dtos = response.friendships ?? [];
        const myUserId = store.currentUserId() ?? '';
        store.setFriends(dtos.map((dto) => friendshipToFriend(dto, myUserId)));
      } finally {
        store.setIsLoading(false);
      }
    },

    async loadFriendRequests(): Promise<void> {
      store.setIsLoading(true);
      try {
        const response = await api.get<{ friendships: FriendshipDto[]; nextCursor?: string | null }>(
          '/api/v1/users/@me/friends?status=Pending',
        );
        const dtos = response.friendships ?? [];
        const myUserId = store.currentUserId() ?? '';
        const incoming = dtos.filter((d) => d.receiverId === myUserId).map(friendshipToRequest);
        const outgoing = dtos.filter((d) => d.senderId === myUserId).map(friendshipToRequest);
        store.setIncomingRequests(incoming);
        store.setOutgoingRequests(outgoing);
      } finally {
        store.setIsLoading(false);
      }
    },

    async sendFriendRequest(userId: string): Promise<FriendRequest> {
      const dto = await api.post<FriendshipDto>('/api/v1/users/@me/friends', { userId: Number(userId) });
      const request = friendshipToRequest(dto);
      store.setOutgoingRequests([...store.outgoingRequests(), request]);
      return request;
    },

    async sendFriendRequestByUsername(username: string): Promise<FriendRequest> {
      const dto = await api.post<FriendshipDto>('/api/v1/friends/request', { username });
      const request = friendshipToRequest(dto);
      store.setOutgoingRequests([...store.outgoingRequests(), request]);
      return request;
    },

    async acceptFriendRequest(requestId: string): Promise<void> {
      const dto = await api.put<FriendshipDto>(`/api/v1/users/@me/friends/${requestId}/accept`, {});
      store.setIncomingRequests(store.incomingRequests().filter((r) => r.id !== requestId));
      const myUserId = store.currentUserId() ?? '';
      store.setFriends([...store.friends(), friendshipToFriend(dto, myUserId)]);
    },

    async declineFriendRequest(requestId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/friends/${requestId}`);
      store.setIncomingRequests(store.incomingRequests().filter((r) => r.id !== requestId));
    },

    async cancelFriendRequest(requestId: string): Promise<void> {
      await api.delete(`/api/v1/users/@me/friends/${requestId}`);
      store.setOutgoingRequests(store.outgoingRequests().filter((r) => r.id !== requestId));
    },

    async removeFriend(userId: string): Promise<void> {
      const allFriends = store.friends();
      const friend = allFriends.find((f) => f.userId === userId);
      if (friend) {
        // The backend DELETE endpoint takes the friendship ID, not the user ID.
        await api.delete(`/api/v1/users/@me/friends/${friend.friendshipId}`);
        store.setFriends(allFriends.filter((f) => f.userId !== userId));
      }
    },

    reset(): void {
      store.setFriends([]);
      store.setIncomingRequests([]);
      store.setOutgoingRequests([]);
      store.setIsLoading(false);
    },
  };
}
