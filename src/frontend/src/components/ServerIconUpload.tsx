import { createSignal, Show } from 'solid-js';
import { api } from '../api/client';
import type { Server } from '../types/server';
import styles from './ServerIconUpload.module.css';

const MAX_FILE_SIZE = 8 * 1024 * 1024; // 8 MB

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface ServerIconUploadProps {
  server: Server;
  onUpdated?: (server: Server) => void;
}

type UploadTarget = 'icon' | 'banner';

// ---- Pure helpers ----

export function validateServerImageFile(file: { type: string; size: number }): string | null {
  if (!file.type.startsWith('image/')) return 'Only image files are allowed.';
  if (file.size > MAX_FILE_SIZE) return 'File size must be 8 MB or less.';
  return null;
}

// ---- Component ----

export default function ServerIconUpload(props: ServerIconUploadProps) {
  const [iconPreview, setIconPreview] = createSignal<string | null>(null);
  const [bannerPreview, setBannerPreview] = createSignal<string | null>(null);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploading, setUploading] = createSignal(false);
  const [uploadTarget, setUploadTarget] = createSignal<UploadTarget | null>(null);
  const [error, setError] = createSignal<string | null>(null);
  const [success, setSuccess] = createSignal<string | null>(null);

  let iconInputRef: HTMLInputElement | undefined;
  let bannerInputRef: HTMLInputElement | undefined;

  const handleIconClick = () => {
    setError(null);
    iconInputRef?.click();
  };

  const handleBannerClick = () => {
    setError(null);
    bannerInputRef?.click();
  };

  const validateFile = (file: File): string | null => {
    if (!file.type.startsWith('image/')) {
      return 'Only image files are allowed.';
    }
    if (file.size > MAX_FILE_SIZE) {
      return 'File size must be 8 MB or less.';
    }
    return null;
  };

  const readPreview = (file: File): Promise<string> =>
    new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = () => reject(new Error('Failed to read file'));
      reader.readAsDataURL(file);
    });

  const performUpload = async (file: File, target: UploadTarget): Promise<string> => {
    // Step 1: request presigned URL
    const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
      fileName: file.name,
      contentType: file.type,
      fileSize: file.size,
    });

    // Step 2: PUT to presigned URL (use fetch for simplicity; XHR used in MessageCompose for progress)
    await new Promise<void>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.upload.addEventListener('progress', (ev) => {
        if (ev.lengthComputable) {
          setUploadProgress(Math.round((ev.loaded / ev.total) * 100));
        }
      });
      xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) resolve();
        else reject(new Error(`Upload failed with status ${xhr.status}`));
      });
      xhr.addEventListener('error', () => reject(new Error('Upload network error')));
      xhr.open('PUT', uploadUrl);
      xhr.setRequestHeader('Content-Type', file.type);
      xhr.send(file);
    });

    // Step 3: confirm upload
    const confirmed = await api.post<{ url: string }>(`/api/v1/attachments/${attachmentId}/confirm`, {});
    return confirmed.url ?? uploadUrl;
  };

  const handleFileSelect = async (e: Event, target: UploadTarget) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    const validationError = validateFile(file);
    if (validationError) {
      setError(validationError);
      return;
    }

    setUploading(true);
    setUploadTarget(target);
    setUploadProgress(0);
    setError(null);
    setSuccess(null);

    try {
      // Show local preview immediately
      const preview = await readPreview(file);
      if (target === 'icon') setIconPreview(preview);
      else setBannerPreview(preview);

      // Upload
      const url = await performUpload(file, target);

      // Update server record
      const updates =
        target === 'icon'
          ? { iconUrl: url }
          : { bannerUrl: url };

      const updated = await api.put<Server>(`/api/v1/servers/${props.server.id}`, updates);
      props.onUpdated?.(updated);
      setSuccess(target === 'icon' ? 'Server icon updated.' : 'Server banner updated.');
    } catch (err) {
      console.error('Upload failed:', err);
      setError('Upload failed. Please try again.');
      if (target === 'icon') setIconPreview(null);
      else setBannerPreview(null);
    } finally {
      setUploading(false);
      setUploadTarget(null);
    }
  };

  const currentIconUrl = () => iconPreview() ?? props.server.iconUrl ?? null;
  const currentBannerUrl = () => bannerPreview() ?? props.server.bannerUrl ?? null;

  return (
    <div class={styles.container}>
      {/* Hidden file inputs */}
      <input
        ref={iconInputRef}
        type="file"
        accept="image/*"
        class={styles.hidden}
        data-testid="icon-file-input"
        onChange={(e) => handleFileSelect(e, 'icon')}
      />
      <input
        ref={bannerInputRef}
        type="file"
        accept="image/*"
        class={styles.hidden}
        data-testid="banner-file-input"
        onChange={(e) => handleFileSelect(e, 'banner')}
      />

      {/* Error / success messages */}
      <Show when={error()}>
        <p class={styles.errorText} role="alert" data-testid="upload-error">{error()}</p>
      </Show>
      <Show when={success()}>
        <p class={styles.successText} role="status" data-testid="upload-success">{success()}</p>
      </Show>

      {/* Upload progress */}
      <Show when={uploading()}>
        <div class={styles.progressWrapper} aria-live="polite" data-testid="upload-progress-bar">
          <div class={styles.progressHeader}>
            <span>Uploading {uploadTarget()}...</span>
            <span>{uploadProgress()}%</span>
          </div>
          <div class={styles.progressTrack}>
            <div
              class={styles.progressFill}
              style={{ width: `${uploadProgress()}%` }}
            />
          </div>
        </div>
      </Show>

      {/* Server icon */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Server Icon
        </label>
        <button
          type="button"
          class={styles.iconButton}
          onClick={handleIconClick}
          disabled={uploading()}
          aria-label="Change server icon"
          data-testid="server-icon-button"
        >
          <Show
            when={currentIconUrl()}
            fallback={
              <span class={styles.iconInitial}>
                {props.server.name.charAt(0).toUpperCase()}
              </span>
            }
          >
            <img
              src={currentIconUrl()!}
              alt={`${props.server.name} icon`}
              class={styles.iconImage}
              data-testid="server-icon-preview"
            />
          </Show>
          {/* Hover overlay */}
          <span class={styles.hoverOverlay}>
            Change
          </span>
        </button>
        <p class={styles.hint}>Images only, max 8 MB. Click icon to change.</p>
      </div>

      {/* Server banner */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Server Banner
        </label>
        <button
          type="button"
          class={styles.bannerButton}
          onClick={handleBannerClick}
          disabled={uploading()}
          aria-label="Change server banner"
          data-testid="server-banner-button"
        >
          <Show when={currentBannerUrl()}>
            <img
              src={currentBannerUrl()!}
              alt={`${props.server.name} banner`}
              class={styles.bannerImage}
              data-testid="server-banner-preview"
            />
          </Show>
          {/* Hover overlay */}
          <span class={`${styles.hoverOverlay} ${styles.bannerHoverText}`}>
            Change Banner
          </span>
        </button>
        <p class={styles.hint}>Images only, max 8 MB. Click banner to change.</p>
      </div>
    </div>
  );
}
