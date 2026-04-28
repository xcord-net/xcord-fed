import { Show, createMemo } from 'solid-js';
import { useParams } from '@solidjs/router';
import ChannelDirectory from '../ChannelDirectory';
import ChannelSettings from '../ChannelSettings';
import GroupManager from '../GroupManager';
import HubHeader from '../HubHeader';
import Modal from '../ui/Modal';
import Sidebar from '../Sidebar';
// Note: `ServerSettings` is rendered by `Sidebar`, not here.
import { useChannels } from '../../stores/channel.store';
import { useDms } from '../../stores/dm.store';
import { useModals } from '../../stores/modal.store';
import { useServers } from '../../stores/server.store';
import { useSignalR } from '../../stores/signalr.store';
import ChannelHeader from './ChannelHeader';
import DmView from './DmView';
import MessagesArea from './MessagesArea';
import RightPanels from './RightPanels';
import SettingsModal from './SettingsModal';
import { useLayoutWiring } from './useLayoutWiring';
import styles from './Layout.module.css';

export default function Layout() {
  const params = useParams<{ serverId?: string; channelId?: string }>();
  const serverStore = useServers();
  const channelStore = useChannels();
  const signalR = useSignalR();
  const modals = useModals();
  const dmStore = useDms();

  const { hubUrl } = useLayoutWiring();

  const isDmView = createMemo(() => params.serverId === 'me');

  const currentChannel = () =>
    channelStore.channels.find((c) => c.id === channelStore.selectedChannelId);

  const conversationId = () => currentChannel()?.conversationId;

  /** True when the current channel's conversation has been successfully joined on the SignalR hub. */
  const conversationJoined = () => {
    const convId = conversationId();
    if (!convId) return false;
    return signalR.currentConversations.has(convId);
  };

  /** Get conversation ID for the selected DM channel (when in DM view). */
  const dmConversationId = createMemo(() => {
    if (!isDmView() || !params.channelId) return undefined;
    const dm = dmStore.dmChannels.find((d) => d.id === params.channelId);
    return dm?.conversationId;
  });

  return (
    <div class={styles.root} data-signalr-connected={String(signalR.isConnected)} data-signalr-conversation-joined={String(conversationJoined())}>
      <Show when={hubUrl()}>
        <HubHeader hubUrl={hubUrl()!} instanceUrl={window.location.origin} />
      </Show>
      <div class={styles.mainRow}>
        {/* Unified sidebar - always present */}
        <Sidebar />

        {/* DM view, channel directory, or channel view */}
        <Show when={isDmView()}>
          <DmView channelId={params.channelId} dmConversationId={dmConversationId()} />
        </Show>
        <Show when={!isDmView()}>
          <Show
            when={params.channelId}
            fallback={
              <Show when={params.serverId} fallback={
                <div class={styles.emptyState}>
                  <p class={styles.emptyStateText}>Loading...</p>
                </div>
              }>
                <ChannelDirectory serverId={params.serverId!} />
              </Show>
            }
          >
            {/* Channel selected: show messages */}
            <div class={styles.channelView}>
              <Show
                when={conversationId()}
                fallback={
                  <div class={styles.emptyState}>
                    <p class={styles.emptyStateText}>Select a channel to start chatting</p>
                  </div>
                }
              >
                {(convId) => (
                  <>
                    <ChannelHeader
                      channelName={currentChannel()?.name}
                      channelTopic={currentChannel()?.topic}
                    />

                    {/* Messages + right panels row */}
                    <div class={styles.contentRow}>
                      <MessagesArea
                        conversationId={convId()}
                        serverId={params.serverId}
                        channelId={params.channelId}
                      />
                      <RightPanels
                        conversationId={conversationId()}
                        channelId={params.channelId}
                      />
                    </div>
                  </>
                )}
              </Show>
            </div>
          </Show>
        </Show>
      </div>{/* end mainRow */}

      {/* Settings modal */}
      <SettingsModal />

      {/* Channel Settings modal */}
      <Show when={modals.showChannelSettings && channelStore.selectedChannelId && serverStore.selectedServerId}>
        <ChannelSettings
          serverId={serverStore.selectedServerId!}
          channelId={channelStore.selectedChannelId!}
          onClose={() => modals.closeChannelSettings()}
        />
      </Show>

      {/* Group Manager modal */}
      <Modal data-testid="group-manager-modal" open={modals.showGroupManager && !!serverStore.selectedServerId} onClose={() => modals.closeGroupManager()} aria-label="Group Manager" size="xl">
        <div class={styles.groupManagerContent}>
          <GroupManager serverId={serverStore.selectedServerId!} />
        </div>
      </Modal>

      {/* Server Settings modal is rendered by Sidebar */}
    </div>
  );
}
