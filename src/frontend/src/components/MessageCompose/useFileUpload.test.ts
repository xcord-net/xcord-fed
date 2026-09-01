import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createRoot } from 'solid-js';

const apiPost = vi.fn();
vi.mock('../../api/client', () => ({
  api: { post: (...args: unknown[]) => apiPost(...args) },
}));

const toastError = vi.fn();
vi.mock('../../stores/toast.store', () => ({
  useToasts: () => ({ error: toastError, success: vi.fn(), info: vi.fn() }),
}));

import { uploadErrorMessage, clipboardFile, useFileUpload } from './useFileUpload';

describe('uploadErrorMessage', () => {
  it('maps a 413 / too-large failure to a size message', () => {
    expect(uploadErrorMessage(new Error('Upload failed with status 413'))).toMatch(/too large/i);
    expect(uploadErrorMessage(new Error('file too large'))).toMatch(/too large/i);
  });

  it('maps a 415 / unsupported-type failure to a type message', () => {
    expect(uploadErrorMessage(new Error('Upload failed with status 415'))).toMatch(/not supported/i);
    expect(uploadErrorMessage(new Error('unsupported content type'))).toMatch(/not supported/i);
  });

  it('maps a network failure to a connection message', () => {
    expect(uploadErrorMessage(new Error('Upload network error'))).toMatch(/connection/i);
  });

  it('falls back to a generic message for unknown failures', () => {
    expect(uploadErrorMessage(new Error('Upload failed with status 500'))).toBe('Upload failed. Please try again.');
    expect(uploadErrorMessage('weird non-error value')).toBe('Upload failed. Please try again.');
  });
});

function imageFile(name = 'image.png') {
  return new File([new Uint8Array([1, 2, 3])], name, { type: 'image/png' });
}

/** Minimal stand-in for the DataTransfer a paste event carries. */
function clipboard(opts: {
  files?: File[];
  items?: Array<{ kind: string; type: string; file?: File }>;
}): DataTransfer {
  return {
    files: (opts.files ?? []) as unknown as FileList,
    items: (opts.items ?? []).map((i) => ({
      kind: i.kind,
      type: i.type,
      getAsFile: () => i.file ?? null,
    })) as unknown as DataTransferItemList,
  } as DataTransfer;
}

describe('clipboardFile', () => {
  it('returns the first file from the clipboard file list', () => {
    const file = imageFile();
    expect(clipboardFile(clipboard({ files: [file] }))).toBe(file);
  });

  it('falls back to the clipboard items when files is empty', () => {
    const file = imageFile();
    expect(clipboardFile(clipboard({ items: [{ kind: 'file', type: 'image/png', file }] }))).toBe(file);
  });

  it('ignores non-file items so a plain text paste is left alone', () => {
    expect(clipboardFile(clipboard({ items: [{ kind: 'string', type: 'text/plain' }] }))).toBeNull();
  });

  it('returns null for missing clipboard data', () => {
    expect(clipboardFile(null)).toBeNull();
  });
});

describe('useFileUpload paste handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiPost.mockResolvedValue({ attachmentId: '42', uploadUrl: 'https://uploads.test/42' });
    // Stub the PUT so the upload resolves without touching the network.
    class StubXhr {
      upload = { addEventListener: vi.fn() };
      status = 200;
      private handlers: Record<string, () => void> = {};
      addEventListener(event: string, cb: () => void) { this.handlers[event] = cb; }
      open() { /* no-op */ }
      setRequestHeader() { /* no-op */ }
      withCredentials = false;
      send() { this.handlers.load?.(); }
    }
    vi.stubGlobal('XMLHttpRequest', StubXhr);
  });

  function pasteEvent(data: DataTransfer | null) {
    const preventDefault = vi.fn();
    return {
      event: { clipboardData: data, preventDefault } as unknown as ClipboardEvent,
      preventDefault,
    };
  }

  it('uploads an image pasted into the composer', async () => {
    await createRoot(async (dispose) => {
      const upload = useFileUpload();
      const { event, preventDefault } = pasteEvent(clipboard({ files: [imageFile('shot.png')] }));

      await upload.handlePaste(event);

      expect(preventDefault).toHaveBeenCalled();
      expect(apiPost).toHaveBeenCalledWith('/api/v1/uploads', {
        fileName: 'shot.png',
        contentType: 'image/png',
        fileSize: 3,
      });
      expect(upload.uploadedAttachment()).toEqual({
        attachmentId: '42',
        fileName: 'shot.png',
        fileSize: 3,
      });
      dispose();
    });
  });

  it('names an unnamed pasted image so it is identifiable', async () => {
    await createRoot(async (dispose) => {
      const upload = useFileUpload();
      const unnamed = new File([new Uint8Array([1])], '', { type: 'image/png' });

      await upload.handlePaste(pasteEvent(clipboard({ files: [unnamed] })).event);

      expect(upload.uploadedAttachment()?.fileName).toMatch(/^pasted-.+\.png$/);
      dispose();
    });
  });

  it('leaves a text-only paste to the browser', async () => {
    await createRoot(async (dispose) => {
      const upload = useFileUpload();
      const { event, preventDefault } = pasteEvent(clipboard({ items: [{ kind: 'string', type: 'text/plain' }] }));

      await upload.handlePaste(event);

      expect(preventDefault).not.toHaveBeenCalled();
      expect(apiPost).not.toHaveBeenCalled();
      dispose();
    });
  });

  it('refuses a second paste while an attachment is already staged', async () => {
    await createRoot(async (dispose) => {
      const upload = useFileUpload();
      await upload.handlePaste(pasteEvent(clipboard({ files: [imageFile('first.png')] })).event);
      apiPost.mockClear();

      await upload.handlePaste(pasteEvent(clipboard({ files: [imageFile('second.png')] })).event);

      expect(apiPost).not.toHaveBeenCalled();
      expect(toastError).toHaveBeenCalledWith(expect.stringMatching(/one attachment/i));
      expect(upload.uploadedAttachment()?.fileName).toBe('first.png');
      dispose();
    });
  });
});
