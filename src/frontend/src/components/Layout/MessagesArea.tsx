import { Show } from 'solid-js';
import BroadcastChannel from '../BroadcastChannel';
import ForumPostList from '../ForumPostList';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import ScreenShareViewer from '../ScreenShareViewer';
import TypingIndicator from '../TypingIndicator';
import { useChannels } from '../../stores/channel.store';
import { useMessages } from '../../stores/message.store';
import { useModals } from '../../stores/modal.store';
import { Capability, hasCapability } from '../../types/channel';
import styles from './MessagesArea.module.css';

interface MessagesAreaProps {
  conversationId: string;
  serverId: string | undefined;
  channelId: string | undefined;
}

export default function MessagesArea(props: MessagesAreaProps) {
  const channelStore = useChannels();
  const messageStore = useMessages();
  const modals = useModals();

  const currentChannel = () =>
    channelStore.channels.find((c) => c.id === channelStore.selectedChannelId);

  return (
    <div class={styles.messagesArea}>
      <Show when={currentChannel()?.capabilities && hasCapability(currentChannel()!.capabilities, Capability.Streaming)}>
        <BroadcastChannel
          channelId={props.channelId!}
          canManageBroadcasts={true}
        />
      </Show>
      <Show when={currentChannel()?.capabilities && hasCapability(currentChannel()!.capabilities, Capability.Forum) && !hasCapability(currentChannel()!.capabilities, Capability.Streaming)}>
        <Show when={!modals.selectedForumPost}>
          <ForumPostList
            serverId={props.serverId!}
            channelId={props.channelId!}
            onSelectPost={(post) => modals.selectForumPost(post)}
          />
        </Show>
        <Show when={modals.selectedForumPost}>
          {(post) => (
            <div data-testid="forum-thread-view" class={styles.forumThreadView}>
              {/* Thread header with back button */}
              <div class={styles.forumThreadHeader}>
                <button
                  data-testid="forum-back-button"
                  class={styles.forumBackButton}
                  onClick={() => modals.selectForumPost(null)}
                  aria-label="Back to forum posts"
                >
                  &larr; Back
                </button>
                <h3 class={styles.forumPostTitle}>{post().title}</h3>
              </div>
              <MessageList conversationId={post().conversationId} />
              <MessageCompose conversationId={post().conversationId} channelId={props.channelId} />
            </div>
          )}
        </Show>
      </Show>
      <Show when={
        (!currentChannel()?.capabilities || !hasCapability(currentChannel()!.capabilities, Capability.Forum)) &&
        (!currentChannel()?.capabilities || !hasCapability(currentChannel()!.capabilities, Capability.Streaming))
      }>
        <ScreenShareViewer />
        <MessageList conversationId={props.conversationId} />
        <TypingIndicator conversationId={props.conversationId} />
        <Show
          when={messageStore.editingMessageId}
          fallback={<MessageCompose conversationId={props.conversationId} channelId={props.channelId} />}
        >
          {/* Edit bar replaces compose while editing */}
          <div class={styles.editBarWrapper}>
            <div class={styles.editBarInner}>
              <div class={styles.editBarLabel}>Editing message</div>
              <textarea
                data-testid="message-edit-textarea"
                class={styles.editTextarea}
                value={messageStore.editContent}
                onInput={(e) => messageStore.setEditContent((e.target as HTMLTextAreaElement).value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    messageStore.editMessage(props.conversationId, messageStore.editingMessageId!, messageStore.editContent);
                  }
                  if (e.key === 'Escape') messageStore.cancelEditing();
                }}
                rows={2}
              />
              <div class={styles.editBarHint}>Enter to save, Escape to cancel</div>
            </div>
          </div>
        </Show>
      </Show>
    </div>
  );
}
