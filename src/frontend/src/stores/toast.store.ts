import { createSignal, createRoot } from 'solid-js';

export type ToastKind = 'error' | 'success' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

const DEFAULT_DURATION_MS = 5000;

const store = createRoot(() => {
  const [toasts, setToasts] = createSignal<Toast[]>([]);
  return { toasts, setToasts };
});

let nextId = 1;

export function useToasts() {
  function push(kind: ToastKind, message: string, durationMs = DEFAULT_DURATION_MS): number {
    const id = nextId++;
    store.setToasts([...store.toasts(), { id, kind, message }]);
    if (durationMs > 0) {
      window.setTimeout(() => dismiss(id), durationMs);
    }
    return id;
  }

  function dismiss(id: number): void {
    store.setToasts(store.toasts().filter((t) => t.id !== id));
  }

  return {
    get toasts() { return store.toasts(); },
    push,
    dismiss,
    error(message: string): number { return push('error', message); },
    success(message: string): number { return push('success', message); },
    info(message: string): number { return push('info', message); },
    reset(): void { store.setToasts([]); },
  };
}
