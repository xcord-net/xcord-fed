import { useServers } from './server.store';
import { useChannels } from './channel.store';
import { useMessages } from './message.store';
import { useMembers } from './member.store';
import { useDms } from './dm.store';
import { useFriends } from './friend.store';
import { useBlocks } from './block.store';
import { useNotifications } from './notification.store';
import { useCalls } from './call.store';
import { useThreads } from './thread.store';
import { usePins } from './pin.store';
import { useSearch } from './search.store';
import { useEmojis } from './emoji.store';
import { useForums } from './forum.store';
import { useProfiles } from './profile.store';
import { usePresence } from './presence.store';
import { useTyping } from './typing.store';
import { useVoice } from './voice.store';
import { useUnread } from './unread.store';
import { useBroadcast } from './broadcast.store';
import { useStreambot } from './streambot.store';
import { useSignalR } from './signalr.store';

/**
 * Resets all application stores to their initial state.
 * Called during logout to prevent data leakage between sessions.
 */
export async function resetAllStores(): Promise<void> {
  // Disconnect SignalR first so no events arrive during reset
  await useSignalR().reset();

  // Reset all data stores
  useServers().reset();
  useChannels().reset();
  useMessages().reset();
  useMembers().reset();
  useDms().reset();
  useFriends().reset();
  useBlocks().reset();
  useNotifications().reset();
  useCalls().reset();
  useThreads().reset();
  usePins().reset();
  useSearch().reset();
  useEmojis().reset();
  useForums().reset();
  useProfiles().reset();
  usePresence().reset();
  useTyping().reset();
  useVoice().reset();
  useUnread().reset();
  useBroadcast().reset();
  useStreambot().reset();
}
