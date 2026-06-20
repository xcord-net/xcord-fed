import { Show } from 'solid-js';
import Flexbox from '../ui/Flexbox';
import styles from './ReplyPreview.module.css';

interface ReplyPreviewProps {
  onCancel: () => void;
  author?: string;
  content?: string;
}

export default function ReplyPreview(props: ReplyPreviewProps) {
  return (
    <Flexbox align="center" justify="between" class={styles.replyPreview}>
      <span class={styles.replyText}>
        <Show when={props.author} fallback="Replying to a message">
          Replying to <span class={styles.replyAuthor}>{props.author}</span>
          <Show when={props.content}>
            <span class={styles.replySnippet}>: {props.content}</span>
          </Show>
        </Show>
      </span>
      <button class={styles.closeBtn} aria-label="Cancel reply" onClick={props.onCancel}>x</button>
    </Flexbox>
  );
}
