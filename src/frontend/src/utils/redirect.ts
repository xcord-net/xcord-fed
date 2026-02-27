/**
 * Validates that a redirect URL is a safe relative path.
 * Returns the path if valid, or the fallback ('/channels/me') otherwise.
 *
 * A valid redirect must:
 *  - start with '/'
 *  - not contain '://' (prevents absolute URLs like https://evil.com)
 */
export function sanitizeRedirect(redirect: string | null | undefined, fallback = '/channels/me'): string {
  if (!redirect) return fallback;
  if (!redirect.startsWith('/')) return fallback;
  if (redirect.includes('://')) return fallback;
  return redirect;
}
