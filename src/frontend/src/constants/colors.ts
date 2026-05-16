/**
 * Shared color constants for the federation UI.
 *
 * Centralized here so palette tweaks touch one place rather than the many
 * components that previously inlined the hex codes. See kanban #122.
 */

/** Default group/role color when none is set (matches `--color-xcord-brand`). */
export const DEFAULT_GROUP_COLOR = '#d4943a';

/**
 * Preset palette offered in the role/group color picker. The order is
 * intentional - first row is brand-adjacent, subsequent rows widen the gamut.
 */
export const GROUP_COLOR_PALETTE: readonly string[] = [
  '#d4943a', '#3ba55d', '#f0b232', '#e06a8a', '#ed4245',
  '#3a8fd4', '#2ecc71', '#c47a2e', '#8a5db8', '#1abc9c',
  '#cf5050', '#e0a44a', '#8a8ea0', '#ffffff', '#000000',
] as const;
