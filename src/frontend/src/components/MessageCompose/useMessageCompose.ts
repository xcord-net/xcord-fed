import { createSignal, createEffect, onMount, createMemo } from 'solid-js';
import { useMessages } from '../../stores/message.store';
import { useChannels } from '../../stores/channel.store';
import { useSignalR } from '../../stores/signalr.store';
import { useAuth } from '../../stores/auth.store';
import { useServers } from '../../stores/server.store';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import { useFileUpload } from './useFileUpload';
import { useSlowMode } from './useSlowMode';

interface UseMessageComposeArgs {
  conversationId: () => string;
  channelId: () => string | undefined;
  textareaRef: () => HTMLTextAreaElement | undefined;
}

export function useMessageCompose(args: UseMessageComposeArgs) {
  const messageStore = useMessages();
  const channelStore = useChannels();
  const signalR = useSignalR();
  const authStore = useAuth();
  const serverStore = useServers();

  const [content, setContent] = createSignal('');
  // Reply target is shared via the message store so the message action bar (a
  // sibling of the composer) can start a reply. replyToId stays a derived read.
  const replyTarget = () => messageStore.replyTarget;
  const replyToId = () => messageStore.replyTarget?.id ?? null;
  const [isSending, setIsSending] = createSignal(false);
  const [sendError, setSendError] = createSignal<string | null>(null);

  const [showPollForm, setShowPollForm] = createSignal(false);
  const [showGifPicker, setShowGifPicker] = createSignal(false);
  const [showEmojiPicker, setShowEmojiPicker] = createSignal(false);
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

  const upload = useFileUpload();
  const slow = useSlowMode({ channelId: args.channelId });

  let lastTypingSentAt = 0;

  // Auto-focus textarea when channel changes
  createEffect(() => {
    const _convId = args.conversationId();
    void _convId;
    requestAnimationFrame(() => args.textareaRef()?.focus());
  });

  const currentChannel = () => {
    const id = args.channelId();
    return id ? channelStore.channels.find((c) => c.id === id) : undefined;
  };

  const isServerAdmin = createMemo(() => {
    const channel = currentChannel();
    if (!channel?.serverId) return false;
    const server = serverStore.servers.find((s) => s.id === channel.serverId);
    return !!server && !!authStore.user?.id && server.ownerId === authStore.user.id;
  });

  const handleSend = async () => {
    const text = content().trim();
    const attachment = upload.uploadedAttachment();
    if ((!text && !attachment) || isSending() || slow.isSlowModeActive()) return;

    setIsSending(true);
    setSendError(null);
    try {
      const attachmentIds = attachment ? [attachment.attachmentId] : undefined;

      await messageStore.sendMessage(
        args.conversationId(),
        text,
        replyToId() || undefined,
        attachmentIds,
      );
      setContent('');
      // Sending empties the box without an input event, so withdraw the notice
      // here too - otherwise you go on "typing" beside the message you just sent.
      lastTypingSentAt = 0;
      signalR.sendStoppedTyping(args.conversationId()).catch(() => { /* non-fatal */ });
      messageStore.cancelReply();
      upload.setUploadedAttachment(null);
      const ta = args.textareaRef();
      if (ta) {
        ta.style.height = 'auto';
      }
      slow.startSlowModeCountdown();
    } catch (err: unknown) {
      setSendError(getErrorMessage(err, 'Failed to send message'));
      setTimeout(() => setSendError(null), 5_000);
      console.error('Failed to send message:', err);
    } finally {
      setIsSending(false);
    }
  };

  const handleKeyDown = async (e: KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      await handleSend();
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
        signalR.sendTyping(args.conversationId()).catch(() => { /* non-fatal */ });
      }
    } else if (lastTypingSentAt > 0) {
      // Emptying the box is a decision, not a pause. Without telling anyone,
      // someone who typed a word and deleted it went on "typing" on every other
      // screen until the eight-second notice expired. Only sent when this
      // composer actually claimed to be typing.
      lastTypingSentAt = 0;
      signalR.sendStoppedTyping(args.conversationId()).catch(() => { /* non-fatal */ });
    }
  };

  const cancelReply = () => {
    messageStore.cancelReply();
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

      await api.post(`/api/v1/conversations/${args.conversationId()}/polls`, {
        question: pollData.question,
        options: pollData.options.map((text) => ({ text })),
        allowMultipleAnswers: pollData.allowMultiSelect,
        expiresAt,
      });

      messageStore.clearMessages();
      await messageStore.loadMessages(args.conversationId());
    } catch (error) {
      console.error('Failed to create poll:', error);
    }
  };

  const handleEmojiSelect = (emoji: string) => {
    setShowEmojiPicker(false);
    setContent((prev) => prev + emoji);
    args.textareaRef()?.focus();
  };

  const handleGifSelect = async (gifUrl: string) => {
    setShowGifPicker(false);
    if (isSending() || slow.isSlowModeActive()) return;

    setIsSending(true);
    setSendError(null);
    try {
      await messageStore.sendMessage(args.conversationId(), gifUrl);
      slow.startSlowModeCountdown();
    } catch (err: unknown) {
      setSendError(getErrorMessage(err, 'Failed to send GIF'));
      setTimeout(() => setSendError(null), 5_000);
      console.error('Failed to send GIF:', err);
    } finally {
      setIsSending(false);
    }
  };

  const hasTopSection = () => !!(replyToId() || upload.uploading() || upload.uploadedAttachment());

  return {
    // signals (read)
    content,
    replyToId,
    replyTarget,
    isSending,
    sendError,
    showPollForm,
    showGifPicker,
    showEmojiPicker,
    showMemberList,
    gifAvailable,
    slowModeCountdown: slow.slowModeCountdown,
    uploading: upload.uploading,
    uploadProgress: upload.uploadProgress,
    uploadedAttachment: upload.uploadedAttachment,
    // setters
    setShowPollForm,
    setShowGifPicker,
    setShowEmojiPicker,
    setShowMemberList,
    // derived
    currentChannel,
    isServerAdmin,
    isSlowModeActive: slow.isSlowModeActive,
    hasTopSection,
    // handlers
    handleSend,
    handleKeyDown,
    handleInput,
    cancelReply,
    handleFileSelect: upload.handleFileSelect,
    handlePaste: upload.handlePaste,
    removeAttachment: upload.removeAttachment,
    handlePollSubmit,
    handleEmojiSelect,
    handleGifSelect,
  };
}
