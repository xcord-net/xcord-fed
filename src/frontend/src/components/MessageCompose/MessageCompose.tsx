import { Show } from 'solid-js';
import { useMembers } from '../../stores/member.store';
import { CreatePollForm } from '../PollDisplay';
import GifPicker from '../GifPicker';
import EmojiPicker from '../EmojiPicker';
import MemberList from '../MemberList';
import Dropdown from '../ui/Dropdown';
import ReplyPreview from './ReplyPreview';
import UploadProgress from './UploadProgress';
import AttachmentPreview from './AttachmentPreview';
import ComposeBar from './ComposeBar';
import { useMessageCompose } from './useMessageCompose';
import styles from './MessageCompose.module.css';

interface MessageComposeProps {
  conversationId: string;
  channelId?: string;
}

export default function MessageCompose(props: MessageComposeProps) {
  const memberStore = useMembers();

  let textareaRef: HTMLTextAreaElement | undefined;
  let fileInputRef: HTMLInputElement | undefined;
  let gifButtonRef: HTMLButtonElement | undefined;
  let emojiButtonRef: HTMLButtonElement | undefined;
  let membersButtonRef: HTMLButtonElement | undefined;

  const compose = useMessageCompose({
    conversationId: () => props.conversationId,
    channelId: () => props.channelId,
    textareaRef: () => textareaRef,
  });

  const handleAttachmentClick = () => {
    fileInputRef?.click();
  };

  return (
    <div class={styles.wrapper}>
      <input ref={fileInputRef} type="file" style={{ display: 'none' }} onChange={compose.handleFileSelect} />

      <Show when={compose.showPollForm()}>
        <div class={styles.pollWrap}>
          <CreatePollForm onSubmit={compose.handlePollSubmit} onCancel={() => compose.setShowPollForm(false)} />
        </div>
      </Show>

      <Show when={compose.replyToId()}>
        <ReplyPreview onCancel={compose.cancelReply} />
      </Show>

      <Show when={compose.uploading()}>
        <UploadProgress progress={compose.uploadProgress()} />
      </Show>

      <Show when={compose.uploadedAttachment()}>
        {(attachment) => (
          <AttachmentPreview
            fileName={attachment().fileName}
            fileSize={attachment().fileSize}
            onRemove={compose.removeAttachment}
          />
        )}
      </Show>

      <Show when={compose.sendError()}>
        <div role="alert" aria-label="Message blocked" class={styles.errorAlert}>{compose.sendError()}</div>
      </Show>

      <ComposeBar
        hasTopSection={compose.hasTopSection()}
        uploading={compose.uploading()}
        isSending={compose.isSending()}
        isSlowModeActive={compose.isSlowModeActive()}
        slowModeCountdown={compose.slowModeCountdown()}
        showPollForm={compose.showPollForm()}
        showGifPicker={compose.showGifPicker()}
        showEmojiPicker={compose.showEmojiPicker()}
        showMemberList={compose.showMemberList()}
        gifAvailable={compose.gifAvailable()}
        memberCount={memberStore.members.length}
        channelName={compose.currentChannel()?.name ?? 'channel'}
        content={compose.content()}
        textareaRef={(el) => { textareaRef = el; }}
        gifButtonRef={(el) => { gifButtonRef = el; }}
        emojiButtonRef={(el) => { emojiButtonRef = el; }}
        membersButtonRef={(el) => { membersButtonRef = el; }}
        onAttachmentClick={handleAttachmentClick}
        onTogglePollForm={() => compose.setShowPollForm(!compose.showPollForm())}
        onToggleGifPicker={() => compose.setShowGifPicker(!compose.showGifPicker())}
        onToggleEmojiPicker={() => compose.setShowEmojiPicker(!compose.showEmojiPicker())}
        onToggleMemberList={() => compose.setShowMemberList(!compose.showMemberList())}
        onInput={compose.handleInput}
        onKeyDown={compose.handleKeyDown}
      />

      <Dropdown open={compose.showGifPicker()} onClose={() => compose.setShowGifPicker(false)} trigger={gifButtonRef}>
        <GifPicker onSelect={compose.handleGifSelect} onClose={() => compose.setShowGifPicker(false)} />
      </Dropdown>

      <Dropdown open={compose.showEmojiPicker()} onClose={() => compose.setShowEmojiPicker(false)} trigger={emojiButtonRef}>
        <EmojiPicker onSelect={compose.handleEmojiSelect} onClose={() => compose.setShowEmojiPicker(false)} serverId={compose.currentChannel()?.serverId} isAdmin={compose.isServerAdmin()} />
      </Dropdown>

      <Dropdown open={compose.showMemberList()} onClose={() => compose.setShowMemberList(false)} trigger={membersButtonRef} anchor="top-end">
        <MemberList open={compose.showMemberList()} onClose={() => compose.setShowMemberList(false)} />
      </Dropdown>

    </div>
  );
}
