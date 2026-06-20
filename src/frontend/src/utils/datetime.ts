// Centralized date/time formatting so timestamps render consistently across the
// app. All helpers respect the user's locale (no hardcoded format) and tolerate
// invalid input by returning an empty string rather than "Invalid Date".

function parse(dateString: string | number | Date): Date | null {
  const d = dateString instanceof Date ? dateString : new Date(dateString);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** Time only, e.g. "3:01 PM" (locale-dependent). */
export function formatTime(dateString: string | number | Date): string {
  const d = parse(dateString);
  return d ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
}

/** Medium date, e.g. "Jun 12, 2026" (locale-dependent). */
export function formatDate(dateString: string | number | Date): string {
  const d = parse(dateString);
  return d ? d.toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' }) : '';
}

/** Date and time together, e.g. "Jun 12, 2026, 3:01 PM". */
export function formatDateTime(dateString: string | number | Date): string {
  const d = parse(dateString);
  return d
    ? d.toLocaleString([], { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
    : '';
}
