import { Show, createEffect, onMount, splitProps } from 'solid-js';
import type { JSX } from 'solid-js';
import { createFocusTrap } from '../../hooks/createFocusTrap';
import { createScrollLock } from '../../hooks/createScrollLock';

// Whether the user has requested reduced motion at the OS level.
const prefersReducedMotion = () =>
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

type ModalSize = 'sm' | 'md' | 'lg' | 'xl';

const SIZE_CLASS: Record<ModalSize, string> = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-xl',
  xl: 'max-w-2xl',
};

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  'aria-label'?: string;
  'data-testid'?: string;
  size?: ModalSize;
  role?: 'dialog' | 'alertdialog';
  children: JSX.Element;
  initialFocusRef?: HTMLElement;
}

/**
 * Modal - a reusable, accessible modal dialog.
 *
 * - Traps keyboard focus inside the dialog while open.
 * - Locks body scroll, compensating for scrollbar width.
 * - Closes on Escape key or backdrop click.
 * - Renders an optional title header with a close button.
 * - Entrance animation: backdrop fade + content scale-in (skipped when
 *   the user prefers reduced motion).
 *
 * Usage:
 *   <Modal open={isOpen()} onClose={() => setIsOpen(false)} title="My Dialog">
 *     <p>Content here</p>
 *   </Modal>
 */
export default function Modal(props: ModalProps) {
  const [local, _rest] = splitProps(props, [
    'open',
    'onClose',
    'title',
    'aria-label',
    'data-testid',
    'size',
    'role',
    'children',
    'initialFocusRef',
  ]);

  let dialogRef!: HTMLDivElement;

  // Lock body scroll while the modal is open.
  createScrollLock(() => local.open);

  // Manage focus trap - only active while the modal is mounted (i.e. open).
  createFocusTrap(() => dialogRef, { onEscape: () => local.onClose() });

  // If an initialFocusRef is provided, move focus there after mount.
  onMount(() => {
    if (local.initialFocusRef) {
      local.initialFocusRef.focus();
    }
  });

  // Entrance animation state: we flip this to true on the next frame so the
  // CSS transition actually runs (starting from the "hidden" state).
  let backdropEl!: HTMLDivElement;
  let panelEl!: HTMLDivElement;

  createEffect(() => {
    if (!local.open) return;
    if (prefersReducedMotion()) return;

    // Start invisible/scaled-down, then transition to visible on next frame.
    backdropEl.style.opacity = '0';
    panelEl.style.opacity = '0';
    panelEl.style.transform = 'scale(0.95)';

    requestAnimationFrame(() => {
      backdropEl.style.opacity = '';
      panelEl.style.opacity = '';
      panelEl.style.transform = '';
    });
  });

  const sizeClass = () => SIZE_CLASS[local.size ?? 'md'];
  const ariaLabel = () => local['aria-label'] ?? local.title;
  const dialogRole = () => local.role ?? 'dialog';

  return (
    <Show when={local.open}>
      {/* Backdrop */}
      <div
        ref={backdropEl}
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 transition-opacity duration-200"
        onClick={(e) => {
          if (e.target === e.currentTarget) local.onClose();
        }}
      >
        {/* Content panel */}
        <div
          ref={(el) => {
            panelEl = el;
            dialogRef = el;
          }}
          data-testid={local['data-testid']}
          role={dialogRole()}
          aria-modal="true"
          aria-label={ariaLabel()}
          class={`relative w-full ${sizeClass()} mx-4 max-h-[90vh] bg-xcord-bg-secondary rounded-lg shadow-xl flex flex-col overflow-hidden transition-all duration-200`}
          onClick={(e) => e.stopPropagation()}
        >
          {/* Optional header */}
          <Show when={local.title}>
            <div class="flex items-center justify-between px-6 py-4 border-b border-xcord-border flex-shrink-0">
              <h2 class="text-xl font-bold text-xcord-text-primary">{local.title}</h2>
              <button
                type="button"
                aria-label="Close dialog"
                onClick={() => local.onClose()}
                class="text-xcord-text-muted hover:text-xcord-text-primary transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand rounded p-0.5"
              >
                {/* X icon */}
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="w-5 h-5"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  aria-hidden="true"
                >
                  <path
                    fill-rule="evenodd"
                    d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                    clip-rule="evenodd"
                  />
                </svg>
              </button>
            </div>
          </Show>

          {/* Scrollable body */}
          <div class="flex-1 overflow-y-auto">
            {local.children}
          </div>
        </div>
      </div>
    </Show>
  );
}
