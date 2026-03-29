import { Show, createEffect, onCleanup, splitProps } from 'solid-js';
import type { JSX } from 'solid-js';
import styles from './Dropdown.module.css';

type Anchor = 'top-start' | 'top-end' | 'bottom-start' | 'bottom-end';

interface DropdownProps {
  open: boolean;
  onClose: () => void;
  trigger: HTMLElement | undefined;
  anchor?: Anchor;
  children: JSX.Element;
}

/**
 * Dropdown - a reusable floating panel anchored to a trigger element.
 *
 * Renders a click-outside overlay and a fixed-position panel near the trigger.
 * Closes on Escape or click-outside.
 */
export default function Dropdown(props: DropdownProps) {
  const [local] = splitProps(props, ['open', 'onClose', 'trigger', 'anchor', 'children']);
  let panelRef!: HTMLDivElement;

  const position = (): JSX.CSSProperties => {
    const el = local.trigger;
    if (!el) return {};
    const rect = el.getBoundingClientRect();
    const placement = local.anchor ?? 'top-start';
    const s: JSX.CSSProperties = {};

    if (placement.startsWith('top')) {
      s.bottom = `${window.innerHeight - rect.top + 8}px`;
    } else {
      s.top = `${rect.bottom + 8}px`;
    }

    if (placement.endsWith('start')) {
      s.left = `${rect.left}px`;
    } else {
      s.right = `${window.innerWidth - rect.right}px`;
    }

    return s;
  };

  // Clamp to viewport after render
  createEffect(() => {
    if (!local.open || !panelRef) return;
    requestAnimationFrame(() => {
      if (!panelRef) return;
      const r = panelRef.getBoundingClientRect();
      if (r.right > window.innerWidth - 8) {
        panelRef.style.left = `${window.innerWidth - r.width - 8}px`;
        panelRef.style.right = 'auto';
      }
      if (r.left < 8) {
        panelRef.style.left = '8px';
        panelRef.style.right = 'auto';
      }
      if (r.top < 8) {
        panelRef.style.top = '8px';
        panelRef.style.bottom = 'auto';
      }
    });
  });

  // Close on Escape
  createEffect(() => {
    if (!local.open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        local.onClose();
      }
    };
    document.addEventListener('keydown', handler);
    onCleanup(() => document.removeEventListener('keydown', handler));
  });

  return (
    <Show when={local.open}>
      <div class={styles.overlay} aria-hidden="true" onClick={() => local.onClose()} />
      <div ref={panelRef} class={styles.panel} style={position()}>
        {local.children}
      </div>
    </Show>
  );
}
