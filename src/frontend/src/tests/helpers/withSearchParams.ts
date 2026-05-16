/**
 * Set `window.location.search` for the duration of a single test, then
 * restore the original URL + history state on completion.
 *
 * Components in this codebase read `window.location.search` at `onMount`
 * rather than going through `useLocation()`, so unit tests that need a
 * particular query string must mutate the real `window.history`. Doing this
 * inline is dangerous: a thrown assertion (or a missing `afterEach`) leaks
 * the URL into the next test, where it can flip behaviour silently.
 *
 * This helper:
 *  - snapshots `pathname + search + hash` and `history.state` before the test
 *  - pushes the requested URL
 *  - restores the snapshot in a `finally` block even when the callback throws
 *
 * Usage:
 *
 * ```ts
 * it('does the thing', async () => {
 *   await withSearchParams({ token: 'abc' }, async () => {
 *     const { findByTestId } = renderWithRouter(() => <ResetPassword />);
 *     ...
 *   });
 * });
 * ```
 *
 * Pass `null` (or no map) to render the page with no query string.
 */
export async function withSearchParams<T>(
  params: Record<string, string> | null,
  run: () => T | Promise<T>,
  options?: { pathname?: string },
): Promise<T> {
  const pathname = options?.pathname ?? window.location.pathname;
  const original = {
    url: window.location.pathname + window.location.search + window.location.hash,
    state: window.history.state,
  };

  const search = params
    ? '?' + new URLSearchParams(params).toString()
    : '';

  window.history.pushState({}, '', `${pathname}${search}`);

  try {
    return await run();
  } finally {
    window.history.replaceState(original.state, '', original.url);
  }
}
