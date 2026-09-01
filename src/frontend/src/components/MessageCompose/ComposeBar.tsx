import { Show } from 'solid-js';
import { tooltip } from '../../directives/tooltip';
import { PaperclipIcon, BarChartIcon, SmileIcon, UsersIcon, SendIcon } from './icons';
import Flexbox from '../ui/Flexbox';
import styles from './ComposeBar.module.css';

// Ensure the directive is not tree-shaken
void tooltip;

interface ComposeBarProps {
  hasTopSection: boolean;
  uploading: boolean;
  isSending: boolean;
  isSlowModeActive: boolean;
  slowModeCountdown: number;
  showPollForm: boolean;
  showGifPicker: boolean;
  showEmojiPicker: boolean;
  showMemberList: boolean;
  gifAvailable: boolean;
  memberCount: number;
  channelName: string;
  content: string;
  textareaRef: (el: HTMLTextAreaElement) => void;
  gifButtonRef: (el: HTMLButtonElement) => void;
  emojiButtonRef: (el: HTMLButtonElement) => void;
  membersButtonRef: (el: HTMLButtonElement) => void;
  onAttachmentClick: () => void;
  onTogglePollForm: () => void;
  onToggleGifPicker: () => void;
  onToggleEmojiPicker: () => void;
  onToggleMemberList: () => void;
  onInput: (e: Event) => void;
  onKeyDown: (e: KeyboardEvent) => void;
  onPaste: (e: ClipboardEvent) => void;
  canSend: boolean;
  onSend: () => void;
}

export default function ComposeBar(props: ComposeBarProps) {
  return (
    <Flexbox align="center" gap={0.375} class={`${styles.bar} ${props.hasTopSection ? styles.barContinued : ''}`}>
      <button data-testid="compose-attach-button" class={styles.btn} use:tooltip="Attach file" onClick={props.onAttachmentClick} disabled={props.uploading || props.isSending} aria-label="Attach file" title="Attach file">
        <PaperclipIcon />
      </button>

      <button data-testid="compose-poll-button" class={styles.btn} use:tooltip="Create poll" onClick={props.onTogglePollForm} disabled={props.isSending} aria-label="Create poll" title="Create poll">
        <BarChartIcon />
      </button>

      <Show when={props.gifAvailable}>
        <button data-testid="compose-gif-button" class={styles.btnText} use:tooltip="Send GIF" ref={props.gifButtonRef} onClick={props.onToggleGifPicker} disabled={props.isSending || props.isSlowModeActive} aria-label="Send GIF" title="Send GIF">GIF</button>
      </Show>

      <button data-testid="compose-emoji-button" class={styles.btn} use:tooltip="Insert emoji" ref={props.emojiButtonRef} onClick={props.onToggleEmojiPicker} disabled={props.isSending} aria-label="Insert emoji" title="Insert emoji">
        <SmileIcon />
      </button>

      <textarea
        id="message-compose-textarea"
        data-testid="compose-textarea"
        ref={props.textareaRef}
        class={styles.input}
        placeholder={`Message ${props.channelName}`}
        value={props.content}
        onInput={props.onInput}
        onKeyDown={props.onKeyDown}
        onPaste={props.onPaste}
        rows={1}
        disabled={props.isSending || props.isSlowModeActive}
      />

      <Show when={props.isSlowModeActive}>
        <span id="message-compose-slowmode" class={styles.slowMode} aria-live="polite" aria-label={`Slow mode active. Wait ${props.slowModeCountdown} seconds before sending again.`}>
          Slowmode: {props.slowModeCountdown}s
        </span>
      </Show>

      <button
        data-testid="compose-send-button"
        class={styles.sendBtn}
        use:tooltip="Send message"
        onClick={props.onSend}
        disabled={!props.canSend}
        aria-label="Send message"
        title="Send message"
      >
        <SendIcon />
      </button>

      <button data-testid="compose-members-button" class={styles.memberTrigger} classList={{ [styles.btnActive]: props.showMemberList }} ref={props.membersButtonRef} onClick={props.onToggleMemberList} aria-label="Members" title="Members">
        <UsersIcon />
        <span>{props.memberCount}</span>
      </button>
    </Flexbox>
  );
}
