import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';

export type BroadcastStatus = 'Starting' | 'Live' | 'Ended' | 'Failed';
export type BroadcastLayoutPreset = 'Grid' | 'Spotlight' | 'Pip' | 'SideBySide' | 'AudioShow';
export type BroadcastStreambotStatus = 'Connecting' | 'Active' | 'Failed' | 'Ended';

export interface StageSlot {
  userId: string;
  slotIndex: number;
}

export interface BroadcastStreambotInfo {
  streambotId: string;
  name: string;
  status: BroadcastStreambotStatus;
  lastError?: string;
}

export interface Broadcast {
  id: string;
  channelId: string;
  hostUserId: string;
  layoutPreset: BroadcastLayoutPreset;
  status: BroadcastStatus;
  hlsUrl: string;
  roomName: string;
  startedAt: string;
  endedAt?: string;
  stageSlots: StageSlot[];
  streambots: BroadcastStreambotInfo[];
}

interface StartBroadcastResponse {
  broadcastId: string;
  publishToken: string;
  roomName: string;
  livekitUrl: string;
  hlsUrl: string;
}

interface HostCredentials {
  broadcastId: string;
  token: string;
  roomName: string;
  livekitUrl: string;
}

interface GuestCredentials {
  broadcastId: string;
  token: string;
  roomName: string;
  livekitUrl: string;
}

const store = createRoot(() => {
  // Active broadcast per channel (most channels will have 0 or 1).
  const [activeByChannel, setActiveByChannel] = createSignal<Record<string, Broadcast>>({});
  const [hostCredentials, setHostCredentials] = createSignal<HostCredentials | null>(null);
  const [guestCredentials, setGuestCredentials] = createSignal<GuestCredentials | null>(null);
  return {
    activeByChannel,
    setActiveByChannel,
    hostCredentials,
    setHostCredentials,
    guestCredentials,
    setGuestCredentials,
  };
});

async function loadActiveBroadcastInternal(channelId: string): Promise<Broadcast | null> {
  try {
    const list = await api.get<Broadcast[]>(`/api/v1/channels/${channelId}/broadcasts?active=true`);
    const active = list[0] ?? null;
    if (active) {
      store.setActiveByChannel(prev => ({ ...prev, [channelId]: active }));
    } else {
      store.setActiveByChannel(prev => {
        const { [channelId]: _removed, ...rest } = prev;
        return rest;
      });
    }
    return active;
  } catch {
    return null;
  }
}

async function reloadBroadcastById(broadcastId: string): Promise<void> {
  // Find which channel currently has this broadcast, so we can refresh it in place.
  const current = store.activeByChannel();
  let channelId: string | null = null;
  for (const cid in current) {
    if (current[cid].id === broadcastId) {
      channelId = cid;
      break;
    }
  }

  if (channelId) {
    await loadActiveBroadcastInternal(channelId);
    return;
  }

  // Broadcast is unknown locally - fetch by id and insert under its channelId.
  try {
    const fresh = await api.get<Broadcast>(`/api/v1/broadcasts/${broadcastId}`);
    if (fresh) {
      store.setActiveByChannel(prev => ({ ...prev, [fresh.channelId]: fresh }));
    }
  } catch {
    // Silent - broadcast may have ended or access was lost.
  }
}

export function useBroadcast() {
  return {
    get activeByChannel() {
      return store.activeByChannel();
    },
    get hostCredentials() {
      return store.hostCredentials();
    },
    get guestCredentials() {
      return store.guestCredentials();
    },

    getActiveBroadcast(channelId: string): Broadcast | undefined {
      return store.activeByChannel()[channelId];
    },

    async loadActiveBroadcast(channelId: string): Promise<Broadcast | null> {
      return loadActiveBroadcastInternal(channelId);
    },

    async startBroadcast(
      channelId: string,
      layoutPreset: BroadcastLayoutPreset,
      streambotIds: string[],
    ): Promise<StartBroadcastResponse> {
      const res = await api.post<StartBroadcastResponse>(
        `/api/v1/channels/${channelId}/broadcasts`,
        { layoutPreset, streambotIds },
      );
      store.setHostCredentials({
        broadcastId: res.broadcastId,
        token: res.publishToken,
        roomName: res.roomName,
        livekitUrl: res.livekitUrl,
      });
      await loadActiveBroadcastInternal(channelId);
      return res;
    },

    async endBroadcast(broadcastId: string): Promise<void> {
      await api.delete(`/api/v1/broadcasts/${broadcastId}`);
      store.setHostCredentials(null);
      // Remove the broadcast from the per-channel map immediately so UI reflects
      // the ended state without waiting for the SignalR event.
      store.setActiveByChannel(prev => {
        const next = { ...prev };
        for (const cid in next) {
          if (next[cid].id === broadcastId) {
            delete next[cid];
          }
        }
        return next;
      });
    },

    async updateLayout(broadcastId: string, preset: BroadcastLayoutPreset): Promise<void> {
      await api.patch(`/api/v1/broadcasts/${broadcastId}/layout`, { preset });
    },

    async setActiveStreambots(broadcastId: string, streambotIds: string[]): Promise<void> {
      await api.put(`/api/v1/broadcasts/${broadcastId}/streambots`, { streambotIds });
    },

    async addToStage(broadcastId: string, userId: string, slotIndex: number): Promise<void> {
      await api.post(`/api/v1/broadcasts/${broadcastId}/stage`, { userId, slotIndex });
    },

    async removeFromStage(broadcastId: string, userId: string): Promise<void> {
      await api.delete(`/api/v1/broadcasts/${broadcastId}/stage/${userId}`);
    },

    async joinAsGuest(broadcastId: string): Promise<{ token: string; roomName: string; livekitUrl: string }> {
      const res = await api.post<{ token: string; roomName: string; livekitUrl: string }>(
        `/api/v1/broadcasts/${broadcastId}/guest-token`,
        {},
      );
      store.setGuestCredentials({ broadcastId, ...res });
      return res;
    },

    clearGuestCredentials(): void {
      store.setGuestCredentials(null);
    },

    clearHostCredentials(): void {
      store.setHostCredentials(null);
    },

    /**
     * Called by signalr.store when receiving broadcast events. The event name
     * is the SignalR method name; data is the raw payload. Handlers here update
     * the per-channel active broadcast in place or reload when the event lacks
     * enough information to apply a precise patch.
     */
    _onSignalREvent(event: string, data: Record<string, unknown>): void {
      switch (event) {
        case 'Broadcast_Started': {
          // The event only carries {broadcastId, hostId, layoutPreset}; fetch the
          // full object so stageSlots/streambots/status are populated.
          const broadcastId = data.broadcastId as string;
          if (broadcastId) {
            void reloadBroadcastById(broadcastId);
          }
          break;
        }
        case 'Broadcast_Ended': {
          const broadcastId = data.broadcastId as string;
          store.setActiveByChannel(prev => {
            const next = { ...prev };
            for (const channelId in next) {
              if (next[channelId].id === broadcastId) {
                delete next[channelId];
              }
            }
            return next;
          });
          // Clear credentials tied to this broadcast so components unmount cleanly.
          const hc = store.hostCredentials();
          if (hc && hc.broadcastId === broadcastId) {
            store.setHostCredentials(null);
          }
          const gc = store.guestCredentials();
          if (gc && gc.broadcastId === broadcastId) {
            store.setGuestCredentials(null);
          }
          break;
        }
        case 'Broadcast_LayoutChanged': {
          const broadcastId = data.broadcastId as string;
          const newPreset = data.newPreset as BroadcastLayoutPreset;
          store.setActiveByChannel(prev => {
            const next = { ...prev };
            for (const channelId in next) {
              if (next[channelId].id === broadcastId) {
                next[channelId] = { ...next[channelId], layoutPreset: newPreset };
              }
            }
            return next;
          });
          break;
        }
        case 'Broadcast_StageChanged': {
          const broadcastId = data.broadcastId as string;
          const slots = (data.slots as StageSlot[]) ?? [];
          store.setActiveByChannel(prev => {
            const next = { ...prev };
            for (const channelId in next) {
              if (next[channelId].id === broadcastId) {
                next[channelId] = { ...next[channelId], stageSlots: slots };
              }
            }
            return next;
          });
          break;
        }
        case 'Broadcast_StreambotsChanged': {
          const broadcastId = data.broadcastId as string;
          const streambots = data.streambots as Array<{ streambotId: string; status: BroadcastStreambotStatus }> | undefined;
          if (streambots) {
            store.setActiveByChannel(prev => {
              const next = { ...prev };
              for (const channelId in next) {
                if (next[channelId].id === broadcastId) {
                  const existing = next[channelId].streambots;
                  // Merge: keep existing `name`/`lastError`, replace `status`, add new entries.
                  const byId = new Map(existing.map(b => [b.streambotId, b]));
                  const merged: BroadcastStreambotInfo[] = streambots.map(update => {
                    const prior = byId.get(update.streambotId);
                    return {
                      streambotId: update.streambotId,
                      name: prior?.name ?? '',
                      status: update.status,
                      lastError: prior?.lastError,
                    };
                  });
                  next[channelId] = { ...next[channelId], streambots: merged };
                }
              }
              return next;
            });
          } else if (broadcastId) {
            // Payload shape unexpected - fall back to a reload so state stays correct.
            void reloadBroadcastById(broadcastId);
          }
          break;
        }
        case 'Broadcast_StreambotStatusChanged': {
          const broadcastId = data.broadcastId as string;
          const streambotId = data.streambotId as string;
          const status = data.status as BroadcastStreambotStatus;
          const error = data.error as string | undefined;
          store.setActiveByChannel(prev => {
            const next = { ...prev };
            for (const channelId in next) {
              if (next[channelId].id === broadcastId) {
                const updated = next[channelId].streambots.map(b =>
                  b.streambotId === streambotId
                    ? { ...b, status, lastError: error ?? b.lastError }
                    : b,
                );
                next[channelId] = { ...next[channelId], streambots: updated };
              }
            }
            return next;
          });
          break;
        }
        case 'Broadcast_StatusChanged': {
          const broadcastId = data.broadcastId as string;
          const status = data.status as BroadcastStatus;
          store.setActiveByChannel(prev => {
            const next = { ...prev };
            for (const channelId in next) {
              if (next[channelId].id === broadcastId) {
                next[channelId] = { ...next[channelId], status };
                if (status === 'Ended' || status === 'Failed') {
                  delete next[channelId];
                }
              }
            }
            return next;
          });
          if (status === 'Ended' || status === 'Failed') {
            const hc = store.hostCredentials();
            if (hc && hc.broadcastId === broadcastId) {
              store.setHostCredentials(null);
            }
            const gc = store.guestCredentials();
            if (gc && gc.broadcastId === broadcastId) {
              store.setGuestCredentials(null);
            }
          }
          break;
        }
      }
    },

    reset(): void {
      store.setActiveByChannel({});
      store.setHostCredentials(null);
      store.setGuestCredentials(null);
    },
  };
}
