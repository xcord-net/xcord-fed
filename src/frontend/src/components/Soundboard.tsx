import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface Sound {
  id: string;
  name: string;
  serverId: string;
  uploadedBy: string;
  durationMs: number;
  isDefault: boolean;
  createdAt: string;
}

export interface SoundboardProps {
  serverId: string;
}

// ---- Helpers ----

export function formatSoundDuration(ms: number): string {
  const totalSeconds = Math.round(ms / 1000);
  const mins = Math.floor(totalSeconds / 60);
  const secs = totalSeconds % 60;
  if (mins > 0) {
    return `${mins}m ${secs}s`;
  }
  return `${secs}s`;
}

export function validateSoundName(name: string): string | null {
  if (!name.trim()) return 'Sound name is required.';
  if (name.trim().length > 50) return 'Sound name must be 50 characters or fewer.';
  return null;
}

export function sortSounds(sounds: Sound[]): Sound[] {
  // Default sounds first, then custom sounds alphabetically
  return [...sounds].sort((a, b) => {
    if (a.isDefault && !b.isDefault) return -1;
    if (!a.isDefault && b.isDefault) return 1;
    return a.name.localeCompare(b.name);
  });
}

// ---- Component ----

export default function Soundboard(props: SoundboardProps) {
  const [sounds, setSounds] = createSignal<Sound[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [playingId, setPlayingId] = createSignal<string | null>(null);
  const [showUpload, setShowUpload] = createSignal(false);
  const [uploadName, setUploadName] = createSignal('');
  const [uploadFile, setUploadFile] = createSignal<File | null>(null);
  const [isUploading, setIsUploading] = createSignal(false);
  const [uploadError, setUploadError] = createSignal<string | null>(null);

  let fileInputRef: HTMLInputElement | undefined;

  const loadSounds = async (serverId: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.get<Sound[]>(`/api/v1/servers/${serverId}/sounds`);
      setSounds(sortSounds(data));
    } catch {
      setError('Failed to load sounds.');
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadSounds(serverId);
    }
  });

  const playSound = async (soundId: string) => {
    if (playingId() === soundId) return;
    setPlayingId(soundId);
    setError(null);
    try {
      await api.post(`/api/v1/servers/${props.serverId}/sounds/${soundId}/play`, {});
    } catch {
      setError('Failed to play sound.');
    } finally {
      // Brief visual feedback — clear after 1s
      setTimeout(() => {
        setPlayingId((current) => (current === soundId ? null : current));
      }, 1000);
    }
  };

  const handleFileChange = (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    setUploadFile(file);
    if (file && !uploadName().trim()) {
      // Pre-fill name from file name (without extension)
      setUploadName(file.name.replace(/\.[^.]+$/, ''));
    }
  };

  const handleUpload = async () => {
    const nameVal = uploadName().trim();
    const file = uploadFile();

    const nameError = validateSoundName(nameVal);
    if (nameError) {
      setUploadError(nameError);
      return;
    }
    if (!file) {
      setUploadError('Please select a sound file.');
      return;
    }

    setIsUploading(true);
    setUploadError(null);

    try {
      const newSound = await api.post<Sound>(`/api/v1/servers/${props.serverId}/sounds`, {
        name: nameVal,
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type || 'audio/mpeg',
      });
      setSounds((prev) => sortSounds([...prev, newSound]));
      // Reset upload form
      setUploadName('');
      setUploadFile(null);
      if (fileInputRef) fileInputRef.value = '';
      setShowUpload(false);
    } catch {
      setUploadError('Failed to upload sound. Please try again.');
    } finally {
      setIsUploading(false);
    }
  };

  const cancelUpload = () => {
    setShowUpload(false);
    setUploadName('');
    setUploadFile(null);
    if (fileInputRef) fileInputRef.value = '';
    setUploadError(null);
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary" aria-label="Soundboard">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Soundboard</h2>
        <button
          class="bg-xcord-brand text-white px-3 py-1.5 rounded hover:bg-xcord-brand/80 transition-colors text-sm"
          onClick={() => setShowUpload(true)}
          aria-label="Upload sound"
        >
          + Upload Sound
        </button>
      </div>

      {/* Upload form */}
      <Show when={showUpload()}>
        <div class="px-4 py-4 bg-xcord-bg-primary border-b border-xcord-bg-tertiary space-y-3 flex-shrink-0">
          <h3 class="text-xcord-text-primary font-semibold text-sm">Upload Custom Sound</h3>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Sound Name *
            </label>
            <input
              type="text"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
              placeholder="e.g. Airhorn"
              value={uploadName()}
              onInput={(e) => setUploadName(e.currentTarget.value)}
            />
          </div>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Audio File *
            </label>
            <input
              ref={fileInputRef}
              type="file"
              accept="audio/*"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-secondary rounded px-3 py-2 text-sm outline-none"
              onChange={handleFileChange}
              aria-label="Select audio file"
            />
            <Show when={uploadFile()}>
              {(file) => (
                <p class="text-xcord-text-muted text-xs mt-1">{file().name}</p>
              )}
            </Show>
          </div>

          <Show when={uploadError()}>
            <p class="text-red-400 text-xs">{uploadError()}</p>
          </Show>

          <div class="flex gap-2 pt-1">
            <button
              class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleUpload}
              disabled={isUploading()}
              aria-label="Submit sound upload"
            >
              {isUploading() ? 'Uploading...' : 'Upload'}
            </button>
            <button
              class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
              onClick={cancelUpload}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Global error */}
      <Show when={error()}>
        <div class="px-4 py-2">
          <p class="text-red-400 text-sm">{error()}</p>
        </div>
      </Show>

      {/* Sound grid */}
      <div class="flex-1 overflow-y-auto p-4">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!isLoading() && sounds().length === 0}>
          <div class="flex flex-col items-center justify-center h-48 space-y-3">
            <div class="text-4xl text-xcord-text-muted">🔊</div>
            <p class="text-xcord-text-muted text-sm">No sounds yet</p>
            <button
              class="text-xcord-brand hover:underline text-sm"
              onClick={() => setShowUpload(true)}
            >
              Upload a sound
            </button>
          </div>
        </Show>

        <Show when={!isLoading() && sounds().length > 0}>
          <div class="grid grid-cols-3 gap-3" role="list" aria-label="Sound buttons">
            <For each={sounds()}>
              {(sound) => (
                <button
                  class={`flex flex-col items-center gap-2 p-3 rounded-lg border-2 transition-all text-center ${
                    playingId() === sound.id
                      ? 'border-xcord-brand bg-xcord-brand/10 text-xcord-brand'
                      : 'border-xcord-bg-tertiary bg-xcord-bg-primary text-xcord-text-primary hover:border-xcord-brand/50 hover:bg-xcord-bg-primary/80'
                  }`}
                  onClick={() => playSound(sound.id)}
                  aria-label={`Play ${sound.name}`}
                  role="listitem"
                >
                  <span class="text-2xl" aria-hidden="true">
                    {playingId() === sound.id ? '▶' : '🔊'}
                  </span>
                  <span class="text-xs font-medium leading-tight w-full truncate">
                    {sound.name}
                  </span>
                  <span class="text-xcord-text-muted text-xs">
                    {formatSoundDuration(sound.durationMs)}
                  </span>
                </button>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}

