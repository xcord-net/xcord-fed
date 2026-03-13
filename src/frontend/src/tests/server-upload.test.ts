import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { validateServerImageFile } from '../components/ServerIconUpload';

/**
 * Server Icon and Banner Upload tests - Card 171
 *
 * The ServerIconUpload component handles a three-step flow:
 *   1. POST /api/v1/uploads  → get presigned URL + attachmentId
 *   2. PUT presigned URL     → upload file bytes
 *   3. POST /api/v1/attachments/{id}/confirm → confirm upload
 *   4. PUT /api/v1/servers/{id} → update server with new iconUrl / bannerUrl
 *
 * These tests validate the upload flow logic, file validation, and API shapes.
 */

// ---- Types ----

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface Server {
  id: string;
  name: string;
  iconUrl?: string;
  bannerUrl?: string;
  ownerId: string;
  createdAt: string;
}

// ---- Upload flow test infrastructure (mirrors component logic) ----

async function performServerUpload(
  serverId: string,
  file: { name: string; type: string; size: number },
  target: 'icon' | 'banner',
  xhrFactory: (uploadUrl: string) => Promise<void>,
): Promise<{ iconUrl?: string; bannerUrl?: string }> {
  // Step 1: initiate
  const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
    fileName: file.name,
    contentType: file.type,
    fileSize: file.size,
  });

  // Step 2: PUT presigned URL
  await xhrFactory(uploadUrl);

  // Step 3: confirm
  const confirmed = await api.post<{ url: string }>(`/api/v1/attachments/${attachmentId}/confirm`, {});
  const url = confirmed.url ?? uploadUrl;

  // Step 4: update server
  const updates = target === 'icon' ? { iconUrl: url } : { bannerUrl: url };
  await api.put<Server>(`/api/v1/servers/${serverId}`, updates);

  return updates;
}

function makeMockXhrFactory(onCall?: (url: string) => void) {
  return async (uploadUrl: string): Promise<void> => {
    onCall?.(uploadUrl);
  };
}

// ---- Fixtures ----

const SERVER: Server = {
  id: 'srv-abc',
  name: 'My Server',
  ownerId: 'user-1',
  createdAt: '2024-01-01T00:00:00Z',
};

// ---- Tests ----

describe('server-upload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.setAuthenticated(true);
  });

  describe('file type validation', () => {
    it('rejects non-image files', () => {
      // Arrange
      const file = { type: 'application/pdf', size: 1024 };

      // Act
      const result = validateServerImageFile(file);

      // Assert
      expect(result).toBe('Only image files are allowed.');
    });

    it('rejects files larger than 8 MB', () => {
      // Arrange
      const file = { type: 'image/png', size: 9 * 1024 * 1024 };

      // Act
      const result = validateServerImageFile(file);

      // Assert
      expect(result).toBe('File size must be 8 MB or less.');
    });

    it('accepts valid image files under 8 MB', () => {
      // Arrange
      const file = { type: 'image/jpeg', size: 2 * 1024 * 1024 };

      // Act
      const result = validateServerImageFile(file);

      // Assert
      expect(result).toBeNull();
    });

    it('accepts exactly 8 MB files', () => {
      // Arrange
      const file = { type: 'image/png', size: 8 * 1024 * 1024 };

      // Act
      const result = validateServerImageFile(file);

      // Assert
      expect(result).toBeNull();
    });
  });

  describe('upload flow', () => {
    it('calls POST /api/v1/uploads with correct payload for icon upload', async () => {
      // Arrange
      const file = { name: 'icon.png', type: 'image/png', size: 50000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({
            attachmentId: 'att-1',
            uploadUrl: 'https://storage.example.com/att-1',
          } satisfies UploadInitResponse),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: 'https://cdn.example.com/icon.png' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ ...SERVER, iconUrl: 'https://cdn.example.com/icon.png' }),
        });

      // Act
      await performServerUpload(SERVER.id, file, 'icon', makeMockXhrFactory());

      // Assert - first fetch is the initiate request
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        1,
        '/api/v1/uploads',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ fileName: 'icon.png', contentType: 'image/png', fileSize: 50000 }),
        }),
      );
    });

    it('calls POST /api/v1/attachments/{id}/confirm after XHR upload', async () => {
      // Arrange
      const attachmentId = 'att-confirm-icon';
      const file = { name: 'banner.jpg', type: 'image/jpeg', size: 200000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl: 'https://s3.example.com/att' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: 'https://cdn.example.com/banner.jpg' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ ...SERVER, bannerUrl: 'https://cdn.example.com/banner.jpg' }),
        });

      // Act
      await performServerUpload(SERVER.id, file, 'banner', makeMockXhrFactory());

      // Assert
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        2,
        `/api/v1/attachments/${attachmentId}/confirm`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('calls PUT /api/v1/servers/{id} with iconUrl after icon upload', async () => {
      // Arrange
      const confirmedUrl = 'https://cdn.example.com/my-icon.png';
      const file = { name: 'my-icon.png', type: 'image/png', size: 30000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-icon', uploadUrl: 'https://s3.example.com/att-icon' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: confirmedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ ...SERVER, iconUrl: confirmedUrl }),
        });

      // Act
      await performServerUpload(SERVER.id, file, 'icon', makeMockXhrFactory());

      // Assert - third fetch is the server update
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        3,
        `/api/v1/servers/${SERVER.id}`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ iconUrl: confirmedUrl }),
        }),
      );
    });

    it('calls PUT /api/v1/servers/{id} with bannerUrl after banner upload', async () => {
      // Arrange
      const confirmedUrl = 'https://cdn.example.com/my-banner.jpg';
      const file = { name: 'banner.jpg', type: 'image/jpeg', size: 400000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-banner', uploadUrl: 'https://s3.example.com/att-b' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: confirmedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ ...SERVER, bannerUrl: confirmedUrl }),
        });

      // Act
      await performServerUpload(SERVER.id, file, 'banner', makeMockXhrFactory());

      // Assert
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        3,
        `/api/v1/servers/${SERVER.id}`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ bannerUrl: confirmedUrl }),
        }),
      );
    });

    it('passes the presigned URL to the XHR upload factory', async () => {
      // Arrange
      const presignedUrl = 'https://minio.example.com/bucket/icon?sig=xyz';
      const file = { name: 'icon.png', type: 'image/png', size: 10000 };
      const xhrUrls: string[] = [];

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-x', uploadUrl: presignedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: presignedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ ...SERVER }),
        });

      // Act
      await performServerUpload(SERVER.id, file, 'icon', makeMockXhrFactory((u) => xhrUrls.push(u)));

      // Assert
      expect(xhrUrls).toHaveLength(1);
      expect(xhrUrls[0]).toBe(presignedUrl);
    });

    it('throws when the initiate request fails', async () => {
      // Scenario: the backend upload initiation endpoint returns an error (e.g. storage unavailable).
      // The upload flow must reject with the API error so callers (including the component's catch
      // block) can surface a meaningful message to the user.
      const file = { name: 'fail.png', type: 'image/png', size: 1024 };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Storage unavailable' }),
      });

      // Act & Assert - the rejection must carry a non-empty error string so the UI can display it
      await expect(
        performServerUpload(SERVER.id, file, 'icon', makeMockXhrFactory()),
      ).rejects.toMatchObject({ error: expect.stringMatching(/.+/) });

      // Verify the presigned-URL step was never reached (no second fetch call)
      expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    });
  });

});
