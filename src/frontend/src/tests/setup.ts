import '@testing-library/jest-dom/vitest';
import { afterEach, vi } from 'vitest';
import { cleanup } from '@solidjs/testing-library';

// jsdom polyfills for browser APIs that components occasionally read at import/render time.
if (typeof window !== 'undefined' && !window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

const unmockedFetch = async (): Promise<Response> => {
  throw new Error('fetch was called without being mocked - stub it with mockFetch() in your test');
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  // Re-arm fetch with the throwing implementation so any in-flight requests
  // that resolve AFTER teardown still reject loudly instead of returning
  // undefined (which would surface as TypeError unhandled rejections).
  globalThis.fetch = vi.fn(unmockedFetch) as typeof globalThis.fetch;
});

globalThis.fetch = vi.fn(unmockedFetch) as typeof globalThis.fetch;
