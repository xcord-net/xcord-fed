import { api } from '../../api/client';

export const focusChannelButton = (channelId: string) => {
  const el = document.querySelector<HTMLElement>(`[data-channel-id="${channelId}"]`);
  el?.focus();
};

/** Build the keydown handler for the channel listbox. Pure function of
 *  callbacks/getters so it stays testable and out of the parent shell. */
export function makeChannelListKeyDown(args: {
  visibleChannelIds: () => string[];
  focusedChannelId: () => string | null;
  setFocusedChannelId: (id: string) => void;
  selectChannel: (id: string) => void;
}) {
  return (e: KeyboardEvent) => {
    const ids = args.visibleChannelIds();
    if (ids.length === 0) return;
    const current = args.focusedChannelId();
    const currentIdx = current !== null ? ids.indexOf(current) : -1;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const next = currentIdx < ids.length - 1 ? ids[currentIdx + 1] : ids[0];
      args.setFocusedChannelId(next);
      focusChannelButton(next);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prev = currentIdx > 0 ? ids[currentIdx - 1] : ids[ids.length - 1];
      args.setFocusedChannelId(prev);
      focusChannelButton(prev);
    } else if (e.key === 'Home') {
      e.preventDefault();
      args.setFocusedChannelId(ids[0]);
      focusChannelButton(ids[0]);
    } else if (e.key === 'End') {
      e.preventDefault();
      args.setFocusedChannelId(ids[ids.length - 1]);
      focusChannelButton(ids[ids.length - 1]);
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      if (current !== null) args.selectChannel(current);
    }
  };
}

/** Toggle a channel's favorite status with the same optimistic-update +
 *  revert-on-failure semantics as the original Sidebar. */
export async function toggleFavoriteChannel(args: {
  serverId: string;
  channelId: string;
  current: Set<string>;
  setFavorites: (s: Set<string>) => void;
}): Promise<void> {
  const next = new Set(args.current);
  if (next.has(args.channelId)) next.delete(args.channelId); else next.add(args.channelId);
  args.setFavorites(next);
  try {
    await api.put(`/api/v1/servers/${args.serverId}/favorites`, {
      favoriteChannelIds: [...next],
    });
  } catch {
    // Revert on failure
    if (next.has(args.channelId)) next.delete(args.channelId); else next.add(args.channelId);
    args.setFavorites(new Set(next));
  }
}
