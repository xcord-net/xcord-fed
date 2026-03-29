import { createSignal, createEffect, onCleanup, onMount, Show } from 'solid-js';
import { useMessages } from '../stores/message.store';
import { useChannels } from '../stores/channel.store';
import { useSignalR } from '../stores/signalr.store';
import { useMembers } from '../stores/member.store';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import { CreatePollForm } from './PollDisplay';
import GifPicker from './GifPicker';
import EmojiPicker from './EmojiPicker';
import MemberList from './MemberList';
import Dropdown from './ui/Dropdown';
import styles from './MessageCompose.module.css';

interface MessageComposeProps {
  conversationId: string;
  channelId?: string;
}

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface UploadedAttachment {
  attachmentId: string;
  fileName: string;
  fileSize: number;
}

// --- SVG Icons ---

function PaperclipIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
    </svg>
  );
}

function BarChartIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <line x1="12" y1="20" x2="12" y2="10" />
      <line x1="18" y1="20" x2="18" y2="4" />
      <line x1="6" y1="20" x2="6" y2="16" />
    </svg>
  );
}

function SmileIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <circle cx="12" cy="12" r="10" />
      <path d="M8 14s1.5 2 4 2 4-2 4-2" />
      <line x1="9" y1="9" x2="9.01" y2="9" />
      <line x1="15" y1="9" x2="15.01" y2="9" />
    </svg>
  );
}

function ClockIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  );
}

function UsersIcon(props: { class?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={props.class ?? 'w-4 h-4'}>
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  );
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function MessageCompose(props: MessageComposeProps) {
  const messageStore = useMessages();
  const channelStore = useChannels();
  const signalR = useSignalR();
  const memberStore = useMembers();
  const [content, setContent] = createSignal('');
  const [replyToId, setReplyToId] = createSignal<string | null>(null);
  const [isSending, setIsSending] = createSignal(false);
  const [sendError, setSendError] = createSignal<string | null>(null);

  // Poll form visibility
  const [showPollForm, setShowPollForm] = createSignal(false);

  // GIF picker visibility
  const [showGifPicker, setShowGifPicker] = createSignal(false);

  // Emoji picker visibility
  const [showEmojiPicker, setShowEmojiPicker] = createSignal(false);

  // Member list popover
  const [showMemberList, setShowMemberList] = createSignal(false);

  // GIF availability - probe once on mount
  const [gifAvailable, setGifAvailable] = createSignal(false);
  onMount(async () => {
    try {
      const result = await api.get<{ gifs: unknown[] }>('/api/v1/gifs/trending?limit=1');
      setGifAvailable(result.gifs.length > 0);
    } catch {
      setGifAvailable(false);
    }
  });

  // Slow mode state
  const [slowModeCountdown, setSlowModeCountdown] = createSignal(0);

  // Upload state
  const [uploading, setUploading] = createSignal(false);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploadedAttachment, setUploadedAttachment] = createSignal<UploadedAttachment | null>(null);

  let textareaRef: HTMLTextAreaElement | undefined;
  let fileInputRef: HTMLInputElement | undefined;
  let gifButtonRef: HTMLButtonElement | undefined;
  let emojiButtonRef: HTMLButtonElement | undefined;
  let membersButtonRef: HTMLButtonElement | undefined;
  let slowModeTimer: ReturnType<typeof setInterval> | undefined;
  let lastTypingSentAt = 0;

  // Auto-focus textarea when channel changes
  createEffect(() => {
    const _convId = props.conversationId;
    void _convId;
    requestAnimationFrame(() => textareaRef?.focus());
  });

  // Reset slow mode countdown when channel changes
  createEffect(() => {
    const _channelId = props.channelId;
    void _channelId;
    setSlowModeCountdown(0);
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
      slowModeTimer = undefined;
    }
  });

  onCleanup(() => {
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
    }
  });

  const currentChannel = () =>
    props.channelId ? channelStore.channels.find((c) => c.id === props.channelId) : undefined;

  const slowModeInterval = () => {
    if (!props.channelId) return 0;
    const channel = channelStore.channels.find((c) => c.id === props.channelId);
    return channel?.slowModeSeconds ?? 0;
  };

  const isSlowModeActive = () => slowModeCountdown() > 0;

  const startSlowModeCountdown = () => {
    const interval = slowModeInterval();
    if (interval <= 0) return;
    setSlowModeCountdown(interval);
    if (slowModeTimer !== undefined) {
      clearInterval(slowModeTimer);
    }
    slowModeTimer = setInterval(() => {
      setSlowModeCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(slowModeTimer);
          slowModeTimer = undefined;
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  };

  const handleKeyDown = async (e: KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      await handleSend();
    }
  };

  const handleSend = async () => {
    const text = content().trim();
    const attachment = uploadedAttachment();
    if ((!text && !attachment) || isSending() || isSlowModeActive()) return;

    setIsSending(true);
    setSendError(null);
    try {
      const attachmentIds = attachment ? [attachment.attachmentId] : undefined;

      await messageStore.sendMessage(
        props.conversationId,
        text,
        replyToId() || undefined,
        attachmentIds,
      );
      setContent('');
      setReplyToId(null);
      setUploadedAttachment(null);
      if (textareaRef) {
        textareaRef.style.height = 'auto';
      }
      startSlowModeCountdown();
    } catch (err: unknown) {
      setSendError(getErrorMessage(err, 'Failed to send message'));
      setTimeout(() => setSendError(null), 5_000);
      console.error('Failed to send message:', err);
    } finally {
      setIsSending(false);
    }
  };

  const handleInput = (e: Event) => {
    const target = e.target as HTMLTextAreaElement;
    setContent(target.value);

    // Auto-resize textarea
    target.style.height = 'auto';
    target.style.height = `${Math.min(target.scrollHeight, 200)}px`;

    if (target.value.length > 0) {
      const now = Date.now();
      if (now - lastTypingSentAt > 3_000) {
        lastTypingSentAt = now;
        signalR.sendTyping(props.conversationId).catch(() => { /* non-fatal */ });
      }
    }
  };

  const cancelReply = () => {
    setReplyToId(null);
  };

  const handleAttachmentClick = () => {
    fileInputRef?.click();
  };

  const handleFileSelect = async (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    input.value = '';

    setUploading(true);
    setUploadProgress(0);

    try {
      const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSize: file.size,
      });

      await new Promise<void>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.upload.addEventListener('progress', (ev) => {
          if (ev.lengthComputable) {
            setUploadProgress(Math.round((ev.loaded / ev.total) * 100));
          }
        });
        xhr.addEventListener('load', () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            resolve();
          } else {
            reject(new Error(`Upload failed with status ${xhr.status}`));
          }
        });
        xhr.addEventListener('error', () => reject(new Error('Upload network error')));
        xhr.open('PUT', uploadUrl);
        xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');
        xhr.withCredentials = true;
        xhr.send(file);
      });

      await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

      setUploadedAttachment({ attachmentId, fileName: file.name, fileSize: file.size });
      setUploadProgress(100);
    } catch (error) {
      console.error('Failed to upload file:', error);
      setUploadedAttachment(null);
    } finally {
      setUploading(false);
    }
  };

  const removeAttachment = () => {
    setUploadedAttachment(null);
    setUploadProgress(0);
  };

  const handlePollSubmit = async (pollData: {
    question: string;
    options: string[];
    allowMultiSelect: boolean;
    durationHours?: number;
  }) => {
    setShowPollForm(false);
    try {
      const expiresAt = pollData.durationHours
        ? new Date(Date.now() + pollData.durationHours * 60 * 60 * 1000).toISOString()
        : undefined;

      await api.post(`/api/v1/conversations/${props.conversationId}/polls`, {
        question: pollData.question,
        options: pollData.options.map((text) => ({ text })),
        allowMultipleAnswers: pollData.allowMultiSelect,
        expiresAt,
      });

      messageStore.clearMessages();
      await messageStore.loadMessages(props.conversationId);
    } catch (error) {
      console.error('Failed to create poll:', error);
    }
  };

  const handleEmojiSelect = (emoji: string) => {
    setShowEmojiPicker(false);
    setContent((prev) => prev + emoji);
    textareaRef?.focus();
  };

  const handleGifSelect = async (gifUrl: string) => {
    setShowGifPicker(false);
    if (isSending() || isSlowModeActive()) return;

    setIsSending(true);
    setSendError(null);
    try {
      await messageStore.sendMessage(props.conversationId, gifUrl);
      startSlowModeCountdown();
    } catch (err: unknown) {
      setSendError(getErrorMessage(err, 'Failed to send GIF'));
      setTimeout(() => setSendError(null), 5_000);
      console.error('Failed to send GIF:', err);
    } finally {
      setIsSending(false);
    }
  };

  const hasTopSection = () => !!(replyToId() || uploading() || uploadedAttachment());

  return (
    <div class={styles.wrapper}>
      <input ref={fileInputRef} type="file" style={{ display: 'none' }} onChange={handleFileSelect} />

      <Show when={showPollForm()}>
        <div class={styles.pollWrap}>
          <CreatePollForm onSubmit={handlePollSubmit} onCancel={() => setShowPollForm(false)} />
        </div>
      </Show>

      <Show when={replyToId()}>
        <div class={styles.replyPreview}>
          <span class={styles.replyText}>Replying to a message</span>
          <button class={styles.closeBtn} onClick={cancelReply}>x</button>
        </div>
      </Show>

      <Show when={uploading()}>
        <div class={styles.uploadProgress}>
          <div class={styles.uploadHeader}>
            <span class={styles.uploadLabel}>Uploading...</span>
            <span class={styles.uploadPercent}>{uploadProgress()}%</span>
          </div>
          <div class={styles.uploadTrack}>
            <div class={styles.uploadFill} style={{ width: `${uploadProgress()}%` }} />
          </div>
        </div>
      </Show>

      <Show when={uploadedAttachment()}>
        {(attachment) => (
          <div data-testid="compose-attachment-preview" class={styles.filePreview}>
            <PaperclipIcon class={styles.fileIcon} />
            <div class={styles.fileInfo}>
              <p data-testid="compose-attachment-filename" class={styles.fileName}>{attachment().fileName}</p>
              <p class={styles.fileSize}>{formatFileSize(attachment().fileSize)}</p>
            </div>
            <button data-testid="compose-attachment-remove" class={styles.removeBtn} onClick={removeAttachment} aria-label="Remove attachment">x</button>
          </div>
        )}
      </Show>

      <Show when={sendError()}>
        <div role="alert" aria-label="Message blocked" class={styles.errorAlert}>{sendError()}</div>
      </Show>

      <div class={`${styles.bar} ${hasTopSection() ? styles.barContinued : ''}`}>
        <button data-testid="compose-attach-button" class={styles.btn} onClick={handleAttachmentClick} disabled={uploading() || isSending()} aria-label="Attach file" title="Attach file">
          <PaperclipIcon />
        </button>

        <button data-testid="compose-poll-button" class={styles.btn} onClick={() => setShowPollForm(!showPollForm())} disabled={isSending()} aria-label="Create poll" title="Create poll">
          <BarChartIcon />
        </button>

        <Show when={gifAvailable()}>
          <button data-testid="compose-gif-button" class={styles.btnText} ref={(el) => { gifButtonRef = el; }} onClick={() => setShowGifPicker(!showGifPicker())} disabled={isSending() || isSlowModeActive()} aria-label="Send GIF" title="Send GIF">GIF</button>
        </Show>

        <button data-testid="compose-emoji-button" class={styles.btn} ref={(el) => { emojiButtonRef = el; }} onClick={() => setShowEmojiPicker(!showEmojiPicker())} disabled={isSending()} aria-label="Insert emoji" title="Insert emoji">
          <SmileIcon />
        </button>

        <textarea
          id="message-compose-textarea"
          data-testid="compose-textarea"
          ref={textareaRef}
          class={styles.input}
          placeholder={`Message ${currentChannel()?.name ?? 'channel'}`}
          value={content()}
          onInput={handleInput}
          onKeyDown={handleKeyDown}
          rows={1}
          disabled={isSending() || isSlowModeActive()}
        />

        <Show when={isSlowModeActive()}>
          <span id="message-compose-slowmode" class={styles.slowMode} aria-live="polite" aria-label={`Slow mode active. Wait ${slowModeCountdown()} seconds before sending again.`}>
            Slowmode: {slowModeCountdown()}s
          </span>
        </Show>

        <button data-testid="compose-members-button" class={styles.memberTrigger} classList={{ [styles.btnActive]: showMemberList() }} ref={(el) => { membersButtonRef = el; }} onClick={() => setShowMemberList(!showMemberList())} aria-label="Members" title="Members">
          <UsersIcon />
          <span>{memberStore.members.length}</span>
        </button>
      </div>

      <Dropdown open={showGifPicker()} onClose={() => setShowGifPicker(false)} trigger={gifButtonRef}>
        <GifPicker onSelect={handleGifSelect} onClose={() => setShowGifPicker(false)} />
      </Dropdown>

      <Dropdown open={showEmojiPicker()} onClose={() => setShowEmojiPicker(false)} trigger={emojiButtonRef}>
        <EmojiPicker onSelect={handleEmojiSelect} onClose={() => setShowEmojiPicker(false)} />
      </Dropdown>

      <Dropdown open={showMemberList()} onClose={() => setShowMemberList(false)} trigger={membersButtonRef} anchor="top-end">
        <MemberList open={showMemberList()} onClose={() => setShowMemberList(false)} />
      </Dropdown>

    </div>
  );
}
