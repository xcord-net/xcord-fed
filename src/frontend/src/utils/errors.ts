export function getErrorMessage(err: unknown, fallback = 'An error occurred'): string {
  if (err instanceof Error) return err.message;
  const e = err as { detail?: string; message?: string; error?: string } | null;
  return e?.detail || e?.message || e?.error || fallback;
}
