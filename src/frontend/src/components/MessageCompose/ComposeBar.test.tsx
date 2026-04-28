import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ComposeBar from './ComposeBar';

type ComposeBarProps = Parameters<typeof ComposeBar>[0];

function makeProps(overrides: Partial<ComposeBarProps> = {}): ComposeBarProps {
  return {
    hasTopSection: false,
    uploading: false,
    isSending: false,
    isSlowModeActive: false,
    slowModeCountdown: 0,
    showPollForm: false,
    showGifPicker: false,
    showEmojiPicker: false,
    showMemberList: false,
    gifAvailable: false,
    memberCount: 5,
    channelName: 'general',
    content: '',
    textareaRef: vi.fn(),
    gifButtonRef: vi.fn(),
    emojiButtonRef: vi.fn(),
    membersButtonRef: vi.fn(),
    onAttachmentClick: vi.fn(),
    onTogglePollForm: vi.fn(),
    onToggleGifPicker: vi.fn(),
    onToggleEmojiPicker: vi.fn(),
    onToggleMemberList: vi.fn(),
    onInput: vi.fn(),
    onKeyDown: vi.fn(),
    ...overrides,
  };
}

describe('ComposeBar', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => <ComposeBar {...makeProps()} />);
    expect(getByTestId('compose-textarea')).toBeInTheDocument();
  });

  it('renders the textarea with the channel-name placeholder and member count', () => {
    const { getByTestId } = render(() => (
      <ComposeBar {...makeProps({ channelName: 'random', memberCount: 12 })} />
    ));
    const textarea = getByTestId('compose-textarea') as HTMLTextAreaElement;
    expect(textarea.placeholder).toBe('Message random');
    expect(getByTestId('compose-members-button')).toHaveTextContent('12');
  });

  it('hides GIF button when unavailable and shows it when available', () => {
    const off = render(() => (
      <ComposeBar {...makeProps({ gifAvailable: false })} />
    ));
    expect(off.queryByTestId('compose-gif-button')).toBeNull();
    off.unmount();

    const on = render(() => (
      <ComposeBar {...makeProps({ gifAvailable: true })} />
    ));
    expect(on.getByTestId('compose-gif-button')).toBeInTheDocument();
  });

  it('invokes onAttachmentClick when attach button is pressed', () => {
    const onAttachmentClick = vi.fn();
    const { getByTestId } = render(() => (
      <ComposeBar {...makeProps({ onAttachmentClick })} />
    ));
    fireEvent.click(getByTestId('compose-attach-button'));
    expect(onAttachmentClick).toHaveBeenCalledTimes(1);
  });

  it('disables textarea and shows the slow-mode label when slow mode is active', () => {
    const { getByTestId, container } = render(() => (
      <ComposeBar {...makeProps({ isSlowModeActive: true, slowModeCountdown: 7 })} />
    ));
    const textarea = getByTestId('compose-textarea') as HTMLTextAreaElement;
    expect(textarea.disabled).toBe(true);
    expect(container.textContent).toContain('Slowmode: 7s');
  });

  it('disables attach button while uploading', () => {
    const { getByTestId } = render(() => (
      <ComposeBar {...makeProps({ uploading: true })} />
    ));
    const attach = getByTestId('compose-attach-button') as HTMLButtonElement;
    expect(attach.disabled).toBe(true);
  });
});
