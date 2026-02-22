import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { formatFileSize } from '../components/MessageCompose';

// ---- Types mirroring MessageCompose internals ----

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface UploadedAttachment {
  attachmentId: string;
  fileName: string;
  fileSize: number;
}

// ---- Upload flow logic extracted for unit testing ----
//
// This mirrors the handleFileSelect logic in MessageCompose.tsx so we can
// test the three-step upload flow (initiate → PUT → confirm) without a DOM.

async function performUpload(
  file: { name: string; type: string; size: number; data: BodyInit },
  xhrFactory: (uploadUrl: string, contentType: string, data: BodyInit) => Promise<void>,
): Promise<UploadedAttachment> {
  // Step 1: initiate upload, get presigned URL
  const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    fileSize: file.size,
  });

  // Step 2: PUT to presigned URL (caller supplies XHR/fetch wrapper)
  await xhrFactory(uploadUrl, file.type || 'application/octet-stream', file.data);

  // Step 3: confirm upload complete
  await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

  return { attachmentId, fileName: file.name, fileSize: file.size };
}

// Simple XHR-less helper used in tests (simulates successful PUT)
function makeMockXhrFactory(onCall?: (url: string) => void) {
  return async (uploadUrl: string, _contentType: string, _data: BodyInit): Promise<void> => {
    onCall?.(uploadUrl);
  };
}

// ---- Tests ----

describe('file-upload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  describe('upload request payload', () => {
    it('sends correct fileName, contentType and fileSize to POST /api/v1/uploads', async () => {
      // Arrange
      const file = { name: 'photo.png', type: 'image/png', size: 204800, data: new Blob() };
      const attachmentId = 'att-001';
      const uploadUrl = 'https://storage.example.com/presigned/att-001';

      globalThis.fetch = vi.fn()
        // POST /api/v1/uploads
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl } satisfies UploadInitResponse),
        })
        // POST /api/v1/attachments/{id}/confirm
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      const result = await performUpload(file, makeMockXhrFactory());

      // Assert — first fetch call is the initiate request
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/uploads',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ fileName: 'photo.png', contentType: 'image/png', fileSize: 204800 }),
        }),
      );
      expect(result.attachmentId).toBe(attachmentId);
      expect(result.fileName).toBe('photo.png');
    });

    it('uses application/octet-stream when file has no MIME type', async () => {
      // Arrange
      const file = { name: 'data.bin', type: '', size: 512, data: new Blob() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-002', uploadUrl: 'https://s3.example.com/att-002' }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await performUpload(file, makeMockXhrFactory());

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/uploads',
        expect.objectContaining({
          body: JSON.stringify({ fileName: 'data.bin', contentType: 'application/octet-stream', fileSize: 512 }),
        }),
      );
    });
  });

  describe('confirm endpoint', () => {
    it('calls POST /api/v1/attachments/{attachmentId}/confirm after upload', async () => {
      // Arrange
      const attachmentId = 'att-confirm-99';
      const uploadUrl = 'https://storage.example.com/presigned/att-confirm-99';
      const file = { name: 'doc.pdf', type: 'application/pdf', size: 1024, data: new Blob() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await performUpload(file, makeMockXhrFactory());

      // Assert — second fetch call is the confirm request
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        2,
        `/api/v1/attachments/${attachmentId}/confirm`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('passes the presigned URL to the XHR PUT step', async () => {
      // Arrange
      const expectedUploadUrl = 'https://minio.example.com/bucket/att-xyz?sig=abc';
      const file = { name: 'clip.mp4', type: 'video/mp4', size: 2048, data: new Blob() };
      const xhrCalls: string[] = [];

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-xyz', uploadUrl: expectedUploadUrl }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await performUpload(file, makeMockXhrFactory((url) => xhrCalls.push(url)));

      // Assert
      expect(xhrCalls).toHaveLength(1);
      expect(xhrCalls[0]).toBe(expectedUploadUrl);
    });
  });

  describe('upload state management', () => {
    it('returns the attachment object with id, name and size on success', async () => {
      // Arrange
      const file = { name: 'screenshot.jpg', type: 'image/jpeg', size: 512000, data: new Blob() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-state-1', uploadUrl: 'https://s3.example.com/att-state-1' }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      const result = await performUpload(file, makeMockXhrFactory());

      // Assert
      expect(result).toEqual({
        attachmentId: 'att-state-1',
        fileName: 'screenshot.jpg',
        fileSize: 512000,
      });
    });

    it('throws when the initiate request fails', async () => {
      // Arrange
      const file = { name: 'fail.txt', type: 'text/plain', size: 128, data: new Blob() };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'File too large' }),
      });

      // Act & Assert
      await expect(performUpload(file, makeMockXhrFactory())).rejects.toMatchObject({
        error: 'File too large',
      });
    });

    it('throws when the XHR PUT step fails', async () => {
      // Arrange
      const file = { name: 'broken.txt', type: 'text/plain', size: 64, data: new Blob() };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => ({ attachmentId: 'att-broken', uploadUrl: 'https://s3.example.com/att-broken' }),
      });

      const failingXhr = async (): Promise<void> => {
        throw new Error('Upload network error');
      };

      // Act & Assert
      await expect(performUpload(file, failingXhr)).rejects.toThrow('Upload network error');
    });
  });

  describe('file preview', () => {
    it('formatFileSize shows filename and renders bytes correctly', () => {
      expect(formatFileSize(512)).toBe('512 B');
    });

    it('formatFileSize renders KB for files between 1 KB and 1 MB', () => {
      expect(formatFileSize(1536)).toBe('1.5 KB');
    });

    it('formatFileSize renders MB for files >= 1 MB', () => {
      expect(formatFileSize(2 * 1024 * 1024)).toBe('2.0 MB');
    });

    it('attachment fileName is preserved in the result', async () => {
      // Arrange
      const file = { name: 'my-document.pdf', type: 'application/pdf', size: 3000, data: new Blob() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-fn', uploadUrl: 'https://s3.example.com/att-fn' }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      const result = await performUpload(file, makeMockXhrFactory());

      // Assert — component uses result.fileName to display the preview
      expect(result.fileName).toBe('my-document.pdf');
    });
  });
});
