import { createEffect, onCleanup } from 'solid-js';

// Module-level lock counter - supports multiple overlapping consumers
// (e.g. a modal opened on top of another modal).
let lockCount = 0;
let savedOverflow = '';
let savedPaddingRight = '';

function lock() {
  if (lockCount === 0) {
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    savedOverflow = document.body.style.overflow;
    savedPaddingRight = document.body.style.paddingRight;

    document.body.style.overflow = 'hidden';
    if (scrollbarWidth > 0) {
      // Compensate for the scrollbar width so the layout doesn't shift.
      const currentPaddingRight = parseInt(getComputedStyle(document.body).paddingRight, 10) || 0;
      document.body.style.paddingRight = `${currentPaddingRight + scrollbarWidth}px`;
    }
  }
  lockCount += 1;
}

function unlock() {
  if (lockCount <= 0) return;
  lockCount -= 1;
  if (lockCount === 0) {
    document.body.style.overflow = savedOverflow;
    document.body.style.paddingRight = savedPaddingRight;
    savedOverflow = '';
    savedPaddingRight = '';
  }
}

/**
 * createScrollLock - locks body scroll while `active()` returns true.
 *
 * Supports stacking: multiple callers can lock simultaneously; the body
 * scroll is only restored once every caller has released the lock.
 *
 * Compensates for the scrollbar width disappearing so that the page layout
 * does not shift when the lock is applied.
 *
 * Usage:
 *   createScrollLock(() => props.open);
 */
export function createScrollLock(active: () => boolean) {
  let locked = false;

  createEffect(() => {
    const isActive = active();

    if (isActive && !locked) {
      lock();
      locked = true;
    } else if (!isActive && locked) {
      unlock();
      locked = false;
    }
  });

  onCleanup(() => {
    if (locked) {
      unlock();
      locked = false;
    }
  });
}
