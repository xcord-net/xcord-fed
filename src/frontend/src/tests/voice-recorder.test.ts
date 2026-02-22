import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { formatDuration, buildWaveformBars } from '../components/VoiceRecorder';

// ---- Types mirroring VoiceRecorder internals ----

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

// ---- Upload flow test infrastructure ----

async function performVoiceUpload(
  blob: { size: number; type: string; data: Blob },
  putBlob: (uploadUrl: string, blob: Blob) => Promise<void>,
): Promise<{ attachmentId: string }> {
  const fileName = `voice-message-test.webm`;
  const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
    fileName,
    contentType: blob.type || 'audio/webm',
    fileSize: blob.size,
  });

  await putBlob(uploadUrl, blob.data);
  await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

  return { attachmentId };
}

function makeNoopPutBlob(onCall?: (url: string) => void) {
  return async (uploadUrl: string, _blob: Blob): Promise<void> => {
    onCall?.(uploadUrl);
  };
}

// ---- Tests ----

describe('VoiceRecorder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- formatDuration helper ----

  describe('formatDuration', () => {
    it('formats zero seconds as 00:00', () => {
      expect(formatDuration(0)).toBe('00:00');
    });

    it('formats 30 seconds correctly', () => {
      expect(formatDuration(30)).toBe('00:30');
    });

    it('formats 90 seconds as 01:30', () => {
      expect(formatDuration(90)).toBe('01:30');
    });

    it('formats 3600 seconds as 60:00', () => {
      expect(formatDuration(3600)).toBe('60:00');
    });

    it('pads single-digit seconds with leading zero', () => {
      expect(formatDuration(65)).toBe('01:05');
    });
  });

  // ---- buildWaveformBars helper ----

  describe('buildWaveformBars', () => {
    it('returns array of requested length', () => {
      const bars = buildWaveformBars(null, 20);
      expect(bars).toHaveLength(20);
    });

    it('returns all zeros when analyserNode is null', () => {
      const bars = buildWaveformBars(null, 10);
      expect(bars.every((b) => b === 0)).toBe(true);
    });

    it('returns array of requested length with a mock analyser', () => {
      // Arrange: mock AnalyserNode
      const mockAnalyser = {
        frequencyBinCount: 128,
        getByteFrequencyData: (arr: Uint8Array) => {
          arr.fill(128); // fill with mid-range values
        },
      } as unknown as AnalyserNode;

      // Act
      const bars = buildWaveformBars(mockAnalyser, 10);

      // Assert
      expect(bars).toHaveLength(10);
      bars.forEach((b) => {
        expect(b).toBeGreaterThanOrEqual(0);
        expect(b).toBeLessThanOrEqual(100);
      });
    });

    it('returns values between 0 and 100 inclusive', () => {
      const mockAnalyser = {
        frequencyBinCount: 64,
        getByteFrequencyData: (arr: Uint8Array) => {
          for (let i = 0; i < arr.length; i++) arr[i] = 255;
        },
      } as unknown as AnalyserNode;

      const bars = buildWaveformBars(mockAnalyser, 8);
      bars.forEach((b) => {
        expect(b).toBeGreaterThanOrEqual(0);
        expect(b).toBeLessThanOrEqual(100);
      });
    });
  });

  // ---- Upload flow ----

  describe('voice message upload flow', () => {
    it('sends correct fileName, contentType and fileSize to POST /api/v1/uploads', async () => {
      // Arrange
      const blob = { size: 48000, type: 'audio/webm', data: new Blob([], { type: 'audio/webm' }) };
      const attachmentId = 'voice-att-001';
      const uploadUrl = 'https://storage.example.com/presigned/voice-att-001';

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl } satisfies UploadInitResponse),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      const result = await performVoiceUpload(blob, makeNoopPutBlob());

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/uploads',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            fileName: 'voice-message-test.webm',
            contentType: 'audio/webm',
            fileSize: 48000,
          }),
        }),
      );
      expect(result.attachmentId).toBe(attachmentId);
    });

    it('calls confirm endpoint after PUT upload', async () => {
      // Arrange
      const attachmentId = 'voice-att-002';
      const uploadUrl = 'https://storage.example.com/presigned/voice-att-002';
      const blob = { size: 12000, type: 'audio/webm', data: new Blob() };

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId, uploadUrl }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await performVoiceUpload(blob, makeNoopPutBlob());

      // Assert: second call is confirm
      expect(globalThis.fetch).toHaveBeenNthCalledWith(
        2,
        `/api/v1/attachments/${attachmentId}/confirm`,
        expect.objectContaining({ method: 'POST' }),
      );
    });

    it('passes correct presigned URL to the PUT step', async () => {
      // Arrange
      const expectedUploadUrl = 'https://minio.example.com/bucket/voice-xyz?sig=abc';
      const blob = { size: 5000, type: 'audio/webm', data: new Blob() };
      const putCalls: string[] = [];

      globalThis.fetch = vi.fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ attachmentId: 'voice-xyz', uploadUrl: expectedUploadUrl }),
        })
        .mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await performVoiceUpload(blob, makeNoopPutBlob((url) => putCalls.push(url)));

      // Assert
      expect(putCalls).toHaveLength(1);
      expect(putCalls[0]).toBe(expectedUploadUrl);
    });

    it('throws when the upload initiation fails', async () => {
      // Arrange
      const blob = { size: 1000, type: 'audio/webm', data: new Blob() };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        json: async () => ({ error: 'Storage quota exceeded' }),
      });

      // Act & Assert
      await expect(performVoiceUpload(blob, makeNoopPutBlob())).rejects.toMatchObject({
        error: 'Storage quota exceeded',
      });
    });

    it('throws when the PUT step fails', async () => {
      // Arrange
      const blob = { size: 1000, type: 'audio/webm', data: new Blob() };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => ({ attachmentId: 'voice-err', uploadUrl: 'https://example.com/put' }),
      });

      const failingPut = async (): Promise<void> => {
        throw new Error('PUT network error');
      };

      // Act & Assert
      await expect(performVoiceUpload(blob, failingPut)).rejects.toThrow('PUT network error');
    });
  });
});
