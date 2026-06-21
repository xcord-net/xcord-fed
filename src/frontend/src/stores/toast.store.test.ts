import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { useToasts } from './toast.store';

describe('toast.store', () => {
  beforeEach(() => {
    useToasts().reset();
  });

  it('pushes an error toast and exposes it with the right kind and message', () => {
    const toasts = useToasts();
    toasts.error('Something failed');
    expect(toasts.toasts).toHaveLength(1);
    expect(toasts.toasts[0].kind).toBe('error');
    expect(toasts.toasts[0].message).toBe('Something failed');
  });

  it('assigns distinct ids and supports multiple concurrent toasts', () => {
    const toasts = useToasts();
    toasts.success('a');
    toasts.info('b');
    expect(toasts.toasts).toHaveLength(2);
    expect(toasts.toasts[0].id).not.toBe(toasts.toasts[1].id);
    expect(toasts.toasts.map((t) => t.kind)).toEqual(['success', 'info']);
  });

  it('dismisses a specific toast by id without affecting others', () => {
    const toasts = useToasts();
    const id = toasts.error('keep one');
    toasts.success('remove me then dismiss the first');
    toasts.dismiss(id);
    expect(toasts.toasts).toHaveLength(1);
    expect(toasts.toasts[0].kind).toBe('success');
  });

  describe('auto-dismiss', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('auto-dismisses after the default duration', () => {
      const toasts = useToasts();
      toasts.error('temporary');
      expect(toasts.toasts).toHaveLength(1);
      vi.advanceTimersByTime(5000);
      expect(toasts.toasts).toHaveLength(0);
    });

    it('does not auto-dismiss before the duration elapses', () => {
      const toasts = useToasts();
      toasts.info('still here');
      vi.advanceTimersByTime(4000);
      expect(toasts.toasts).toHaveLength(1);
    });
  });
});
