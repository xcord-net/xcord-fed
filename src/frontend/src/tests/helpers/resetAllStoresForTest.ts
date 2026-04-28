import { useAuth } from '../../stores/auth.store';
import { useBlocks } from '../../stores/block.store';
import { useBroadcast } from '../../stores/broadcast.store';
import { useCalls } from '../../stores/call.store';
import { useChannels } from '../../stores/channel.store';
import { useDms } from '../../stores/dm.store';
import { useEmojis } from '../../stores/emoji.store';
import { useForums } from '../../stores/forum.store';
import { useFriends } from '../../stores/friend.store';
import { useMembers } from '../../stores/member.store';
import { useMessages } from '../../stores/message.store';
import { useModals } from '../../stores/modal.store';
import { useNotifications } from '../../stores/notification.store';
import { usePins } from '../../stores/pin.store';
import { usePresence } from '../../stores/presence.store';
import { useProfiles } from '../../stores/profile.store';
import { useSearch } from '../../stores/search.store';
import { useServers } from '../../stores/server.store';
import { useStreambot } from '../../stores/streambot.store';
import { useThreads } from '../../stores/thread.store';
import { useTyping } from '../../stores/typing.store';
import { useUnread } from '../../stores/unread.store';

// Resets every store EXCEPT useVoice() and useSignalR() — those pull in heavy
// transports (livekit-client, @microsoft/signalr) that interfere with vi.mock()
// when imported eagerly from setup.ts. Tests that need them reset must call
// useVoice().reset() / useSignalR().reset() themselves.
export function resetAllStoresForTest(): void {
  useAuth().reset();
  useBlocks().reset();
  useBroadcast().reset();
  useCalls().reset();
  useChannels().reset();
  useDms().reset();
  useEmojis().reset();
  useForums().reset();
  useFriends().reset();
  useMembers().reset();
  useMessages().reset();
  useModals().reset();
  useNotifications().reset();
  usePins().reset();
  usePresence().reset();
  useProfiles().reset();
  useSearch().reset();
  useServers().reset();
  useStreambot().reset();
  useThreads().reset();
  useTyping().reset();
  useUnread().reset();
}
