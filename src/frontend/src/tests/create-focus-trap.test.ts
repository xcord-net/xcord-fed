import { describe, it, expect, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Extracted logic from createFocusTrap.ts — we test the pure algorithm, not
// the Solid hook lifecycle.
// ---------------------------------------------------------------------------

const FOCUSABLE_SELECTORS = [
  'a[href]',
  'button:not([disabled])',
  'textarea:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ');

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    (el) => !el.closest('[aria-hidden="true"]') && getComputedStyle(el).display !== 'none',
  );
}

/**
 * Simulates the Tab-wrap logic from createFocusTrap's keydown handler.
 * Returns the element that would receive focus, or null if the event wouldn't
 * be intercepted.
 */
function getTabWrapTarget(
  container: HTMLElement,
  activeElement: HTMLElement | null,
  shiftKey: boolean,
): HTMLElement | null {
  const focusable = getFocusableElements(container);
  if (focusable.length === 0) return null;

  const first = focusable[0];
  const last = focusable[focusable.length - 1];

  if (shiftKey) {
    // Shift+Tab: wrap backward from first → last
    if (activeElement === first || !container.contains(activeElement)) {
      return last;
    }
  } else {
    // Tab: wrap forward from last → first
    if (activeElement === last || !container.contains(activeElement)) {
      return first;
    }
  }

  return null; // browser default tab behaviour
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let container: HTMLDivElement;

function appendTo(parent: HTMLElement, tag: string, attrs: Record<string, string> = {}): HTMLElement {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    el.setAttribute(key, value);
  }
  parent.appendChild(el);
  return el;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('createFocusTrap — Tab wrap logic', () => {
  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    document.body.removeChild(container);
  });

  it('Tab from last element wraps to first', () => {
    // Arrange
    const btn1 = appendTo(container, 'button') as HTMLButtonElement;
    appendTo(container, 'button');
    const btn3 = appendTo(container, 'button') as HTMLButtonElement;

    // Act — active element is the last focusable, pressing Tab (no shift)
    const target = getTabWrapTarget(container, btn3, false);

    // Assert
    expect(target).toBe(btn1);
  });

  it('Tab when active element is outside container wraps to first', () => {
    // Arrange
    const btn1 = appendTo(container, 'button') as HTMLButtonElement;
    appendTo(container, 'button');
    const outsideBtn = document.createElement('button');
    document.body.appendChild(outsideBtn);

    // Act
    const target = getTabWrapTarget(container, outsideBtn, false);

    // Assert
    expect(target).toBe(btn1);

    // Cleanup
    document.body.removeChild(outsideBtn);
  });
});
