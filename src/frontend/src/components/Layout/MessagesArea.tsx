import { Show, createEffect, createSignal } from 'solid-js';
import { api } from '../../api/client';
import BroadcastChannel from '../BroadcastChannel';
import ForumPostList from '../ForumPostList';
import MessageCompose from '../MessageCompose';
import MessageList from '../MessageList';
import ScreenShareViewer from '../ScreenShareViewer';
import TypingIndicator from '../TypingIndicator';
import VoiceStage from '../VoiceStage';
import { useChannels } from '../../stores/channel.store';
import { useMessages } from '../../stores/message.store';
import { useModals } from '../../stores/modal.store';
import { Capability, hasCapability } from '../../types/channel';
import Flexbox from '../ui/Flexbox';
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

  /**
   * Whether this person may actually run a broadcast here.
   *
   * This used to be hardcoded true, so every member opening a streaming channel
   * was shown "Start a Broadcast" - an offer the server refuses. The permission
   * has always existed; nothing asked for it.
   */
  const MANAGE_BROADCASTS = 1n << 35n;
  /** Moderator bit - what an announcement channel asks for before you may post. */
  const MANAGE_MESSAGES = 1n << 7n;
  const [canManageBroadcasts, setCanManageBroadcasts] = createSignal(false);
  const [canModerate, setCanModerate] = createSignal(false);
  createEffect(() => {
    const channelId = props.channelId;
    if (!channelId) {
      setCanManageBroadcasts(false);
      setCanModerate(false);
      return;
    }
    api.get<{ permissions: string | number }>(`/api/v1/channels/${channelId}/my-permissions`)
      .then((data) => {
        try {
          const held = BigInt(data.permissions);
          setCanManageBroadcasts((held & MANAGE_BROADCASTS) !== 0n);
          setCanModerate((held & MANAGE_MESSAGES) !== 0n);
        } catch {
          setCanManageBroadcasts(false);
          setCanModerate(false);
        }
      })
      .catch(() => {
        setCanManageBroadcasts(false);
        setCanModerate(false);
      });
  });

  /**
   * Whether this person may write here.
   *
   * Only announcement channels answer no: they are read-only to everyone but
   * moderators. The composer used to render regardless, so a member could type
   * a reply to an announcement and have it refused on send - the interface
   * offering something the server does not allow.
   */
  const canPostHere = () => {
    const channel = currentChannel();
    if (!channel?.capabilities) return true;
    if (!hasCapability(channel.capabilities, Capability.Announcement)) return true;
    return canModerate();
  };

  return (
    <Flexbox direction="vertical" class={styles.messagesArea}>
      <Show when={currentChannel()?.capabilities && hasCapability(currentChannel()!.capabilities, Capability.Streaming)}>
        <BroadcastChannel
          channelId={props.channelId!}
          canManageBroadcasts={canManageBroadcasts()}
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
            <Flexbox direction="vertical" data-testid="forum-thread-view" class={styles.forumThreadView}>
              {/* Thread header with back button */}
              <Flexbox align="center" class={styles.forumThreadHeader}>
                <button
                  data-testid="forum-back-button"
                  class={styles.forumBackButton}
                  onClick={() => modals.selectForumPost(null)}
                  aria-label="Back to forum posts"
                >
                  &larr; Back
                </button>
                <h3 class={styles.forumPostTitle}>{post().title}</h3>
              </Flexbox>
              <MessageList conversationId={post().conversationId} />
              <MessageCompose conversationId={post().conversationId} channelId={props.channelId} />
            </Flexbox>
          )}
        </Show>
      </Show>
      <Show when={
        (!currentChannel()?.capabilities || !hasCapability(currentChannel()!.capabilities, Capability.Forum)) &&
        (!currentChannel()?.capabilities || !hasCapability(currentChannel()!.capabilities, Capability.Streaming))
      }>
        {/* Voice channels show who is in the room above the chat. */}
        <Show when={currentChannel()?.capabilities && hasCapability(currentChannel()!.capabilities, Capability.Voice)}>
          <VoiceStage />
        </Show>
        <ScreenShareViewer />
        <MessageList conversationId={props.conversationId} />
        <TypingIndicator conversationId={props.conversationId} />
        <Show
          when={messageStore.editingMessageId}
          fallback={
            <Show
              when={canPostHere()}
              fallback={
                <p data-testid="channel-read-only-notice" class={styles.readOnlyNotice}>
                  Only moderators can post in this announcement channel.
                </p>
              }
            >
              <MessageCompose conversationId={props.conversationId} channelId={props.channelId} />
            </Show>
          }
        >
          {/* Edit bar replaces compose while editing */}
          <div class={styles.editBarWrapper}>
            <Flexbox direction="vertical" gap={0.25} class={styles.editBarInner}>
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
            </Flexbox>
          </div>
        </Show>
      </Show>
    </Flexbox>
  );
}
