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

export function useFileUpload() {
  const toasts = useToasts();
  const [uploading, setUploading] = createSignal(false);
  const [uploadProgress, setUploadProgress] = createSignal(0);
  const [uploadedAttachment, setUploadedAttachment] = createSignal<UploadedAttachment | null>(null);

  const handleFileSelect = async (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    input.value = '';

    setUploading(true);
    setUploadProgress(0);

    try {
      const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
        fileName: file.name,
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

      setUploadedAttachment({ attachmentId, fileName: file.name, fileSize: file.size });
      setUploadProgress(100);
    } catch (error) {
      console.error('Failed to upload file:', error);
      setUploadedAttachment(null);
      toasts.error(uploadErrorMessage(error));
    } finally {
      setUploading(false);
    }
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
    removeAttachment,
  };
}
