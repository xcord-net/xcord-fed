import { describe, it, expect, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Extracted logic from createScrollLock.ts — we test the lock/unlock counter
// mechanism, not the Solid reactive wrapper.
// ---------------------------------------------------------------------------

let lockCount = 0;
let savedOverflow = '';

function lock() {
  if (lockCount === 0) {
    savedOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
  }
  lockCount += 1;
}

function unlock() {
  if (lockCount <= 0) return;
  lockCount -= 1;
  if (lockCount === 0) {
    document.body.style.overflow = savedOverflow;
    savedOverflow = '';
  }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('createScrollLock — lock/unlock counter', () => {
  beforeEach(() => {
    // Reset module-level state before each test
    lockCount = 0;
    savedOverflow = '';
    document.body.style.overflow = '';
  });

  afterEach(() => {
    document.body.style.overflow = '';
  });

  it('lock sets body overflow to hidden', () => {
    // Arrange
    expect(document.body.style.overflow).toBe('');

    // Act
    lock();

    // Assert
    expect(document.body.style.overflow).toBe('hidden');
    expect(lockCount).toBe(1);
  });

  it('unlock restores original overflow', () => {
    // Arrange
    document.body.style.overflow = 'auto';
    savedOverflow = '';
    lockCount = 0;

    // Act
    lock(); // saves 'auto', sets to 'hidden'
    unlock(); // restores 'auto'

    // Assert
    expect(document.body.style.overflow).toBe('auto');
    expect(lockCount).toBe(0);
  });

  it('stacking: 2 locks then 1 unlock = still locked', () => {
    // Arrange & Act
    lock();
    lock();
    expect(lockCount).toBe(2);

    unlock(); // only decrement — still 1 lock remaining

    // Assert
    expect(lockCount).toBe(1);
    expect(document.body.style.overflow).toBe('hidden');
  });

  it('stacking: 2nd unlock after 2 locks restores overflow', () => {
    // Arrange
    document.body.style.overflow = 'scroll';

    // Act
    lock(); // saves 'scroll', sets 'hidden'
    lock(); // lockCount = 2, no style change
    unlock(); // lockCount = 1, still hidden
    unlock(); // lockCount = 0, restores 'scroll'

    // Assert
    expect(lockCount).toBe(0);
    expect(document.body.style.overflow).toBe('scroll');
  });

  it('does not go below zero lockCount', () => {
    // Act — unlock without any prior locks
    unlock();
    unlock();
    unlock();

    // Assert
    expect(lockCount).toBe(0);
    expect(document.body.style.overflow).toBe('');
  });

  it('lock-unlock-lock-unlock cycle works correctly', () => {
    // Arrange
    document.body.style.overflow = 'visible';

    // First lock-unlock cycle
    lock();
    expect(document.body.style.overflow).toBe('hidden');
    unlock();
    expect(document.body.style.overflow).toBe('visible');
    expect(lockCount).toBe(0);

    // Second lock-unlock cycle (original overflow was restored, now we lock again)
    lock();
    expect(document.body.style.overflow).toBe('hidden');
    unlock();
    expect(document.body.style.overflow).toBe('visible');
    expect(lockCount).toBe(0);
  });

  it('saves overflow at the moment of first lock only', () => {
    // Arrange — body starts with 'auto'
    document.body.style.overflow = 'auto';

    // Act — first lock captures 'auto'
    lock();
    // Change overflow externally while locked (should not affect saved value)
    document.body.style.overflow = 'scroll';
    lock(); // second lock does NOT re-capture

    unlock();
    unlock(); // restores original 'auto', not 'scroll'

    // Assert
    expect(document.body.style.overflow).toBe('auto');
  });
});
