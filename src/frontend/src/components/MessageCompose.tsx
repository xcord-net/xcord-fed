import { createSignal, createEffect, onCleanup, Show } from 'solid-js';
import { useMessages } from '../stores/message.store';
import { useChannels } from '../stores/channel.store';
import { useSignalR } from '../stores/signalr.store';
import { useModals } from '../stores/modal.store';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import { CreatePollForm } from './PollDisplay';
import GifPicker from './GifPicker';
import Modal from './ui/Modal';

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

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function MessageCompose(props: MessageComposeProps) {
  const messageStore = useMessages();
  const channelStore = useChannels();
  const signalR = useSignalR();
  const modals = useModals();
  const [content, setContent] = createSignal('');
  const [replyToId, setReplyToId] = createSignal<string | null>(null);
  const [isSending, setIsSending] = createSignal(false);
  const [sendError, setSendError] = createSignal<string | null>(null);

  // Poll form visibility
  const [showPollForm, setShowPollForm] = createSignal(false);

  // GIF picker visibility
  const [showGifPicker, setShowGifPicker] = createSignal(false);

  // Schedule message modal
  const [showScheduleModal, setShowScheduleModal] = createSignal(false);
  const [scheduleDate, setScheduleDate] = createSignal('');
  const [scheduleTime, setScheduleTime] = createSignal('');
  const [scheduleError, setScheduleError] = createSignal('');
  const [isScheduling, setIsScheduling] = createSignal(false);

  // Slow mode state
  const [slowModeCountdown, setSlowModeCountdown] = createSignal(0);

  // Upload state
  const [uploading, setUploading] = createSignal(false);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploadedAttachment, setUploadedAttachment] = createSignal<UploadedAttachment | null>(null);

  let textareaRef: HTMLTextAreaElement | undefined;
  let fileInputRef: HTMLInputElement | undefined;
  let slowModeTimer: ReturnType<typeof setInterval> | undefined;
  // Throttle typing events - send at most once every 3 seconds.
  let lastTypingSentAt = 0;

  // Reset slow mode countdown when channel changes
  createEffect(() => {
    // Track the channelId - when it changes, reset the countdown
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
      // Start slow mode countdown after successful send
      startSlowModeCountdown();
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string; message?: string };
      const msg = e?.detail ?? e?.error ?? e?.message ?? 'Failed to send message';
      setSendError(msg);
      // Auto-clear error after 5 seconds
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

    // Notify other users that this user is typing. Throttled to once per 3s
    // so we don't flood the server with typing events on every keystroke.
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

    // Reset file input so the same file can be re-selected if needed
    input.value = '';

    setUploading(true);
    setUploadProgress(0);

    try {
      // Step 1: Request presigned upload URL
      const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSize: file.size,
      });

      // Step 2: Upload directly to S3/MinIO with XHR for progress tracking
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
        // Auth cookie sent automatically via withCredentials
        xhr.withCredentials = true;
        xhr.send(file);
      });

      // Step 3: Confirm upload complete
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

      // Reload messages to show the new poll message
      messageStore.clearMessages();
      await messageStore.loadMessages(props.conversationId);
    } catch (error) {
      console.error('Failed to create poll:', error);
    }
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
      const e = err as { error?: string; detail?: string; message?: string };
      const msg = e?.detail ?? e?.error ?? e?.message ?? 'Failed to send GIF';
      setSendError(msg);
      setTimeout(() => setSendError(null), 5_000);
      console.error('Failed to send GIF:', err);
    } finally {
      setIsSending(false);
    }
  };

  const openScheduleModal = () => {
    const text = content().trim();
    if (!text) {
      setSendError('Type a message before scheduling');
      setTimeout(() => setSendError(null), 3_000);
      return;
    }
    // Default to 1 hour from now
    const defaultTime = new Date(Date.now() + 60 * 60 * 1000);
    const year = defaultTime.getFullYear();
    const month = String(defaultTime.getMonth() + 1).padStart(2, '0');
    const day = String(defaultTime.getDate()).padStart(2, '0');
    const hours = String(defaultTime.getHours()).padStart(2, '0');
    const minutes = String(defaultTime.getMinutes()).padStart(2, '0');
    setScheduleDate(`${year}-${month}-${day}`);
    setScheduleTime(`${hours}:${minutes}`);
    setScheduleError('');
    setShowScheduleModal(true);
  };

  const handleScheduleSend = async () => {
    if (!props.channelId) {
      setScheduleError('Scheduled messages require a channel');
      return;
    }

    const text = content().trim();
    if (!text) {
      setScheduleError('Message content is required');
      return;
    }

    const dateVal = scheduleDate();
    const timeVal = scheduleTime();
    if (!dateVal || !timeVal) {
      setScheduleError('Please select a date and time');
      return;
    }

    // Build a DateTimeOffset from local date/time
    const localDate = new Date(`${dateVal}T${timeVal}`);
    if (isNaN(localDate.getTime())) {
      setScheduleError('Invalid date or time');
      return;
    }

    const now = new Date();
    if (localDate.getTime() - now.getTime() < 60_000) {
      setScheduleError('Scheduled time must be at least 1 minute in the future');
      return;
    }

    if (localDate.getTime() - now.getTime() > 30 * 24 * 60 * 60 * 1000) {
      setScheduleError('Scheduled time must be within 30 days');
      return;
    }

    setIsScheduling(true);
    setScheduleError('');
    try {
      await api.post(`/api/v1/channels/${props.channelId}/scheduled-messages`, {
        content: text,
        scheduledAt: localDate.toISOString(),
      });
      setShowScheduleModal(false);
      setContent('');
      if (textareaRef) {
        textareaRef.style.height = 'auto';
      }
    } catch (err: unknown) {
      setScheduleError(getErrorMessage(err, 'Failed to schedule message'));
    } finally {
      setIsScheduling(false);
    }
  };

  const hasTopSection = () => !!(replyToId() || uploading() || uploadedAttachment());

  return (
    <div class="px-4 pb-6">
      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        class="hidden"
        onChange={handleFileSelect}
      />

      {/* Poll creation form */}
      <Show when={showPollForm()}>
        <div class="mb-2">
          <CreatePollForm
            onSubmit={handlePollSubmit}
            onCancel={() => setShowPollForm(false)}
          />
        </div>
      </Show>

      {/* Reply preview */}
      <Show when={replyToId()}>
        <div class="flex items-center justify-between px-3 py-2 bg-xcord-bg-primary rounded-t-lg border-b border-xcord-bg-secondary">
          <span class="text-sm text-xcord-text-secondary">Replying to a message</span>
          <button
            class="text-xcord-text-muted hover:text-xcord-text-primary transition-colors"
            onClick={cancelReply}
          >
            ✕
          </button>
        </div>
      </Show>

      {/* Upload progress bar */}
      <Show when={uploading()}>
        <div class="px-3 py-2 bg-xcord-bg-primary border-b border-xcord-bg-secondary rounded-t-lg">
          <div class="flex items-center gap-2 mb-1">
            <span class="text-xs text-xcord-text-secondary">Uploading...</span>
            <span class="text-xs text-xcord-text-muted ml-auto">{uploadProgress()}%</span>
          </div>
          <div class="w-full h-1 bg-xcord-bg-secondary rounded-full overflow-hidden">
            <div
              class="h-full bg-xcord-accent transition-all duration-200"
              style={{ width: `${uploadProgress()}%` }}
            />
          </div>
        </div>
      </Show>

      {/* File preview */}
      <Show when={uploadedAttachment()}>
        {(attachment) => (
          <div class="flex items-center gap-2 px-3 py-2 bg-xcord-bg-primary border-b border-xcord-bg-secondary rounded-t-lg">
            <span class="text-lg" aria-hidden="true">📎</span>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-xcord-text-primary truncate">{attachment().fileName}</p>
              <p class="text-xs text-xcord-text-muted">{formatFileSize(attachment().fileSize)}</p>
            </div>
            <button
              class="text-xcord-text-muted hover:text-xcord-text-primary transition-colors flex-shrink-0"
              onClick={removeAttachment}
              aria-label="Remove attachment"
            >
              ✕
            </button>
          </div>
        )}
      </Show>

      {/* Send error (e.g. automod blocked) */}
      <Show when={sendError()}>
        <div
          role="alert"
          aria-label="Message blocked"
          class="px-3 py-2 mb-1 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-xs"
        >
          {sendError()}
        </div>
      </Show>

      {/* Input area */}
      <div class={`bg-xcord-bg-primary ${hasTopSection() ? 'rounded-b-lg' : 'rounded-lg'} px-4 py-3 flex items-end gap-2`}>
        {/* Attachment button */}
        <button
          class="flex-shrink-0 text-xcord-text-muted hover:text-xcord-text-primary transition-colors pb-0.5 disabled:opacity-40 disabled:cursor-not-allowed"
          onClick={handleAttachmentClick}
          disabled={uploading() || isSending()}
          aria-label="Attach file"
          title="Attach file"
        >
          📎
        </button>

        {/* Poll button */}
        <button
          class="flex-shrink-0 text-xcord-text-muted hover:text-xcord-text-primary transition-colors pb-0.5 disabled:opacity-40 disabled:cursor-not-allowed"
          onClick={() => setShowPollForm(!showPollForm())}
          disabled={isSending()}
          aria-label="Create poll"
          title="Create poll"
        >
          📊
        </button>

        {/* GIF button */}
        <div class="relative flex-shrink-0">
          <button
            class="text-xcord-text-muted hover:text-xcord-text-primary transition-colors pb-0.5 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold"
            onClick={() => setShowGifPicker(!showGifPicker())}
            disabled={isSending() || isSlowModeActive()}
            aria-label="Send GIF"
            title="Send GIF"
          >
            GIF
          </button>

          <Show when={showGifPicker()}>
            <div class="fixed inset-0 z-40" aria-hidden="true" onClick={() => setShowGifPicker(false)} />
            <div class="absolute bottom-full left-0 mb-2 z-50">
              <GifPicker
                onSelect={handleGifSelect}
                onClose={() => setShowGifPicker(false)}
              />
            </div>
          </Show>
        </div>

        {/* Schedule message button */}
        <Show when={props.channelId}>
          <button
            class="flex-shrink-0 text-xcord-text-muted hover:text-xcord-text-primary transition-colors pb-0.5 disabled:opacity-40 disabled:cursor-not-allowed"
            onClick={openScheduleModal}
            disabled={isSending() || isSlowModeActive()}
            aria-label="Schedule message"
            title="Schedule message"
          >
            &#128336;
          </button>
        </Show>

        <textarea
          id="message-compose-textarea"
          ref={textareaRef}
          class="flex-1 bg-transparent text-xcord-text-primary placeholder-xcord-text-muted resize-none outline-none"
          placeholder="Message #channel-name"
          value={content()}
          onInput={handleInput}
          onKeyDown={handleKeyDown}
          rows={1}
          disabled={isSending() || isSlowModeActive()}
        />

        {/* Slow mode countdown indicator */}
        <Show when={isSlowModeActive()}>
          <span
            id="message-compose-slowmode"
            class="flex-shrink-0 text-xs text-xcord-text-muted font-medium whitespace-nowrap"
            aria-live="polite"
            aria-label={`Slow mode active. Wait ${slowModeCountdown()} seconds before sending again.`}
          >
            Slowmode: {slowModeCountdown()}s
          </span>
        </Show>
      </div>

      {/* View scheduled messages link */}
      <Show when={props.channelId}>
        <button
          class="mt-1 text-xs text-xcord-text-muted hover:text-xcord-brand transition-colors"
          onClick={() => modals.toggleScheduledMessages()}
          aria-label="View scheduled messages"
        >
          {modals.showScheduledMessages ? 'Hide' : 'View'} scheduled messages
        </button>
      </Show>

      {/* Schedule message modal */}
      <Modal
        open={showScheduleModal()}
        onClose={() => setShowScheduleModal(false)}
        title="Schedule Message"
        size="sm"
      >
        <div class="p-6 space-y-4">
          {/* Message preview */}
          <div>
            <label class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
              Message
            </label>
            <div class="bg-xcord-bg-primary rounded px-3 py-2 text-sm text-xcord-text-primary max-h-24 overflow-y-auto break-words">
              {content()}
            </div>
          </div>

          {/* Date picker */}
          <div>
            <label for="schedule-date" class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
              Date
            </label>
            <input
              id="schedule-date"
              type="date"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand focus:outline-none"
              value={scheduleDate()}
              onInput={(e) => setScheduleDate(e.currentTarget.value)}
              min={new Date().toISOString().split('T')[0]}
            />
          </div>

          {/* Time picker */}
          <div>
            <label for="schedule-time" class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
              Time
            </label>
            <input
              id="schedule-time"
              type="time"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand focus:outline-none"
              value={scheduleTime()}
              onInput={(e) => setScheduleTime(e.currentTarget.value)}
            />
          </div>

          {/* Timezone note */}
          <p class="text-xcord-text-muted text-xs">
            Times are in your local timezone ({Intl.DateTimeFormat().resolvedOptions().timeZone}).
          </p>

          {/* Error */}
          <Show when={scheduleError()}>
            <div role="alert" class="px-3 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-xs">
              {scheduleError()}
            </div>
          </Show>

          {/* Actions */}
          <div class="flex justify-end gap-3">
            <button
              type="button"
              onClick={() => setShowScheduleModal(false)}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleScheduleSend}
              disabled={isScheduling()}
              class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              {isScheduling() ? 'Scheduling...' : 'Schedule'}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
