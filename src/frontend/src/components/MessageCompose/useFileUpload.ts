import { createSignal } from 'solid-js';
import { api } from '../../api/client';
import { useToasts } from '../../stores/toast.store';

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

export interface UploadedAttachment {
  attachmentId: string;
  fileName: string;
  fileSize: number;
}

/** Maps a raw upload error to a short, user-actionable message. */
export function uploadErrorMessage(error: unknown): string {
  const raw = error instanceof Error ? error.message : String(error);
  if (/status 413|too large/i.test(raw)) return 'That file is too large to upload.';
  if (/status 415|content type|unsupported/i.test(raw)) return 'That file type is not supported.';
  if (/network/i.test(raw)) return 'Upload failed - check your connection and try again.';
  return 'Upload failed. Please try again.';
}

/**
 * Pull the first file out of a paste event's clipboard payload. Screenshots and
 * images copied from another app arrive as a file entry; a plain text paste has
 * none, and must be left to the browser's default handling.
 */
export function clipboardFile(data: DataTransfer | null | undefined): File | null {
  if (!data) return null;

  const direct = data.files?.[0];
  if (direct) return direct;

  for (const item of Array.from(data.items ?? [])) {
    if (item.kind !== 'file') continue;
    const file = item.getAsFile();
    if (file) return file;
  }
  return null;
}

/**
 * Some browsers hand a pasted image over with an empty name. Give it a unique,
 * readable one so the attachment preview and the sent message aren't blank.
 */
function pastedFileName(file: File): string {
  if (file.name) return file.name;
  const ext = file.type.split('/')[1] || 'bin';
  return `pasted-${Date.now()}.${ext}`;
}

export function useFileUpload() {
  const toasts = useToasts();
  const [uploading, setUploading] = createSignal(false);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploadedAttachment, setUploadedAttachment] = createSignal<UploadedAttachment | null>(null);

  const uploadFile = async (file: File, fileName: string) => {
    setUploading(true);
    setUploadProgress(0);

    try {
      const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
        fileName,
        contentType: file.type || 'application/octet-stream',
        fileSize: file.size,
      });

      await new Promise<void>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.upload.addEventListener('progress', (ev) => {
          if (ev.lengthComputable) {
            setUploadProgress(Math.round((ev.loaded / ev.total) * 100));
          }
        });
        xhr.addEventListener('load', () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            resolve();
          } else {
            reject(new Error(`Upload failed with status ${xhr.status}`));
          }
        });
        xhr.addEventListener('error', () => reject(new Error('Upload network error')));
        xhr.open('PUT', uploadUrl);
        xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');
        xhr.withCredentials = true;
        xhr.send(file);
      });

      await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

      setUploadedAttachment({ attachmentId, fileName, fileSize: file.size });
      setUploadProgress(100);
    } catch (error) {
      console.error('Failed to upload file:', error);
      setUploadedAttachment(null);
      toasts.error(uploadErrorMessage(error));
    } finally {
      setUploading(false);
    }
  };

  const handleFileSelect = async (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    input.value = '';

    await uploadFile(file, file.name);
  };

  /** Paste an image (or any file) straight into the composer as an attachment. */
  const handlePaste = async (e: ClipboardEvent) => {
    const file = clipboardFile(e.clipboardData);
    // No file on the clipboard - it's a text paste, let the browser handle it.
    if (!file) return;

    e.preventDefault();

    if (uploading()) return;
    if (uploadedAttachment()) {
      toasts.error('Only one attachment per message. Send or remove the current one first.');
      return;
    }

    await uploadFile(file, pastedFileName(file));
  };

  const removeAttachment = () => {
    setUploadedAttachment(null);
    setUploadProgress(0);
  };

  return {
    uploading,
    uploadProgress,
    uploadedAttachment,
    setUploadedAttachment,
    handleFileSelect,
    handlePaste,
    removeAttachment,
  };
}
