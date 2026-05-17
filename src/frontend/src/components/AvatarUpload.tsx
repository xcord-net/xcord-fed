import { createSignal, Show } from 'solid-js';
import { api } from '../api/client';
import type { UserProfile } from '../types/profile';
import Flexbox from './ui/Flexbox';
import styles from './AvatarUpload.module.css';

const MAX_FILE_SIZE = 8 * 1024 * 1024; // 8 MB

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface AvatarUploadProps {
  profile: UserProfile;
  onUpdated?: (profile: UserProfile) => void;
}

// ---- Pure helpers ----

export function validateAvatarFile(file: { type: string; size: number }): string | null {
  if (!file.type.startsWith('image/')) return 'Only image files are allowed.';
  if (file.size > MAX_FILE_SIZE) return 'File size must be 8 MB or less.';
  return null;
}

/**
 * Executes the full avatar upload flow:
 *   1. POST /api/v1/uploads to obtain a presigned URL
 *   2. PUT to the presigned URL (via the provided xhrFactory)
 *   3. POST /api/v1/attachments/{id}/confirm to finalise the attachment
 *   4. PUT /api/v1/users/@me to update the user's avatar URL
 *
 * Returns the confirmed CDN URL for the new avatar.
 * Exported for direct unit-testing of each step.
 */
export async function uploadAvatar(
  file: { name: string; type: string; size: number },
  xhrFactory: (uploadUrl: string) => Promise<void>,
): Promise<string> {
  // Step 1: request presigned URL
  const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type,
    fileSize: file.size,
  });

  // Step 2: PUT to presigned URL
  await xhrFactory(uploadUrl);

  // Step 3: confirm upload
  const confirmed = await api.post<{ url: string }>(`/api/v1/attachments/${attachmentId}/confirm`, {});
  const avatarUrl = confirmed.url ?? uploadUrl;

  // Step 4: update profile
  await api.put<UserProfile>('/api/v1/users/@me', { avatarUrl });

  return avatarUrl;
}

// ---- Component ----

export default function AvatarUpload(props: AvatarUploadProps) {
  const [preview, setPreview] = createSignal<string | null>(null);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploading, setUploading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [success, setSuccess] = createSignal<string | null>(null);

  let fileInputRef: HTMLInputElement | undefined;

  const handleAvatarClick = () => {
    setError(null);
    fileInputRef?.click();
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

  const handleFileSelect = async (e: Event) => {
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
    setUploadProgress(0);
    setError(null);
    setSuccess(null);

    try {
      // Show local circular preview immediately
      const dataUrl = await readPreview(file);
      setPreview(dataUrl);

      // XHR factory with upload-progress tracking wired to component state
      const xhrFactory = (uploadUrl: string): Promise<void> =>
        new Promise<void>((resolve, reject) => {
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

      const avatarUrl = await uploadAvatar(file, xhrFactory);

      // Notify the parent with the updated profile (new avatar URL merged into existing profile)
      props.onUpdated?.({ ...props.profile, avatarUrl });
      setSuccess('Avatar updated successfully.');
    } catch (err) {
      console.error('Avatar upload failed:', err);
      setError('Upload failed. Please try again.');
      setPreview(null);
    } finally {
      setUploading(false);
    }
  };

  const currentAvatarUrl = () => preview() ?? props.profile.avatarUrl ?? null;

  return (
    <Flexbox direction="vertical" gap={1}>
      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        class={styles.hidden}
        data-testid="avatar-file-input"
        onChange={handleFileSelect}
      />

      {/* Error / success messages */}
      <Show when={error()}>
        <p class={styles.errorText} role="alert" data-testid="avatar-upload-error">{error()}</p>
      </Show>
      <Show when={success()}>
        <p class={styles.successText} role="status" data-testid="avatar-upload-success">{success()}</p>
      </Show>

      {/* Upload progress */}
      <Show when={uploading()}>
        <Flexbox direction="vertical" gap={0.25} aria-live="polite" data-testid="avatar-upload-progress">
          <Flexbox align="center" justify="between" class={styles.progressHeader}>
            <span>Uploading avatar...</span>
            <span>{uploadProgress()}%</span>
          </Flexbox>
          <div class={styles.progressTrack}>
            <div
              class={styles.progressFill}
              style={{ width: `${uploadProgress()}%` }}
            />
          </div>
        </Flexbox>
      </Show>

      {/* Avatar button - circular, clickable */}
      <Flexbox direction="vertical" align="center" gap={0.75}>
        <button
          type="button"
          class={styles.avatarButton}
          onClick={handleAvatarClick}
          disabled={uploading()}
          aria-label="Change avatar"
          data-testid="avatar-button"
        >
          <Show
            when={currentAvatarUrl()}
            fallback={
              <span class={styles.avatarInitial}>
                {props.profile.username.charAt(0).toUpperCase()}
              </span>
            }
          >
            <img
              src={currentAvatarUrl()!}
              alt={`${props.profile.username}'s avatar`}
              class={styles.avatarImage}
              data-testid="avatar-preview"
            />
          </Show>
          {/* Hover overlay */}
          <span class={styles.hoverOverlay}>
            Change
          </span>
        </button>

        <div class={styles.userInfo}>
          <p class={styles.displayName}>{props.profile.displayName}</p>
          <p class={styles.username}>@{props.profile.username}</p>
          <p class={styles.hint}>Images only, max 8 MB</p>
        </div>
      </Flexbox>
    </Flexbox>
  );
}
