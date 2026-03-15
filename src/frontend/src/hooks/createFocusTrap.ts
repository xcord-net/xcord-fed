import { onMount, onCleanup } from 'solid-js';

const FOCUSABLE_SELECTORS = [
  'a[href]',
  'button:not([disabled])',
  'textarea:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ');

/**
 * createFocusTrap - traps keyboard focus inside a container element while it
 * is mounted, restores focus to the previously active element on cleanup, and
 * calls `onEscape` when the Escape key is pressed.
 *
 * Usage:
 *   let containerRef!: HTMLDivElement;
 *   createFocusTrap(() => containerRef, { onEscape: () => props.onClose() });
 */
export function createFocusTrap(
  getContainer: () => HTMLElement | null | undefined,
  options?: { onEscape?: () => void }
) {
  onMount(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;

    const container = getContainer();
    if (container) {
      const focusable = getFocusableElements(container);
      if (focusable.length > 0) {
        focusable[0].focus();
      } else {
        // If no focusable children, make the container itself focusable
        container.setAttribute('tabindex', '-1');
        container.focus();
      }
    }

    const handleKeyDown = (e: KeyboardEvent) => {
      const el = getContainer();
      if (!el) return;

      if (e.key === 'Escape') {
        e.preventDefault();
        e.stopPropagation();
        options?.onEscape?.();
        return;
      }

      if (e.key !== 'Tab') return;

      const focusable = getFocusableElements(el);
      if (focusable.length === 0) {
        e.preventDefault();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement;

      if (e.shiftKey) {
        // Shift+Tab: wrap backward from first to last
        if (active === first || !el.contains(active)) {
          e.preventDefault();
          last.focus();
        }
      } else {
        // Tab: wrap forward from last to first
        if (active === last || !el.contains(active)) {
          e.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener('keydown', handleKeyDown);

    onCleanup(() => {
      document.removeEventListener('keydown', handleKeyDown);
      // Restore focus to the element that had it before the modal opened
      if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
        previouslyFocused.focus();
      }
    });
  });
}

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    (el) => !el.closest('[aria-hidden="true"]') && getComputedStyle(el).display !== 'none'
  );
}
