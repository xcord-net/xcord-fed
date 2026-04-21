import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';

export type StreamBotPlatform = 'YouTube' | 'Twitch' | 'Rumble' | 'Custom';

export interface StreamBot {
  id: string;
  channelId: string;
  name: string;
  platform: StreamBotPlatform;
  rtmpUrl: string;
  isDefault: boolean;
  hasStreamKey: boolean;
  createdAt: string;
}

export interface CreateStreamBotRequest {
  name: string;
  platform: StreamBotPlatform;
  rtmpUrl: string;
  streamKey: string;
  isDefault: boolean;
}

export interface UpdateStreamBotRequest {
  name?: string;
  rtmpUrl?: string;
  streamKey?: string;
  isDefault?: boolean;
}

export interface StreamBotTestResult {
  success: boolean;
  error?: string;
}

const store = createRoot(() => {
  const [byChannel, setByChannel] = createSignal<Record<string, StreamBot[]>>({});
  return { byChannel, setByChannel };
});

export function useStreambot() {
  return {
    get byChannel() {
      return store.byChannel();
    },

    getForChannel(channelId: string): StreamBot[] {
      return store.byChannel()[channelId] ?? [];
    },

    async load(channelId: string): Promise<StreamBot[]> {
      const bots = await api.get<StreamBot[]>(`/api/v1/channels/${channelId}/streambots`);
      store.setByChannel(prev => ({ ...prev, [channelId]: bots }));
      return bots;
    },

    async create(channelId: string, req: CreateStreamBotRequest): Promise<StreamBot> {
      const bot = await api.post<StreamBot>(`/api/v1/channels/${channelId}/streambots`, req);
      store.setByChannel(prev => ({
        ...prev,
        [channelId]: [...(prev[channelId] ?? []), bot],
      }));
      return bot;
    },

    async update(channelId: string, id: string, req: UpdateStreamBotRequest): Promise<StreamBot> {
      const bot = await api.patch<StreamBot>(`/api/v1/streambots/${id}`, req);
      store.setByChannel(prev => ({
        ...prev,
        [channelId]: (prev[channelId] ?? []).map(b => (b.id === id ? bot : b)),
      }));
      return bot;
    },

    async delete(channelId: string, id: string): Promise<void> {
      await api.delete(`/api/v1/streambots/${id}`);
      store.setByChannel(prev => ({
        ...prev,
        [channelId]: (prev[channelId] ?? []).filter(b => b.id !== id),
      }));
    },

    async test(id: string): Promise<StreamBotTestResult> {
      return api.post<StreamBotTestResult>(`/api/v1/streambots/${id}/test`, {});
    },

    reset(): void {
      store.setByChannel({});
    },
  };
}
