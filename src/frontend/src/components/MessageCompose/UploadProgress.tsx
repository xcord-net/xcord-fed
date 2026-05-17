import Flexbox from '../ui/Flexbox';
import styles from './UploadProgress.module.css';

interface UploadProgressProps {
  progress: number;
}

export default function UploadProgress(props: UploadProgressProps) {
  return (
    <div class={styles.uploadProgress}>
      <Flexbox align="center" gap={0.5} class={styles.uploadHeader}>
        <span class={styles.uploadLabel}>Uploading...</span>
        <span class={styles.uploadPercent}>{props.progress}%</span>
      </Flexbox>
      <div class={styles.uploadTrack}>
        <div class={styles.uploadFill} style={{ width: `${props.progress}%` }} />
      </div>
    </div>
  );
}
