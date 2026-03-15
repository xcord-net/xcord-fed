/**
 * Normalizes Snowflake ID fields on an object to strings.
 * Backend may return IDs as numbers; frontend expects strings.
 */
export function normalizeIds<T extends Record<string, unknown>>(
  obj: T,
  ...keys: (keyof T)[]
): T {
  const result = { ...obj };
  for (const key of keys) {
    if (result[key] != null) {
      (result as Record<string, unknown>)[key as string] = String(result[key]);
    }
  }
  return result;
}
