import { createSignal, For, Show, onMount, onCleanup, createMemo } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './StickerPicker.module.css';
import EmptyState from './ui/EmptyState';
import { Sticker } from 'lucide-solid';

export interface Sticker {
  id: string;
  serverId: string;
  name: string;
  description?: string;
  imageUrl: string;
  tags: string[];
  createdAt: string;
}

export interface StickerPack {
  serverId: string;
  serverName: string;
  stickers: Sticker[];
}

interface StickerPickerProps {
  serverId: string;
  /** Called when a sticker is selected for sending */
  onSelect?: (sticker: Sticker) => void;
  /** Whether to show management controls (upload/delete) */
  canManage?: boolean;
}

export function filterStickersByQuery(stickers: Sticker[], query: string): Sticker[] {
  const q = query.toLowerCase().trim();
  if (!q) return stickers;
  return stickers.filter(
    (s) =>
      s.name.toLowerCase().includes(q) ||
      s.tags.some((t) => t.toLowerCase().includes(q)) ||
      (s.description?.toLowerCase().includes(q) ?? false),
  );
}

export function groupStickersByServer(stickers: Sticker[]): StickerPack[] {
  const map = new Map<string, StickerPack>();
  for (const sticker of stickers) {
    if (!map.has(sticker.serverId)) {
      map.set(sticker.serverId, {
        serverId: sticker.serverId,
        serverName: `Server ${sticker.serverId}`,
        stickers: [],
      });
    }
    map.get(sticker.serverId)!.stickers.push(sticker);
  }
  return Array.from(map.values());
}

export function validateStickerName(name: string): string | null {
  const trimmed = name.trim();
  if (!trimmed) return 'Sticker name is required.';
  if (trimmed.length < 2) return 'Name must be at least 2 characters.';
  if (trimmed.length > 32) return 'Name must be 32 characters or fewer.';
  return null;
}

// ---- API types ----

interface StickerDto {
  id: string;
  name: string;
  tags?: string;
  imageUrl: string;
  createdAt: string;
}

interface StickerPackDto {
  id: string;
  serverId: string;
  name: string;
  description?: string;
  createdAt: string;
  stickers: StickerDto[];
}

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
  const requestResp = await api.post<RequestUploadResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type || 'image/png',
    fileSize: file.size,
  });

  const { attachmentId, uploadUrl } = requestResp;

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

  await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

  return attachmentId;
}

export default function StickerPicker(props: StickerPickerProps) {
  // Internal pack ID tracking - we create/use a single "Default" pack per server.
  const [defaultPackId, setDefaultPackId] = createSignal<string | null>(null);
  const [stickers, setStickers] = createSignal<Sticker[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isUploading, setIsUploading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [searchQuery, setSearchQuery] = createSignal('');
  const [showUpload, setShowUpload] = createSignal(false);
  const [confirmDeleteId, setConfirmDeleteId] = createSignal<string | null>(null);

  // Upload form
  const [uploadName, setUploadName] = createSignal('');
  const [uploadDescription, setUploadDescription] = createSignal('');
  const [uploadTags, setUploadTags] = createSignal('');
  const [uploadFile, setUploadFile] = createSignal<File | null>(null);
  const [uploadError, setUploadError] = createSignal<string | null>(null);

  // Track in-flight upload controllers so we can abort on unmount
  const activeUploadControllers = new Set<AbortController>();

  onCleanup(() => {
    for (const controller of activeUploadControllers) {
      controller.abort();
    }
    activeUploadControllers.clear();
  });

  async function loadStickers() {
    setIsLoading(true);
    setError(null);
    try {
      const packs = await api.get<StickerPackDto[]>(`/api/v1/servers/${props.serverId}/sticker-packs`);
      // Store the first pack's ID for sticker creation (or create one later)
      if (packs.length > 0) {
        setDefaultPackId(String(packs[0].id));
      }
      // Flatten all stickers from all packs into a single list
      const allStickers: Sticker[] = packs.flatMap((pack) =>
        (pack.stickers ?? []).map((s) => ({
          id: String(s.id),
          serverId: String(pack.serverId),
          name: s.name,
          imageUrl: s.imageUrl,
          tags: s.tags ? s.tags.split(',').map((t) => t.trim()).filter(Boolean) : [],
          createdAt: s.createdAt,
        }))
      );
      setStickers(allStickers);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load stickers'));
    } finally {
      setIsLoading(false);
    }
  }

  const filteredStickers = createMemo(() =>
    filterStickersByQuery(stickers(), searchQuery()),
  );

  const stickerPacks = createMemo(() => groupStickersByServer(filteredStickers()));

  function handleFileSelect(e: Event) {
    const input = e.currentTarget as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    setUploadFile(file);
    setUploadError(null);
    if (file && !uploadName()) {
      const name = file.name.replace(/\.[^.]+$/, '').replace(/[^a-z0-9_ ]/gi, '_');
      setUploadName(name.slice(0, 32));
    }
  }

  async function ensureDefaultPack(): Promise<string> {
    // If we already have a pack ID cached, use it
    const existing = defaultPackId();
    if (existing) return existing;

    // Create a default sticker pack for this server
    const pack = await api.post<StickerPackDto>(`/api/v1/servers/${props.serverId}/sticker-packs`, {
      name: 'Default',
      description: 'Default sticker pack',
    });

    const packId = String(pack.id);
    setDefaultPackId(packId);
    return packId;
  }

  async function handleUpload(e: Event) {
    e.preventDefault();
    setUploadError(null);

    const nameErr = validateStickerName(uploadName());
    if (nameErr) { setUploadError(nameErr); return; }

    const file = uploadFile();
    if (!file) { setUploadError('Please select an image file.'); return; }

    setIsUploading(true);
    const controller = new AbortController();
    activeUploadControllers.add(controller);
    try {
      // Upload the file and get an attachmentId
      const attachmentId = await uploadFileAndGetAttachmentId(file, controller.signal);

      // Ensure we have a sticker pack to add to
      const packId = await ensureDefaultPack();

      // Build tags string
      const tagsStr = uploadTags()
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean)
        .join(',') || undefined;

      // Create the sticker record
      const stickerResp = await api.post<{ id: string; name: string; tags?: string; imageUrl: string; createdAt: string }>(
        `/api/v1/servers/${props.serverId}/sticker-packs/${packId}/stickers`,
        {
          name: uploadName().trim(),
          tags: tagsStr ?? null,
          attachmentId,
        }
      );

      const newSticker: Sticker = {
        id: String(stickerResp.id),
        serverId: props.serverId,
        name: stickerResp.name,
        imageUrl: stickerResp.imageUrl,
        tags: stickerResp.tags ? stickerResp.tags.split(',').map((t) => t.trim()).filter(Boolean) : [],
        createdAt: stickerResp.createdAt,
      };

      setStickers([...stickers(), newSticker]);

      // Reset form
      setUploadName('');
      setUploadDescription('');
      setUploadTags('');
      setUploadFile(null);
      setShowUpload(false);
    } catch (err: unknown) {
      // Swallow aborts triggered by component unmount
      if (err instanceof DOMException && err.name === 'AbortError') {
        return;
      }
      setUploadError(getErrorMessage(err, 'Failed to upload sticker'));
    } finally {
      activeUploadControllers.delete(controller);
      setIsUploading(false);
    }
  }

  async function handleDelete(stickerId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/stickers/${stickerId}`);
      setStickers(stickers().filter((s) => s.id !== stickerId));
      setConfirmDeleteId(null);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to delete sticker'));
    }
  }

  onMount(() => {
    loadStickers();
  });

  return (
    <div class={styles.picker} data-testid="sticker-picker-container">
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Stickers</h2>
        <Show when={props.canManage}>
          <button
            class={styles.uploadToggleBtn}
            data-testid="sticker-upload-toggle-button"
            onClick={() => setShowUpload(!showUpload())}
          >
            {showUpload() ? 'Cancel' : 'Upload Sticker'}
          </button>
        </Show>
      </div>

      {/* Upload form */}
      <Show when={showUpload() && props.canManage}>
        <div class={styles.uploadPanel} data-testid="sticker-upload-panel">
          <h3 class={styles.uploadPanelTitle}>Upload New Sticker</h3>
          <form onSubmit={handleUpload} class={styles.uploadForm}>
            <input
              type="text"
              data-testid="sticker-name-input"
              placeholder="Sticker name (2-32 characters)"
              value={uploadName()}
              onInput={(e) => setUploadName(e.currentTarget.value)}
              maxLength={32}
              class={styles.textInput}
            />
            <input
              type="text"
              data-testid="sticker-description-input"
              placeholder="Description (optional)"
              value={uploadDescription()}
              onInput={(e) => setUploadDescription(e.currentTarget.value)}
              class={styles.textInput}
            />
            <input
              type="text"
              data-testid="sticker-tags-input"
              placeholder="Tags (comma-separated, e.g. happy,funny,cute)"
              value={uploadTags()}
              onInput={(e) => setUploadTags(e.currentTarget.value)}
              class={styles.textInput}
            />
            <label class={styles.fileLabel}>
              {uploadFile() ? uploadFile()!.name : 'Choose image (PNG, GIF, WEBP - max 512 KB)'}
              <input
                type="file"
                accept="image/png,image/gif,image/webp"
                class={styles.hiddenInput}
                data-testid="sticker-file-input"
                onChange={handleFileSelect}
              />
            </label>
            <Show when={uploadError()}>
              <p class={styles.uploadError}>{uploadError()}</p>
            </Show>
            <button
              type="submit"
              disabled={isUploading()}
              class={styles.uploadSubmitBtn}
              data-testid="sticker-upload-submit-button"
            >
              {isUploading() ? 'Uploading...' : 'Upload Sticker'}
            </button>
          </form>
        </div>
      </Show>

      {/* Search */}
      <div class={styles.searchBar}>
        <input
          type="text"
          placeholder="Search stickers..."
          data-testid="sticker-search-input"
          value={searchQuery()}
          onInput={(e) => setSearchQuery(e.currentTarget.value)}
          class={styles.textInput}
        />
      </div>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      {/* Sticker grid */}
      <div class={styles.stickerArea} data-testid="sticker-grid-area">
        <Show when={isLoading()}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading stickers...</p>
          </div>
        </Show>

        <Show when={!isLoading() && filteredStickers().length === 0}>
          <EmptyState
            icon={Sticker}
            title={searchQuery() ? 'No stickers match that search' : 'No stickers yet'}
            body={searchQuery() ? 'Try a different word.' : 'Stickers added to your communities show up here.'}
            dense
            data-testid="sticker-picker-empty-state"
          />
        </Show>

        <For each={stickerPacks()}>
          {(pack) => (
            <div class={styles.packSection} data-testid={`sticker-pack-${pack.serverId}`}>
              <p class={styles.packName}>
                {pack.serverName}
              </p>
              <div class={styles.stickerGrid}>
                <For each={pack.stickers}>
                  {(sticker) => (
                    <div class={styles.stickerItem} data-testid={`sticker-item-${sticker.id}`}>
                      <button
                        class={styles.stickerBtn}
                        data-testid={`sticker-select-button-${sticker.id}`}
                        onClick={() => props.onSelect?.(sticker)}
                        title={sticker.name}
                      >
                        <img
                          src={sticker.imageUrl}
                          alt={sticker.name}
                          class={styles.stickerImage}
                        />
                      </button>
                      <p class={styles.stickerName}>
                        {sticker.name}
                      </p>

                      {/* Management controls */}
                      <Show when={props.canManage}>
                        <Show
                          when={confirmDeleteId() === sticker.id}
                          fallback={
                            <button
                              class={styles.deleteBtn}
                              data-testid={`delete-sticker-button-${sticker.id}`}
                              onClick={(e) => {
                                e.stopPropagation();
                                setConfirmDeleteId(sticker.id);
                              }}
                              aria-label={`Delete sticker ${sticker.name}`}
                            >
                              x
                            </button>
                          }
                        >
                          <div class={styles.confirmOverlay}>
                            <p class={styles.confirmTitle}>Delete?</p>
                            <div class={styles.confirmActions}>
                              <button
                                class={styles.confirmNoBtn}
                                onClick={() => setConfirmDeleteId(null)}
                              >
                                No
                              </button>
                              <button
                                class={styles.confirmYesBtn}
                                onClick={() => handleDelete(sticker.id)}
                              >
                                Yes
                              </button>
                            </div>
                          </div>
                        </Show>
                      </Show>
                    </div>
                  )}
                </For>
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
