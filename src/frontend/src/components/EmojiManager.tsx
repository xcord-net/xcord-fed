import { createSignal, For, Show, onMount, onCleanup } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import type { CustomEmoji } from '../types/emoji';
import styles from './EmojiManager.module.css';
import EmptyState from './ui/EmptyState';
import { Smile } from 'lucide-solid';

interface EmojiManagerProps {
  serverId: string;
}

// ---- Pure helpers ----

export function validateEmojiName(name: string): string | null {
  if (!name.trim()) return 'Please enter a name for the emoji.';
  if (!/^[a-z0-9_]{2,32}$/i.test(name.trim())) {
    return 'Emoji name must be 2-32 characters: letters, numbers, underscores only.';
  }
  return null;
}

export function deriveEmojiName(fileName: string): string {
  return fileName.replace(/\.[^.]+$/, '').replace(/[^a-z0-9_]/gi, '_').toLowerCase();
}

// ---- Upload flow helpers ----

interface RequestUploadResponse {
  attachmentId: string;
  uploadUrl: string;
  downloadUrl: string;
}

/**
 * Upload a file through the backend proxy and return the attachmentId.
 * Flow:
 *   1. POST /api/v1/uploads   → get { attachmentId, uploadUrl }
 *   2. PUT  {uploadUrl}       → raw bytes
 *   3. POST /api/v1/attachments/{attachmentId}/confirm
 */
async function uploadFileAndGetAttachmentId(file: File, signal?: AbortSignal): Promise<string> {
  // Step 1: request an upload slot
  const requestResp = await api.post<RequestUploadResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type || 'image/png',
    fileSize: file.size,
  });

  const { attachmentId, uploadUrl } = requestResp;

  // Step 2: upload raw bytes to the proxy endpoint
  //
  // Raw fetch is intentional: `uploadUrl` is an S3-style presigned URL (the
  // backend issues a pre-signed PUT that the browser sends directly to object
  // storage). It does not hit the Xcord API, so the api client wrapper -- with
  // its CSRF header, bearer auth, and 401/refresh handling -- would actively
  // break the signed-URL contract.
  const bytes = await file.arrayBuffer();
  const putResp = await fetch(uploadUrl, {
    method: 'PUT',
    body: bytes,
    headers: { 'Content-Type': file.type || 'image/png' },
    credentials: 'include',
    signal,
  });

  if (!putResp.ok) {
    const errBody = await putResp.json().catch(() => ({ error: 'Upload failed' }));
    throw errBody;
  }

  // Step 3: confirm the upload so IsConfirmed is set in the DB
  await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

  return attachmentId;
}

// ---- Component ----

export default function EmojiManager(props: EmojiManagerProps) {
  const [emojis, setEmojis] = createSignal<CustomEmoji[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isUploading, setIsUploading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = createSignal<string | null>(null);

  // Upload form state
  const [emojiName, setEmojiName] = createSignal('');
  const [selectedFile, setSelectedFile] = createSignal<File | null>(null);
  const [previewUrl, setPreviewUrl] = createSignal<string | null>(null);
  const [uploadError, setUploadError] = createSignal<string | null>(null);

  // Track in-flight upload controllers so we can abort on unmount
  const activeUploadControllers = new Set<AbortController>();

  onCleanup(() => {
    for (const controller of activeUploadControllers) {
      controller.abort();
    }
    activeUploadControllers.clear();
  });

  async function loadEmojis() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<{ emojis: CustomEmoji[] }>(`/api/v1/servers/${props.serverId}/emojis`);
      setEmojis(result.emojis ?? []);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load emojis'));
    } finally {
      setIsLoading(false);
    }
  }

  function handleFileSelect(e: Event) {
    const input = e.currentTarget as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    setSelectedFile(file);
    setUploadError(null);

    if (previewUrl()) {
      URL.revokeObjectURL(previewUrl()!);
    }

    if (file) {
      setPreviewUrl(URL.createObjectURL(file));
      // Auto-fill name from filename if empty
      if (!emojiName()) {
        setEmojiName(deriveEmojiName(file.name));
      }
    } else {
      setPreviewUrl(null);
    }
  }

  async function handleUpload(e: Event) {
    e.preventDefault();
    setUploadError(null);

    const file = selectedFile();
    const name = emojiName().trim();

    if (!file) {
      setUploadError('Please select an image file.');
      return;
    }
    const nameError = validateEmojiName(name);
    if (nameError) {
      setUploadError(nameError);
      return;
    }

    setIsUploading(true);
    const controller = new AbortController();
    activeUploadControllers.add(controller);
    try {
      // Upload the file and get an attachmentId
      const attachmentId = await uploadFileAndGetAttachmentId(file, controller.signal);

      // Determine if animated (GIF)
      const isAnimated = file.type === 'image/gif';

      // Create the emoji record using the attachmentId
      const newEmoji = await api.post<CustomEmoji>(`/api/v1/servers/${props.serverId}/emojis`, {
        name,
        attachmentId,
        isAnimated,
      });

      setEmojis([...emojis(), newEmoji]);

      // Reset form
      setEmojiName('');
      setSelectedFile(null);
      if (previewUrl()) {
        URL.revokeObjectURL(previewUrl()!);
        setPreviewUrl(null);
      }
    } catch (err: unknown) {
      // Swallow aborts triggered by component unmount
      if (err instanceof DOMException && err.name === 'AbortError') {
        return;
      }
      setUploadError(getErrorMessage(err, 'Failed to upload emoji'));
    } finally {
      activeUploadControllers.delete(controller);
      setIsUploading(false);
    }
  }

  async function deleteEmoji(emojiId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/emojis/${emojiId}`);
      setEmojis(emojis().filter((em) => em.id !== emojiId));
      setConfirmingDelete(null);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to delete emoji'));
    }
  }

  onMount(() => {
    loadEmojis();
  });

  return (
    <div class={styles.container} data-testid="emoji-manager-container">
      <div class={styles.header}>
        <h2 id="emoji-manager-heading" class={styles.headerTitle}>Custom Emojis</h2>
      </div>

      {/* Upload form */}
      <div class={styles.uploadSection}>
        <h3 class={styles.uploadTitle}>Upload New Emoji</h3>
        <form onSubmit={handleUpload} class={styles.uploadForm} data-testid="emoji-upload-form">
          <div class={styles.fileRow}>
            <Show when={previewUrl()}>
              <img
                src={previewUrl()!}
                alt="Preview"
                class={styles.previewImg}
              />
            </Show>
            <label class={styles.fileLabel}>
              {selectedFile() ? selectedFile()!.name : 'Choose image (PNG, GIF, WEBP - max 256 KB)'}
              <input
                type="file"
                accept="image/png,image/gif,image/webp"
                class={styles.hiddenFileInput}
                data-testid="emoji-file-input"
                onChange={handleFileSelect}
              />
            </label>
          </div>

          <input
            id="emoji-manager-name-input"
            data-testid="emoji-name-input"
            type="text"
            placeholder="Emoji name (e.g. cool_face)"
            class={styles.nameInput}
            value={emojiName()}
            onInput={(e) => setEmojiName(e.currentTarget.value)}
            maxLength={32}
          />

          <Show when={uploadError()}>
            <p class={styles.uploadError}>{uploadError()}</p>
          </Show>

          <button
            id="emoji-manager-upload-btn"
            data-testid="emoji-upload-submit-button"
            type="submit"
            disabled={isUploading()}
            class={styles.uploadButton}
          >
            {isUploading() ? 'Uploading...' : 'Upload Emoji'}
          </button>
        </form>
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <div class={styles.emojiListArea}>
        <Show when={isLoading()}>
          <div class={styles.loadingContainer}>
            <p class={styles.mutedText}>Loading emojis...</p>
          </div>
        </Show>

        <Show when={!isLoading() && emojis().length === 0}>
          <div id="emoji-manager-empty">
            <EmptyState
              icon={Smile}
              title="No custom emoji yet"
              body="Upload an image above and it becomes usable everywhere in this community."
              data-testid="emoji-list-empty-state"
            />
          </div>
        </Show>

        <div class={styles.emojiGrid} data-testid="emoji-list">
          <For each={emojis()}>
            {(emoji) => (
              <div class={styles.emojiItem} data-testid={`emoji-item-${emoji.name}`}>
                <div class={styles.emojiImageBox}>
                  <img
                    src={emoji.imageUrl}
                    alt={`:${emoji.name}:`}
                    title={`:${emoji.name}:`}
                    class={styles.emojiImage}
                  />
                </div>
                <span class={styles.emojiName}>
                  {emoji.name}
                </span>

                <Show
                  when={confirmingDelete() === emoji.id}
                  fallback={
                    <button
                      class={styles.deleteEmojiButton}
                      data-testid={`delete-emoji-button-${emoji.name}`}
                      onClick={() => setConfirmingDelete(emoji.id)}
                      title={`Delete :${emoji.name}:`}
                      aria-label={`Delete emoji ${emoji.name}`}
                    >
                      x
                    </button>
                  }
                >
                  <div class={styles.deleteConfirmOverlay} data-testid={`emoji-delete-confirm-${emoji.name}`}>
                    <p class={styles.deleteConfirmText}>Delete?</p>
                    <div class={styles.deleteConfirmButtons}>
                      <button
                        class={styles.deleteNoBtn}
                        onClick={() => setConfirmingDelete(null)}
                      >
                        No
                      </button>
                      <button
                        class={styles.deleteYesBtn}
                        data-testid={`emoji-delete-confirm-yes-${emoji.name}`}
                        onClick={() => deleteEmoji(emoji.id)}
                      >
                        Yes
                      </button>
                    </div>
                  </div>
                </Show>
              </div>
            )}
          </For>
        </div>
      </div>
    </div>
  );
}
