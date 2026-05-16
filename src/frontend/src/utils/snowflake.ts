/**
 * Normalizes Snowflake ID fields on an object to strings.
 *
 * SOURCE OF TRUTH — this file is shared by both web (xcord-fed) and mobile
 * (xcord-mobile). The mobile app gitignores its synced `src/app/` tree and
 * pulls this file from xcord-fed via `xcord-mobile/scripts/sync-source.sh`.
 * Other repos that need the same utility must also import it from xcord-fed
 * (or be added to the mobile-style sync). Do NOT fork.
 *
 * The backend may serialize Snowflake IDs as either JSON strings (when the
 * global `SnowflakeJsonConverter` is in effect) or numbers (when raw payloads
 * come in over SignalR before the converter runs). The frontend models them
 * uniformly as strings, so any field listed in `keys` is forced to a string
 * via `String(value)` when it is non-null.
 *
 * The signature is intentionally tolerant: callers can pass any object shape
 * `T` and the listed keys will retain their original *static* type, so:
 *
 * ```ts
 * const s = normalizeIds(server, 'id', 'ownerId');  // s: Server
 * ```
 *
 * No `as unknown as` chain is needed. Keys not present in `T` (or whose value
 * is null/undefined) are silently skipped.
 */
export function normalizeIds<T extends object>(obj: T, ...keys: readonly (keyof T & string)[]): T {
  const result: Record<string, unknown> = { ...(obj as Record<string, unknown>) };
  for (const key of keys) {
    const value = result[key];
    if (value != null) {
      result[key] = String(value);
    }
  }
  return result as T;
}
