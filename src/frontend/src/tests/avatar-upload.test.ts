import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { validateAvatarFile } from '../components/AvatarUpload';

/**
 * User Avatar Upload tests — Card 172
 */

// ---- Types ----

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

interface UserProfile {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
}

// ---- Upload flow helper (test infrastructure) ----

async function performAvatarUpload(
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

function makeMockXhrFactory(onCall?: (url: string) => void) {
  return async (uploadUrl: string): Promise<void> => {
    onCall?.(uploadUrl);
  };
}

// ---- Tests ----

describe('avatar-upload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.setAuthenticated(true);
  });

  describe('file type validation', () => {
    it('rejects non-image files', () => {
      const file = { type: 'video/mp4', size: 1024 };
      const result = validateAvatarFile(file);
      expect(result).toBe('Only image files are allowed.');
    });

    it('rejects files larger than 8 MB', () => {
      const file = { type: 'image/jpeg', size: 8 * 1024 * 1024 + 1 };
      const result = validateAvatarFile(file);
      expect(result).toBe('File size must be 8 MB or less.');
    });

    it('accepts valid JPEG files', () => {
      const file = { type: 'image/jpeg', size: 2 * 1024 * 1024 };
      const result = validateAvatarFile(file);
      expect(result).toBeNull();
    });

    it('accepts GIF files', () => {
      const file = { type: 'image/gif', size: 500 * 1024 };
      const result = validateAvatarFile(file);
      expect(result).toBeNull();
    });
  });

  describe('API calls', () => {
    it('calls POST /api/v1/uploads with correct file metadata', async () => {
      const file = { name: 'avatar.png', type: 'image/png', size: 128000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-av-1', uploadUrl: 'https://s3.example.com/av-1' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: 'https://cdn.example.com/avatar.png' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ userId: 'u-1', username: 'alice', displayName: 'Alice', avatarUrl: 'https://cdn.example.com/avatar.png' }),
        });

      await performAvatarUpload(file, makeMockXhrFactory());

      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        1,
        '/api/v1/uploads',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ fileName: 'avatar.png', contentType: 'image/png', fileSize: 128000 }),
        }),
      );
    });

    it('calls PUT /api/v1/users/@me with the confirmed avatar URL', async () => {
      const confirmedUrl = 'https://cdn.example.com/my-avatar.jpg';
      const file = { name: 'me.jpg', type: 'image/jpeg', size: 64000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-av-2', uploadUrl: 'https://s3.example.com/av-2' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: confirmedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ userId: 'u-1', username: 'alice', displayName: 'Alice', avatarUrl: confirmedUrl }),
        });

      await performAvatarUpload(file, makeMockXhrFactory());

      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        3,
        '/api/v1/users/@me',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ avatarUrl: confirmedUrl }),
        }),
      );
    });

    it('calls POST /api/v1/attachments/{id}/confirm after XHR upload', async () => {
      const attachmentId = 'att-av-confirm';
      const file = { name: 'photo.png', type: 'image/png', size: 32000 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl: 'https://s3.example.com/photo' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: 'https://cdn.example.com/photo.png' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ userId: 'u-1', username: 'bob', displayName: 'Bob' }),
        });

      await performAvatarUpload(file, makeMockXhrFactory());

      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        2,
        `/api/v1/attachments/${attachmentId}/confirm`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('returns the confirmed avatar URL', async () => {
      const confirmedUrl = 'https://cdn.example.com/confirmed-avatar.png';
      const file = { name: 'av.png', type: 'image/png', size: 8192 };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'att-final', uploadUrl: 'https://s3.example.com/final' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ url: confirmedUrl }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ userId: 'u-2', username: 'carol', displayName: 'Carol', avatarUrl: confirmedUrl }),
        });

      const result = await performAvatarUpload(file, makeMockXhrFactory());

      expect(result).toBe(confirmedUrl);
    });

    it('throws when upload initiation fails', async () => {
      const file = { name: 'fail.png', type: 'image/png', size: 1024 };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Quota exceeded' }),
      });

      await expect(performAvatarUpload(file, makeMockXhrFactory())).rejects.toMatchObject({
        error: 'Quota exceeded',
      });
    });
  });
});
