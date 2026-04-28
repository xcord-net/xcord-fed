import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { createSignal } from 'solid-js';

// ---- Mocks ----
// Mock the heavy compose hook so we can drive the parent's render branches
// without touching message/channel/signalR/auth stores or the network.
type ComposeMock = ReturnType<typeof makeComposeMock>;
function makeComposeMock() {
  const [showPollForm, setShowPollForm] = createSignal(false);
  const [showGifPicker, setShowGifPicker] = createSignal(false);
  const [showEmojiPicker, setShowEmojiPicker] = createSignal(false);
  const [showMemberList, setShowMemberList] = createSignal(false);
  const [replyToId, setReplyToId] = createSignal<string | null>(null);
  const [uploading, setUploading] = createSignal(false);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploadedAttachment, setUploadedAttachment] = createSignal<
    { attachmentId: string; fileName: string; fileSize: number } | null
  >(null);
  const [sendError, setSendError] = createSignal<string | null>(null);
  const [content, setContent] = createSignal('');

  return {
    // signals (read)
    content,
    replyToId,
    setReplyToId,
    isSending: () => false,
    sendError,
    setSendError,
    showPollForm,
    showGifPicker,
    showEmojiPicker,
    showMemberList,
    gifAvailable: () => false,
    slowModeCountdown: () => 0,
    uploading,
    setUploading,
    uploadProgress,
    setUploadProgress,
    uploadedAttachment,
    setUploadedAttachment,
    setContent,
    // setters used by parent
    setShowPollForm,
    setShowGifPicker,
    setShowEmojiPicker,
    setShowMemberList,
    // derived
    currentChannel: () => ({ id: 'c-1', name: 'general', serverId: 's-1' }),
    isServerAdmin: () => false,
    isSlowModeActive: () => false,
    hasTopSection: () => !!(replyToId() || uploading() || uploadedAttachment()),
    // handlers
    handleSend: vi.fn(),
    handleKeyDown: vi.fn(),
    handleInput: vi.fn(),
    cancelReply: vi.fn(() => setReplyToId(null)),
    handleFileSelect: vi.fn(),
    removeAttachment: vi.fn(() => setUploadedAttachment(null)),
    handlePollSubmit: vi.fn(),
    handleEmojiSelect: vi.fn(),
    handleGifSelect: vi.fn(),
  };
}

let composeMock: ComposeMock;
vi.mock('./useMessageCompose', () => ({
  useMessageCompose: () => composeMock,
}));

// Stub the member store - parent reads members.length only.
vi.mock('../../stores/member.store', () => ({
  useMembers: () => ({ members: [] as unknown[] }),
}));

// Stub heavy children so the parent test focuses on its own composition logic.
vi.mock('../PollDisplay', () => ({
  CreatePollForm: (p: { onCancel: () => void }) => (
    <div data-testid="mock-poll-form">
      <button data-testid="mock-poll-cancel" onClick={p.onCancel}>cancel</button>
    </div>
  ),
}));
vi.mock('../GifPicker', () => ({
  default: () => <div data-testid="mock-gif-picker" />,
}));
vi.mock('../EmojiPicker', () => ({
  default: () => <div data-testid="mock-emoji-picker" />,
}));
vi.mock('../MemberList', () => ({
  default: () => <div data-testid="mock-member-list" />,
}));
vi.mock('../ui/Dropdown', () => ({
  default: (p: { open: boolean; children: import('solid-js').JSX.Element }) => (
    <div data-testid="mock-dropdown">{p.open ? p.children : null}</div>
  ),
}));

// Import after mocks are set up.
import MessageCompose from './MessageCompose';

describe('MessageCompose', () => {
  beforeEach(() => {
    composeMock = makeComposeMock();
  });

  it('renders the ComposeBar with the current channel name in the placeholder', () => {
    const { getByTestId } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    const textarea = getByTestId('compose-textarea') as HTMLTextAreaElement;
    expect(textarea).toBeInTheDocument();
    expect(textarea.placeholder).toBe('Message general');
  });

  it('renders ReplyPreview only when replyToId is set', () => {
    const { queryByText, getByText } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    expect(queryByText('Replying to a message')).toBeNull();
    composeMock.setReplyToId('msg-7');
    expect(getByText('Replying to a message')).toBeInTheDocument();
  });

  it('renders UploadProgress while uploading', () => {
    composeMock.setUploading(true);
    composeMock.setUploadProgress(33);
    const { getByText } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    expect(getByText('Uploading...')).toBeInTheDocument();
    expect(getByText('33%')).toBeInTheDocument();
  });

  it('renders AttachmentPreview when an attachment is uploaded', () => {
    composeMock.setUploadedAttachment({
      attachmentId: 'a-1',
      fileName: 'doc.pdf',
      fileSize: 2048,
    });
    const { getByTestId } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    expect(getByTestId('compose-attachment-filename')).toHaveTextContent('doc.pdf');
  });

  it('renders the send-error alert when sendError is set', () => {
    composeMock.setSendError('Message blocked by automod');
    const { getByRole } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    const alert = getByRole('alert');
    expect(alert).toHaveTextContent('Message blocked by automod');
  });

  it('triggers the file input click when attach button is pressed', () => {
    const { getByTestId, container } = render(() => (
      <MessageCompose conversationId="conv-1" channelId="c-1" />
    ));
    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    const clickSpy = vi.spyOn(fileInput, 'click');
    fireEvent.click(getByTestId('compose-attach-button'));
    expect(clickSpy).toHaveBeenCalled();
  });
});
