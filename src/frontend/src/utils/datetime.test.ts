import { describe, it, expect } from 'vitest';
import { formatTime, formatDate, formatDateTime } from './datetime';

// Use a fixed instant. Assertions avoid locale-specific exact strings (CI locale
// is not guaranteed) and instead check the meaningful, stable properties.
const iso = '2026-06-12T15:01:00Z';

describe('formatTime', () => {
  it('formats a timestamp to a non-empty hour:minute string', () => {
    const out = formatTime(iso);
    expect(out).not.toBe('');
    expect(out).toMatch(/\d{1,2}[:.]\d{2}/);
  });

  it('returns an empty string for invalid input instead of "Invalid Date"', () => {
    expect(formatTime('not-a-date')).toBe('');
    expect(formatTime('')).toBe('');
  });
});

describe('formatDate', () => {
  it('includes the year for a valid date', () => {
    expect(formatDate(iso)).toContain('2026');
  });

  it('returns an empty string for invalid input', () => {
    expect(formatDate('garbage')).toBe('');
  });
});

describe('formatDateTime', () => {
  it('includes both the date (year) and a time component', () => {
    const out = formatDateTime(iso);
    expect(out).toContain('2026');
    expect(out).toMatch(/\d{1,2}[:.]\d{2}/);
  });

  it('returns an empty string for invalid input', () => {
    expect(formatDateTime('nope')).toBe('');
  });

  it('accepts Date and epoch-number inputs', () => {
    expect(formatDate(new Date(iso))).toContain('2026');
    expect(formatDate(Date.parse(iso))).toContain('2026');
  });
});
