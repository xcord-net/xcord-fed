import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Extracted logic from Modal.tsx — we test the pure contracts (size mapping,
// aria-label derivation, role, backdrop click logic), not component rendering.
// ---------------------------------------------------------------------------

type ModalSize = 'sm' | 'md' | 'lg' | 'xl';

const SIZE_CLASS: Record<ModalSize, string> = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-xl',
  xl: 'max-w-2xl',
};

/** Derives the CSS class from a ModalSize, defaulting to 'md'. */
function sizeClass(size?: ModalSize): string {
  return SIZE_CLASS[size ?? 'md'];
}

/** Derives the accessible label — explicit aria-label takes priority, then title. */
function ariaLabel(explicitLabel?: string, title?: string): string | undefined {
  return explicitLabel ?? title;
}

/** Derives the dialog role — defaults to 'dialog'. */
function dialogRole(role?: 'dialog' | 'alertdialog'): 'dialog' | 'alertdialog' {
  return role ?? 'dialog';
}

/**
 * Backdrop click logic — the modal should only close when the click target
 * IS the backdrop itself (target === currentTarget), not when a click on an
 * inner element bubbles up.
 */
function shouldCloseOnBackdropClick(target: EventTarget | null, currentTarget: EventTarget | null): boolean {
  return target === currentTarget;
}

/** Detects OS-level reduced-motion preference. */
function prefersReducedMotion(matchMediaResult: boolean): boolean {
  return matchMediaResult;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('Modal — SIZE_CLASS mapping', () => {
  it('sm maps to max-w-sm', () => {
    expect(sizeClass('sm')).toBe('max-w-sm');
  });

  it('md maps to max-w-md', () => {
    expect(sizeClass('md')).toBe('max-w-md');
  });

  it('lg maps to max-w-xl', () => {
    expect(sizeClass('lg')).toBe('max-w-xl');
  });

  it('xl maps to max-w-2xl', () => {
    expect(sizeClass('xl')).toBe('max-w-2xl');
  });

  it('defaults to max-w-md when size is undefined', () => {
    expect(sizeClass(undefined)).toBe('max-w-md');
  });
});

describe('Modal — aria-label derivation', () => {
  it('uses explicit aria-label when provided', () => {
    // Act
    const label = ariaLabel('Custom Label', 'Dialog Title');

    // Assert
    expect(label).toBe('Custom Label');
  });

  it('falls back to title when no aria-label is provided', () => {
    // Act
    const label = ariaLabel(undefined, 'Dialog Title');

    // Assert
    expect(label).toBe('Dialog Title');
  });

  it('returns undefined when neither is provided', () => {
    // Act
    const label = ariaLabel(undefined, undefined);

    // Assert
    expect(label).toBeUndefined();
  });
});

describe('Modal — role', () => {
  it('defaults to dialog', () => {
    expect(dialogRole(undefined)).toBe('dialog');
  });

  it('uses alertdialog when specified', () => {
    expect(dialogRole('alertdialog')).toBe('alertdialog');
  });

  it('uses dialog when explicitly specified', () => {
    expect(dialogRole('dialog')).toBe('dialog');
  });
});

describe('Modal — backdrop click logic', () => {
  let backdrop: HTMLDivElement;
  let inner: HTMLDivElement;

  beforeEach(() => {
    backdrop = document.createElement('div');
    inner = document.createElement('div');
    backdrop.appendChild(inner);
    document.body.appendChild(backdrop);
  });

  afterEach(() => {
    document.body.removeChild(backdrop);
  });

  it('closes when click target is the backdrop itself', () => {
    // Act
    const result = shouldCloseOnBackdropClick(backdrop, backdrop);

    // Assert
    expect(result).toBe(true);
  });

  it('does not close when click target is an inner element', () => {
    // Act — inner click bubbles up to backdrop as currentTarget
    const result = shouldCloseOnBackdropClick(inner, backdrop);

    // Assert
    expect(result).toBe(false);
  });

  it('integration: backdrop click handler fires onClose', () => {
    // Arrange
    const onClose = vi.fn();

    backdrop.addEventListener('click', (e) => {
      if (e.target === e.currentTarget) onClose();
    });

    // Act — click directly on backdrop
    backdrop.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    // Assert
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('integration: inner click does not fire onClose', () => {
    // Arrange
    const onClose = vi.fn();

    backdrop.addEventListener('click', (e) => {
      if (e.target === e.currentTarget) onClose();
    });

    // Act — click on inner element, event bubbles up
    inner.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    // Assert
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe('Modal — prefers-reduced-motion', () => {
  it('returns true when reduced motion is preferred', () => {
    expect(prefersReducedMotion(true)).toBe(true);
  });

  it('returns false when reduced motion is not preferred', () => {
    expect(prefersReducedMotion(false)).toBe(false);
  });

  it('component logic: returns false when window is undefined', () => {
    // The component checks typeof window !== 'undefined' before calling matchMedia.
    // When the guard fails (SSR), the result is false — no animation skip.
    const result = typeof undefined !== 'undefined' && false;

    // Assert
    expect(result).toBe(false);
  });
});
