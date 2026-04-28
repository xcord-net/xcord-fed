import { PaperclipIcon } from './icons';
import { formatFileSize } from './formatFileSize';
import styles from './AttachmentPreview.module.css';

interface AttachmentPreviewProps {
  fileName: string;
  fileSize: number;
  onRemove: () => void;
}

export default function AttachmentPreview(props: AttachmentPreviewProps) {
  return (
    <div data-testid="compose-attachment-preview" class={styles.filePreview}>
      <PaperclipIcon class={styles.fileIcon} />
      <div class={styles.fileInfo}>
        <p data-testid="compose-attachment-filename" class={styles.fileName}>{props.fileName}</p>
        <p class={styles.fileSize}>{formatFileSize(props.fileSize)}</p>
      </div>
      <button data-testid="compose-attachment-remove" class={styles.removeBtn} onClick={props.onRemove} aria-label="Remove attachment">x</button>
    </div>
  );
}
