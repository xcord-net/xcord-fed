import Flexbox from '../ui/Flexbox';
import styles from './ReplyPreview.module.css';

interface ReplyPreviewProps {
  onCancel: () => void;
}

export default function ReplyPreview(props: ReplyPreviewProps) {
  return (
    <Flexbox align="center" justify="between" class={styles.replyPreview}>
      <span class={styles.replyText}>Replying to a message</span>
      <button class={styles.closeBtn} onClick={props.onCancel}>x</button>
    </Flexbox>
  );
}
