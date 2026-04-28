import styles from './UploadProgress.module.css';

interface UploadProgressProps {
  progress: number;
}

export default function UploadProgress(props: UploadProgressProps) {
  return (
    <div class={styles.uploadProgress}>
      <div class={styles.uploadHeader}>
        <span class={styles.uploadLabel}>Uploading...</span>
        <span class={styles.uploadPercent}>{props.progress}%</span>
      </div>
      <div class={styles.uploadTrack}>
        <div class={styles.uploadFill} style={{ width: `${props.progress}%` }} />
      </div>
    </div>
  );
}
