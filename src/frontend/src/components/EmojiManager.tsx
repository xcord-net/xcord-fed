import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import type { CustomEmoji } from '../types/emoji';

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
async function uploadFileAndGetAttachmentId(file: File): Promise<string> {
  // Step 1: request an upload slot
  const requestResp = await api.post<RequestUploadResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type || 'image/png',
    fileSize: file.size,
  });

  const { attachmentId, uploadUrl } = requestResp;

  // Step 2: upload raw bytes to the proxy endpoint
  const bytes = await file.arrayBuffer();
  const putResp = await fetch(uploadUrl, {
    method: 'PUT',
    body: bytes,
    headers: { 'Content-Type': file.type || 'image/png' },
    credentials: 'include',
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

  async function loadEmojis() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<{ emojis: CustomEmoji[] }>(`/api/v1/servers/${props.serverId}/emojis`);
      setEmojis(result.emojis ?? []);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load emojis');
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
    try {
      // Upload the file and get an attachmentId
      const attachmentId = await uploadFileAndGetAttachmentId(file);

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
      const e = err as { error?: string; detail?: string; title?: string };
      setUploadError(e?.detail ?? e?.error ?? e?.title ?? 'Failed to upload emoji');
    } finally {
      setIsUploading(false);
    }
  }

  async function deleteEmoji(emojiId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/emojis/${emojiId}`);
      setEmojis(emojis().filter((em) => em.id !== emojiId));
      setConfirmingDelete(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to delete emoji');
    }
  }

  onMount(() => {
    loadEmojis();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 id="emoji-manager-heading" class="text-white font-semibold">Custom Emojis</h2>
      </div>

      {/* Upload form */}
      <div class="px-4 py-3 border-b border-xcord-border bg-xcord-bg-primary/30">
        <h3 class="text-white text-sm font-medium mb-2">Upload New Emoji</h3>
        <form onSubmit={handleUpload} class="flex flex-col space-y-2">
          <div class="flex items-center space-x-3">
            <Show when={previewUrl()}>
              <img
                src={previewUrl()!}
                alt="Preview"
                class="w-10 h-10 rounded object-contain bg-xcord-bg-tertiary"
              />
            </Show>
            <label class="flex-1 bg-xcord-bg-tertiary text-xcord-text-muted rounded px-3 py-1.5 text-sm cursor-pointer hover:bg-xcord-bg-primary transition-colors">
              {selectedFile() ? selectedFile()!.name : 'Choose image (PNG, GIF, WEBP - max 256 KB)'}
              <input
                type="file"
                accept="image/png,image/gif,image/webp"
                class="hidden"
                onChange={handleFileSelect}
              />
            </label>
          </div>

          <input
            id="emoji-manager-name-input"
            type="text"
            placeholder="Emoji name (e.g. cool_face)"
            class="bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm border border-xcord-border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
            value={emojiName()}
            onInput={(e) => setEmojiName(e.currentTarget.value)}
            maxLength={32}
          />

          <Show when={uploadError()}>
            <p class="text-red-400 text-xs">{uploadError()}</p>
          </Show>

          <button
            id="emoji-manager-upload-btn"
            type="submit"
            disabled={isUploading()}
            class="bg-xcord-brand text-white px-4 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50 self-start"
          >
            {isUploading() ? 'Uploading...' : 'Upload Emoji'}
          </button>
        </form>
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <div class="flex-1 overflow-y-auto p-4">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading emojis...</p>
          </div>
        </Show>

        <Show when={!isLoading() && emojis().length === 0}>
          <div id="emoji-manager-empty" class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
            <p class="text-lg font-semibold">No custom emojis</p>
            <p class="text-sm mt-1">Upload an emoji above to get started.</p>
          </div>
        </Show>

        <div class="grid grid-cols-4 sm:grid-cols-6 md:grid-cols-8 gap-3">
          <For each={emojis()}>
            {(emoji) => (
              <div class="flex flex-col items-center space-y-1 group relative">
                <div class="w-14 h-14 rounded bg-xcord-bg-tertiary flex items-center justify-center overflow-hidden">
                  <img
                    src={emoji.imageUrl}
                    alt={`:${emoji.name}:`}
                    title={`:${emoji.name}:`}
                    class="w-12 h-12 object-contain"
                  />
                </div>
                <span class="text-xcord-text-muted text-xs truncate w-full text-center">
                  {emoji.name}
                </span>

                <Show
                  when={confirmingDelete() === emoji.id}
                  fallback={
                    <button
                      class="opacity-0 group-hover:opacity-100 absolute -top-1 -right-1 bg-red-500 text-white rounded-full w-5 h-5 flex items-center justify-center text-xs hover:bg-red-600 transition-opacity"
                      onClick={() => setConfirmingDelete(emoji.id)}
                      title={`Delete :${emoji.name}:`}
                      aria-label={`Delete emoji ${emoji.name}`}
                    >
                      x
                    </button>
                  }
                >
                  <div class="absolute inset-0 bg-xcord-bg-primary/90 rounded flex flex-col items-center justify-center space-y-1 p-1">
                    <p class="text-white text-xs font-medium text-center">Delete?</p>
                    <div class="flex space-x-1">
                      <button
                        class="bg-xcord-bg-tertiary text-xcord-text-muted px-2 py-0.5 rounded text-xs hover:text-white"
                        onClick={() => setConfirmingDelete(null)}
                      >
                        No
                      </button>
                      <button
                        class="bg-red-500 text-white px-2 py-0.5 rounded text-xs hover:bg-red-600"
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
