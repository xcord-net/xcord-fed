// Mock global fetch if needed
globalThis.fetch = vi.fn() as typeof globalThis.fetch;

// Reset mocks after each test
afterEach(() => {
  vi.clearAllMocks();
});
