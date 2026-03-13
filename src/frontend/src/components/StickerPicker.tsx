import { createSignal, For, Show, onMount, createMemo } from 'solid-js';
import { api } from '../api/client';

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
async function uploadFileAndGetAttachmentId(file: File): Promise<string> {
  const requestResp = await api.post<RequestUploadResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type || 'image/png',
    fileSize: file.size,
  });

  const { attachmentId, uploadUrl } = requestResp;

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
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to load stickers');
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
    try {
      // Upload the file and get an attachmentId
      const attachmentId = await uploadFileAndGetAttachmentId(file);

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
      const e = err as { error?: string; detail?: string; title?: string };
      setUploadError(e?.detail ?? e?.error ?? e?.title ?? 'Failed to upload sticker');
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDelete(stickerId: string) {
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/stickers/${stickerId}`);
      setStickers(stickers().filter((s) => s.id !== stickerId));
      setConfirmDeleteId(null);
    } catch (err: unknown) {
      const e = err as { error?: string };
      setError(e?.error ?? 'Failed to delete sticker');
    }
  }

  onMount(() => {
    loadStickers();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">Stickers</h2>
        <Show when={props.canManage}>
          <button
            class="bg-xcord-brand text-white px-3 py-1.5 rounded text-sm hover:bg-xcord-brand-hover transition-colors"
            onClick={() => setShowUpload(!showUpload())}
          >
            {showUpload() ? 'Cancel' : 'Upload Sticker'}
          </button>
        </Show>
      </div>

      {/* Upload form */}
      <Show when={showUpload() && props.canManage}>
        <div class="px-4 py-3 border-b border-xcord-border bg-xcord-bg-primary/30 space-y-2">
          <h3 class="text-white text-sm font-semibold">Upload New Sticker</h3>
          <form onSubmit={handleUpload} class="space-y-2">
            <input
              type="text"
              placeholder="Sticker name (2-32 characters)"
              value={uploadName()}
              onInput={(e) => setUploadName(e.currentTarget.value)}
              maxLength={32}
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <input
              type="text"
              placeholder="Description (optional)"
              value={uploadDescription()}
              onInput={(e) => setUploadDescription(e.currentTarget.value)}
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <input
              type="text"
              placeholder="Tags (comma-separated, e.g. happy,funny,cute)"
              value={uploadTags()}
              onInput={(e) => setUploadTags(e.currentTarget.value)}
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            />
            <label class="block bg-xcord-bg-tertiary text-xcord-text-muted rounded px-3 py-1.5 text-sm cursor-pointer hover:bg-xcord-bg-primary transition-colors">
              {uploadFile() ? uploadFile()!.name : 'Choose image (PNG, GIF, WEBP - max 512 KB)'}
              <input
                type="file"
                accept="image/png,image/gif,image/webp"
                class="hidden"
                onChange={handleFileSelect}
              />
            </label>
            <Show when={uploadError()}>
              <p class="text-red-400 text-xs">{uploadError()}</p>
            </Show>
            <button
              type="submit"
              disabled={isUploading()}
              class="bg-xcord-brand text-white px-4 py-1.5 rounded text-sm hover:bg-xcord-brand-hover disabled:opacity-50 transition-colors"
            >
              {isUploading() ? 'Uploading...' : 'Upload Sticker'}
            </button>
          </form>
        </div>
      </Show>

      {/* Search */}
      <div class="px-4 py-2 border-b border-xcord-border">
        <input
          type="text"
          placeholder="Search stickers..."
          value={searchQuery()}
          onInput={(e) => setSearchQuery(e.currentTarget.value)}
          class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
        />
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      {/* Sticker grid */}
      <div class="flex-1 overflow-y-auto p-4 space-y-5">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-24">
            <p class="text-xcord-text-muted">Loading stickers...</p>
          </div>
        </Show>

        <Show when={!isLoading() && filteredStickers().length === 0}>
          <div class="flex flex-col items-center justify-center h-24 text-xcord-text-muted">
            <p class="font-semibold">No stickers found</p>
            <Show when={searchQuery()}>
              <p class="text-sm mt-1">Try a different search term.</p>
            </Show>
          </div>
        </Show>

        <For each={stickerPacks()}>
          {(pack) => (
            <div>
              <p class="text-xcord-text-muted text-xs uppercase font-semibold tracking-wide mb-2">
                {pack.serverName}
              </p>
              <div class="grid grid-cols-4 sm:grid-cols-5 md:grid-cols-6 gap-2">
                <For each={pack.stickers}>
                  {(sticker) => (
                    <div class="relative group">
                      <button
                        class="w-full aspect-square rounded-lg bg-xcord-bg-primary hover:bg-xcord-bg-tertiary transition-colors overflow-hidden flex items-center justify-center p-1"
                        onClick={() => props.onSelect?.(sticker)}
                        title={sticker.name}
                      >
                        <img
                          src={sticker.imageUrl}
                          alt={sticker.name}
                          class="w-full h-full object-contain"
                        />
                      </button>
                      <p class="text-xcord-text-muted text-xs text-center mt-0.5 truncate">
                        {sticker.name}
                      </p>

                      {/* Management controls */}
                      <Show when={props.canManage}>
                        <Show
                          when={confirmDeleteId() === sticker.id}
                          fallback={
                            <button
                              class="absolute top-0 right-0 bg-red-500 text-white rounded-full w-4 h-4 flex items-center justify-center text-xs opacity-0 group-hover:opacity-100 transition-opacity"
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
                          <div class="absolute inset-0 bg-xcord-bg-primary/90 rounded-lg flex flex-col items-center justify-center space-y-1 p-1">
                            <p class="text-white text-xs font-medium">Delete?</p>
                            <div class="flex space-x-1">
                              <button
                                class="bg-xcord-bg-tertiary text-xcord-text-muted px-1.5 py-0.5 rounded text-xs hover:text-white"
                                onClick={() => setConfirmDeleteId(null)}
                              >
                                No
                              </button>
                              <button
                                class="bg-red-500 text-white px-1.5 py-0.5 rounded text-xs hover:bg-red-600"
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
