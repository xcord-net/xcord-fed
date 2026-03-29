import { onCleanup } from 'solid-js';

declare module 'solid-js' {
  namespace JSX {
    interface Directives {
      tooltip: string;
    }
  }
}

const SHOW_DELAY = 400;
const OFFSET = 8;

export function tooltip(el: HTMLElement, accessor: () => string) {
  let tip: HTMLDivElement | null = null;
  let showTimeout: ReturnType<typeof setTimeout> | undefined;

  const show = () => {
    showTimeout = setTimeout(() => {
      const text = accessor();
      if (!text) return;

      tip = document.createElement('div');
      tip.textContent = text;
      Object.assign(tip.style, {
        position: 'fixed',
        zIndex: '70',
        padding: '4px 8px',
        borderRadius: '4px',
        fontSize: '12px',
        lineHeight: '1',
        whiteSpace: 'nowrap',
        pointerEvents: 'none',
        backgroundColor: '#0c0e14',
        color: '#e6e4f0',
        boxShadow: '0 2px 8px rgba(0,0,0,0.4)',
        border: '1px solid #1e2130',
      });
      document.body.appendChild(tip);

      // Position above the element, centered
      const rect = el.getBoundingClientRect();
      const tipRect = tip.getBoundingClientRect();

      let top = rect.top - tipRect.height - OFFSET;
      let left = rect.left + (rect.width - tipRect.width) / 2;

      // Flip below if no room above
      if (top < OFFSET) {
        top = rect.bottom + OFFSET;
      }

      // Clamp horizontally
      left = Math.max(OFFSET, Math.min(left, window.innerWidth - tipRect.width - OFFSET));

      tip.style.top = `${top}px`;
      tip.style.left = `${left}px`;
    }, SHOW_DELAY);
  };

  const hide = () => {
    clearTimeout(showTimeout);
    if (tip) {
      tip.remove();
      tip = null;
    }
  };

  el.addEventListener('mouseenter', show);
  el.addEventListener('mouseleave', hide);
  el.addEventListener('mousedown', hide);

  onCleanup(() => {
    hide();
    el.removeEventListener('mouseenter', show);
    el.removeEventListener('mouseleave', hide);
    el.removeEventListener('mousedown', hide);
  });
}
