import { createSignal, createRoot } from 'solid-js';
import { api } from '../api/client';
import type { Tier, CreateTierInput, UpdateTierInput } from '../types/tier';

interface ListTiersResponse {
  tiers: Tier[];
}

function normalizeGroupIds(t: Tier): Tier {
  // groupIds come back as JSON-encoded long[]; the snowflake JSON converter
  // serializes them as strings, but be defensive in case numbers slip through.
  return {
    ...t,
    groupIds: (t.groupIds ?? []).map((g) => String(g)),
  };
}

const store = createRoot(() => {
  const [tiersByServer, setTiersByServer] = createSignal<Record<string, Tier[]>>({});
  const [loadingByServer, setLoadingByServer] = createSignal<Record<string, boolean>>({});

  return {
    tiersByServer, setTiersByServer,
    loadingByServer, setLoadingByServer,
  };
});

function setLoading(serverId: string, loading: boolean): void {
  store.setLoadingByServer({ ...store.loadingByServer(), [serverId]: loading });
}

function setTiers(serverId: string, tiers: Tier[]): void {
  store.setTiersByServer({ ...store.tiersByServer(), [serverId]: tiers });
}

export function useTiers() {
  return {
    get tiersByServer() { return store.tiersByServer(); },
    get loadingByServer() { return store.loadingByServer(); },

    tiers(serverId: string): Tier[] {
      return store.tiersByServer()[serverId] ?? [];
    },

    isLoading(serverId: string): boolean {
      return store.loadingByServer()[serverId] ?? false;
    },

    async fetchTiers(serverId: string): Promise<void> {
      setLoading(serverId, true);
      try {
        const res = await api.get<ListTiersResponse>(`/api/v1/servers/${serverId}/tiers`);
        setTiers(serverId, (res.tiers ?? []).map(normalizeGroupIds));
      } finally {
        setLoading(serverId, false);
      }
    },

    async createTier(serverId: string, input: CreateTierInput): Promise<Tier> {
      const created = await api.post<Tier>(`/api/v1/servers/${serverId}/tiers`, {
        name: input.name,
        description: input.description ?? null,
        priceMonthly: input.priceMonthly,
        currency: input.currency ?? 'usd',
        groupIds: (input.groupIds ?? []).map((g) => Number(g)),
      });
      const normalized = normalizeGroupIds(created);
      const current = store.tiersByServer()[serverId] ?? [];
      setTiers(serverId, [...current, normalized]);
      return normalized;
    },

    async updateTier(serverId: string, tierId: string, input: UpdateTierInput): Promise<Tier> {
      const updated = await api.patch<Tier>(`/api/v1/servers/${serverId}/tiers/${tierId}`, {
        name: input.name ?? null,
        description: input.description ?? null,
        priceMonthly: input.priceMonthly ?? null,
        groupIds: input.groupIds != null ? input.groupIds.map((g) => Number(g)) : null,
        isActive: input.isActive ?? null,
      });
      const normalized = normalizeGroupIds(updated);
      const current = store.tiersByServer()[serverId] ?? [];
      setTiers(serverId, current.map((t) => (t.id === tierId ? normalized : t)));
      return normalized;
    },

    async deleteTier(serverId: string, tierId: string): Promise<void> {
      await api.delete(`/api/v1/servers/${serverId}/tiers/${tierId}`);
      const current = store.tiersByServer()[serverId] ?? [];
      setTiers(serverId, current.filter((t) => t.id !== tierId));
    },

    reset(): void {
      store.setTiersByServer({});
      store.setLoadingByServer({});
    },
  };
}
