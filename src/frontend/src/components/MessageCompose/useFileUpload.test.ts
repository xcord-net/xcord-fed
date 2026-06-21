import { describe, it, expect } from 'vitest';
import { uploadErrorMessage } from './useFileUpload';

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
