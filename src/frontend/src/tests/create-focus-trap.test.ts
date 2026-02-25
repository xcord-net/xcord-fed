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

describe('createFocusTrap — getFocusableElements', () => {
  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    document.body.removeChild(container);
  });

  it('finds buttons, links with href, inputs, textareas, selects', () => {
    // Arrange
    appendTo(container, 'button');
    appendTo(container, 'a', { href: 'https://example.com' });
    appendTo(container, 'input');
    appendTo(container, 'textarea');
    appendTo(container, 'select');

    // Act
    const result = getFocusableElements(container);

    // Assert
    expect(result).toHaveLength(5);
    expect(result.map((el) => el.tagName.toLowerCase())).toEqual([
      'button',
      'a',
      'input',
      'textarea',
      'select',
    ]);
  });

  it('excludes disabled elements', () => {
    // Arrange
    appendTo(container, 'button'); // focusable
    appendTo(container, 'button', { disabled: '' }); // not focusable
    appendTo(container, 'input', { disabled: '' }); // not focusable
    appendTo(container, 'textarea', { disabled: '' }); // not focusable
    appendTo(container, 'select', { disabled: '' }); // not focusable

    // Act
    const result = getFocusableElements(container);

    // Assert
    expect(result).toHaveLength(1);
    expect(result[0].tagName.toLowerCase()).toBe('button');
  });

  it('excludes elements inside aria-hidden containers', () => {
    // Arrange
    appendTo(container, 'button'); // focusable
    const hidden = appendTo(container, 'div', { 'aria-hidden': 'true' });
    appendTo(hidden, 'button'); // inside aria-hidden — excluded
    appendTo(hidden, 'input'); // inside aria-hidden — excluded

    // Act
    const result = getFocusableElements(container);

    // Assert
    expect(result).toHaveLength(1);
  });

  it('finds elements with positive tabindex, excludes tabindex="-1"', () => {
    // Arrange
    appendTo(container, 'div', { tabindex: '0' }); // focusable
    appendTo(container, 'span', { tabindex: '5' }); // focusable
    appendTo(container, 'p', { tabindex: '-1' }); // excluded

    // Act
    const result = getFocusableElements(container);

    // Assert
    expect(result).toHaveLength(2);
    expect(result.map((el) => el.tagName.toLowerCase())).toEqual(['div', 'span']);
  });

  it('returns empty array when container has no focusable children', () => {
    // Arrange
    appendTo(container, 'div');
    appendTo(container, 'p');
    appendTo(container, 'span');

    // Act
    const result = getFocusableElements(container);

    // Assert
    expect(result).toHaveLength(0);
  });
});

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

  it('Shift+Tab from first element wraps to last', () => {
    // Arrange
    const btn1 = appendTo(container, 'button') as HTMLButtonElement;
    appendTo(container, 'button');
    const btn3 = appendTo(container, 'button') as HTMLButtonElement;

    // Act — active element is the first focusable, pressing Shift+Tab
    const target = getTabWrapTarget(container, btn1, true);

    // Assert
    expect(target).toBe(btn3);
  });

  it('Tab from a middle element does not wrap (returns null)', () => {
    // Arrange
    appendTo(container, 'button');
    const btn2 = appendTo(container, 'button') as HTMLButtonElement;
    appendTo(container, 'button');

    // Act
    const target = getTabWrapTarget(container, btn2, false);

    // Assert — no wrap needed, browser handles normal tab
    expect(target).toBeNull();
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

  it('Shift+Tab when active element is outside container wraps to last', () => {
    // Arrange
    appendTo(container, 'button');
    const btn2 = appendTo(container, 'button') as HTMLButtonElement;
    const outsideBtn = document.createElement('button');
    document.body.appendChild(outsideBtn);

    // Act
    const target = getTabWrapTarget(container, outsideBtn, true);

    // Assert
    expect(target).toBe(btn2);

    // Cleanup
    document.body.removeChild(outsideBtn);
  });

  it('returns null when container has no focusable elements', () => {
    // Arrange
    appendTo(container, 'div');

    // Act
    const forwardTarget = getTabWrapTarget(container, null, false);
    const backwardTarget = getTabWrapTarget(container, null, true);

    // Assert
    expect(forwardTarget).toBeNull();
    expect(backwardTarget).toBeNull();
  });
});
