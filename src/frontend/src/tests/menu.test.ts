import { describe, it, expect, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Extracted logic from Menu.tsx - we test the pure algorithms (getMenuItems,
// keyboard navigation index logic, positioning/clamping), not the Solid
// component lifecycle.
// ---------------------------------------------------------------------------

/** Returns all [role="menuitem"] elements within the given container that are
 *  not disabled and not display:none. */
function getMenuItems(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>('[role="menuitem"]')).filter(
    (el) => !el.hasAttribute('disabled') && getComputedStyle(el).display !== 'none',
  );
}

/**
 * Keyboard navigation index logic - given the current focused index and a key,
 * returns the new index that should receive focus.
 * Returns -1 if no navigation should occur.
 */
function getNextIndex(key: string, currentIndex: number, itemCount: number): number {
  if (itemCount === 0) return -1;

  switch (key) {
    case 'ArrowDown':
      return currentIndex < itemCount - 1 ? currentIndex + 1 : 0;
    case 'ArrowUp':
      return currentIndex > 0 ? currentIndex - 1 : itemCount - 1;
    case 'Home':
      return 0;
    case 'End':
      return itemCount - 1;
    default:
      return -1;
  }
}

/**
 * Positioning logic - clamp a menu to viewport, mirroring the clamping from
 * the component's context-menu positioning mode.
 */
function clampPosition(
  pos: { x: number; y: number },
  menuWidth: number,
  menuHeight: number,
  vw: number,
  vh: number,
  margin: number = 8,
): { top: number; left: number } {
  let top = pos.y;
  let left = pos.x;

  // Clamp right edge
  if (left + menuWidth > vw - margin) {
    left = vw - menuWidth - margin;
  }

  // Clamp bottom edge - flip above
  if (top + menuHeight > vh - margin) {
    top = pos.y - menuHeight;
  }

  // Ensure never off left/top
  left = Math.max(margin, left);
  top = Math.max(margin, top);

  return { top, left };
}

/**
 * Anchor-based positioning with flip logic.
 */
function anchorPosition(
  anchor: { top: number; bottom: number; left: number; right: number },
  menuWidth: number,
  menuHeight: number,
  vw: number,
  vh: number,
  placement: 'bottom-start' | 'bottom-end' | 'top-start' | 'top-end' = 'bottom-start',
  margin: number = 8,
): { top: number; left: number } {
  let top: number;
  let left: number;

  // Vertical
  if (placement.startsWith('bottom')) {
    top = anchor.bottom;
    if (anchor.bottom + menuHeight > vh - margin) {
      top = anchor.top - menuHeight;
    }
  } else {
    top = anchor.top - menuHeight;
    if (anchor.top - menuHeight < margin) {
      top = anchor.bottom;
    }
  }

  // Horizontal
  if (placement.endsWith('start')) {
    left = anchor.left;
    if (left + menuWidth > vw - margin) {
      left = anchor.right - menuWidth;
    }
  } else {
    left = anchor.right - menuWidth;
    if (left < margin) {
      left = anchor.left;
    }
  }

  // Final clamp
  left = Math.max(margin, left);
  top = Math.max(margin, top);

  return { top, left };
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let container: HTMLDivElement;

function appendMenuItem(
  parent: HTMLElement,
  text: string,
  attrs: Record<string, string> = {},
): HTMLElement {
  const el = document.createElement('button');
  el.setAttribute('role', 'menuitem');
  el.textContent = text;
  for (const [key, value] of Object.entries(attrs)) {
    el.setAttribute(key, value);
  }
  parent.appendChild(el);
  return el;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('Menu - getMenuItems', () => {
  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    document.body.removeChild(container);
  });

  it('finds all role="menuitem" elements', () => {
    // Arrange
    appendMenuItem(container, 'Item 1');
    appendMenuItem(container, 'Item 2');
    appendMenuItem(container, 'Item 3');

    // Act
    const items = getMenuItems(container);

    // Assert
    expect(items).toHaveLength(3);
  });

  it('excludes disabled menu items', () => {
    // Arrange
    appendMenuItem(container, 'Enabled');
    appendMenuItem(container, 'Disabled', { disabled: '' });

    // Act
    const items = getMenuItems(container);

    // Assert
    expect(items).toHaveLength(1);
    expect(items[0].textContent).toBe('Enabled');
  });

  it('excludes elements without role="menuitem"', () => {
    // Arrange
    appendMenuItem(container, 'Valid item');
    const divider = document.createElement('div');
    divider.setAttribute('role', 'separator');
    container.appendChild(divider);

    // Act
    const items = getMenuItems(container);

    // Assert
    expect(items).toHaveLength(1);
  });

  it('returns empty array when no menu items exist', () => {
    // Arrange - container with non-menuitem children
    const div = document.createElement('div');
    div.textContent = 'Not a menu item';
    container.appendChild(div);

    // Act
    const items = getMenuItems(container);

    // Assert
    expect(items).toHaveLength(0);
  });

  it('finds nested menu items', () => {
    // Arrange
    const group = document.createElement('div');
    container.appendChild(group);
    appendMenuItem(group, 'Nested 1');
    appendMenuItem(group, 'Nested 2');

    // Act
    const items = getMenuItems(container);

    // Assert
    expect(items).toHaveLength(2);
  });
});

describe('Menu - keyboard navigation index logic', () => {
  it('ArrowDown moves to next item', () => {
    expect(getNextIndex('ArrowDown', 0, 5)).toBe(1);
    expect(getNextIndex('ArrowDown', 2, 5)).toBe(3);
  });

  it('ArrowDown wraps from last to first', () => {
    expect(getNextIndex('ArrowDown', 4, 5)).toBe(0);
  });

  it('ArrowUp moves to previous item', () => {
    expect(getNextIndex('ArrowUp', 3, 5)).toBe(2);
    expect(getNextIndex('ArrowUp', 1, 5)).toBe(0);
  });

  it('ArrowUp wraps from first to last', () => {
    expect(getNextIndex('ArrowUp', 0, 5)).toBe(4);
  });

  it('Home moves to first item', () => {
    expect(getNextIndex('Home', 3, 5)).toBe(0);
    expect(getNextIndex('Home', 0, 5)).toBe(0);
  });

  it('End moves to last item', () => {
    expect(getNextIndex('End', 1, 5)).toBe(4);
    expect(getNextIndex('End', 4, 5)).toBe(4);
  });

  it('returns -1 for unrecognized keys', () => {
    expect(getNextIndex('Enter', 0, 5)).toBe(-1);
    expect(getNextIndex(' ', 2, 5)).toBe(-1);
    expect(getNextIndex('Escape', 0, 5)).toBe(-1);
    expect(getNextIndex('a', 1, 5)).toBe(-1);
  });

  it('returns -1 when item count is 0', () => {
    expect(getNextIndex('ArrowDown', -1, 0)).toBe(-1);
    expect(getNextIndex('ArrowUp', -1, 0)).toBe(-1);
    expect(getNextIndex('Home', -1, 0)).toBe(-1);
    expect(getNextIndex('End', -1, 0)).toBe(-1);
  });

  it('handles single item - ArrowDown wraps to itself', () => {
    expect(getNextIndex('ArrowDown', 0, 1)).toBe(0);
  });
});

describe('Menu - positioning (context-menu clamp)', () => {
  const vw = 1024;
  const vh = 768;
  const menuW = 200;
  const menuH = 300;

  it('positions at click coordinates when space available', () => {
    // Arrange
    const pos = { x: 100, y: 100 };

    // Act
    const result = clampPosition(pos, menuW, menuH, vw, vh);

    // Assert
    expect(result.left).toBe(100);
    expect(result.top).toBe(100);
  });

  it('clamps right edge when menu overflows viewport right', () => {
    // Arrange - click near right edge
    const pos = { x: 900, y: 100 };

    // Act
    const result = clampPosition(pos, menuW, menuH, vw, vh);

    // Assert - clamped to vw - menuW - margin
    expect(result.left).toBe(1024 - 200 - 8);
  });

  it('flips above when menu overflows viewport bottom', () => {
    // Arrange - click near bottom edge
    const pos = { x: 100, y: 600 };

    // Act
    const result = clampPosition(pos, menuW, menuH, vw, vh);

    // Assert - flipped above: y - menuH
    expect(result.top).toBe(600 - 300);
  });

  it('clamps to margin when flipping would go off-screen top', () => {
    // Arrange - near bottom AND near top (tiny viewport scenario)
    const pos = { x: 5, y: 100 };

    // Act - menu is 300px tall, 100 - 300 = -200, should clamp to margin
    const result = clampPosition(pos, menuW, menuH, 1024, 200);

    // Assert
    expect(result.top).toBe(8); // clamped to margin
    expect(result.left).toBe(8); // clamped to margin (5 < 8)
  });

  it('never goes below left margin', () => {
    // Arrange - click at x=2 (less than margin of 8)
    const pos = { x: 2, y: 100 };

    // Act
    const result = clampPosition(pos, menuW, menuH, vw, vh);

    // Assert
    expect(result.left).toBe(8);
  });
});

describe('Menu - anchor-based positioning with flip', () => {
  const vw = 1024;
  const vh = 768;
  const menuW = 200;
  const menuH = 150;

  const anchor = { top: 100, bottom: 140, left: 50, right: 150 };

  it('bottom-start: positions below and aligned to left of anchor', () => {
    // Act
    const result = anchorPosition(anchor, menuW, menuH, vw, vh, 'bottom-start');

    // Assert
    expect(result.top).toBe(140); // anchor.bottom
    expect(result.left).toBe(50); // anchor.left
  });

  it('bottom-end: positions below, flips left when menu wider than anchor', () => {
    // Act - anchor.right(150) - menuW(200) = -50 < margin(8), so flips to anchor.left
    const result = anchorPosition(anchor, menuW, menuH, vw, vh, 'bottom-end');

    // Assert
    expect(result.top).toBe(140); // anchor.bottom
    expect(result.left).toBe(50); // flipped to anchor.left since right-aligned overflows
  });

  it('flips to top when bottom overflows', () => {
    // Arrange - anchor near bottom of viewport
    const lowAnchor = { top: 650, bottom: 690, left: 50, right: 150 };

    // Act
    const result = anchorPosition(lowAnchor, menuW, menuH, vw, vh, 'bottom-start');

    // Assert - flipped: anchor.top - menuH
    expect(result.top).toBe(650 - 150);
  });

  it('flips to bottom when top overflows', () => {
    // Arrange - anchor near top of viewport
    const highAnchor = { top: 50, bottom: 90, left: 50, right: 150 };

    // Act - top-start: would be 50 - 150 = -100
    const result = anchorPosition(highAnchor, menuW, menuH, vw, vh, 'top-start');

    // Assert - flipped to bottom
    expect(result.top).toBe(90); // anchor.bottom
  });

  it('flips horizontal alignment when overflowing right', () => {
    // Arrange - anchor near right edge
    const rightAnchor = { top: 100, bottom: 140, left: 870, right: 970 };

    // Act - bottom-start: left=870, 870+200 > 1024-8
    const result = anchorPosition(rightAnchor, menuW, menuH, vw, vh, 'bottom-start');

    // Assert - flipped: anchor.right - menuW
    expect(result.left).toBe(970 - 200);
  });

  it('flips horizontal alignment when overflowing left', () => {
    // Arrange - anchor near left edge, placement bottom-end
    const leftAnchor = { top: 100, bottom: 140, left: 10, right: 60 };

    // Act - bottom-end: left = 60 - 200 = -140, flips to anchor.left
    const result = anchorPosition(leftAnchor, menuW, menuH, vw, vh, 'bottom-end');

    // Assert - flipped to anchor.left
    expect(result.left).toBe(10);
  });
});
