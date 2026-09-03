import { Show, createEffect, onCleanup, splitProps } from 'solid-js';
import type { JSX } from 'solid-js';
import styles from './Menu.module.css';

// Whether the user has requested reduced motion at the OS level.
const prefersReducedMotion = () =>
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

type Placement = 'bottom-start' | 'bottom-end' | 'top-start' | 'top-end';

interface MenuProps {
  open: boolean;
  onClose: () => void;
  anchorRef?: HTMLElement;
  position?: { x: number; y: number };
  placement?: Placement;
  children: JSX.Element;
}

/** Returns all [role="menuitem"] elements within the given container. */
function getMenuItems(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>('[role="menuitem"]')).filter(
    (el) => !el.hasAttribute('disabled') && getComputedStyle(el).display !== 'none',
  );
}

/**
 * Menu - a reusable dropdown / context-menu component.
 *
 * Dropdown mode: pass `anchorRef` to position relative to a trigger element.
 * Context-menu mode: pass `position` (mouse coordinates) to position at a
 * specific point on the page.
 *
 * Keyboard navigation:
 *   ArrowDown / ArrowUp - moves focus between menu items
 *   Home / End           - jumps to first / last item
 *   Enter / Space        - activates the focused item
 *   Escape               - closes the menu
 *
 * Usage:
 *   <Menu open={isOpen()} onClose={() => setIsOpen(false)} anchorRef={buttonRef}>
 *     <button role="menuitem" onClick={...}>Option 1</button>
 *   </Menu>
 */
export default function Menu(props: MenuProps) {
  const [local, _rest] = splitProps(props, [
    'open',
    'onClose',
    'anchorRef',
    'position',
    'placement',
    'children',
  ]);

  let menuEl!: HTMLDivElement;

  // Position the menu relative to the anchor or pointer position, clamped to
  // the viewport so it never overflows.
  createEffect(() => {
    if (!local.open) return;

    // Re-run whenever the menu changes size. Items can appear after the menu is
    // already on screen - a permission check resolving adds moderation entries -
    // and a menu positioned while it was short then grew past the bottom edge
    // and stayed there, leaving items visible but unclickable.
    const observer = typeof ResizeObserver === 'undefined'
      ? null
      : new ResizeObserver(() => place());
    onCleanup(() => observer?.disconnect());

    function place() {
      if (!menuEl) return;

      const menuWidth = menuEl.offsetWidth;
      const menuHeight = menuEl.offsetHeight;
      const vw = window.innerWidth;
      const vh = window.innerHeight;
      const MARGIN = 8; // minimum gap from viewport edge

      let top = 0;
      let left = 0;

      // The menu is `position: fixed`, so top/left are viewport coordinates and
      // page scroll must not be added to them - `getBoundingClientRect` and
      // `clientX/Y` are already in that space. Adding `scrollY` pushed the menu
      // off the bottom of the screen by exactly the scroll distance, which is
      // why an item could be reported visible and still be unclickable.
      if (local.anchorRef) {
        const anchor = local.anchorRef.getBoundingClientRect();
        const placement: Placement = local.placement ?? 'bottom-start';

        // Vertical
        if (placement.startsWith('bottom')) {
          top = anchor.bottom;
          // Flip to top if overflows bottom
          if (anchor.bottom + menuHeight > vh - MARGIN) {
            top = anchor.top - menuHeight;
          }
        } else {
          top = anchor.top - menuHeight;
          // Flip to bottom if overflows top
          if (anchor.top - menuHeight < MARGIN) {
            top = anchor.bottom;
          }
        }

        // Horizontal
        if (placement.endsWith('start')) {
          left = anchor.left;
          if (left + menuWidth > vw - MARGIN) {
            left = anchor.right - menuWidth;
          }
        } else {
          left = anchor.right - menuWidth;
          if (left < MARGIN) {
            left = anchor.left;
          }
        }
      } else if (local.position) {
        top = local.position.y;
        left = local.position.x;

        // Clamp right edge
        if (left + menuWidth > vw - MARGIN) {
          left = vw - menuWidth - MARGIN;
        }
        // Clamp bottom edge
        if (top + menuHeight > vh - MARGIN) {
          top = local.position.y - menuHeight;
        }
      }

      // Never off the left or top, and never taller than the screen allows.
      left = Math.max(MARGIN, Math.min(left, vw - menuWidth - MARGIN));
      top = Math.max(MARGIN, Math.min(top, vh - menuHeight - MARGIN));

      menuEl.style.top = `${top}px`;
      menuEl.style.left = `${left}px`;
    }

    // Run positioning after the element has rendered.
    requestAnimationFrame(() => {
      if (!menuEl) return;
      place();
      observer?.observe(menuEl);
    });
  });

  // Entrance animation - scale from 95% + fade over 150ms.
  createEffect(() => {
    if (!local.open) return;
    if (prefersReducedMotion()) return;

    requestAnimationFrame(() => {
      if (!menuEl) return;
      menuEl.style.opacity = '0';
      menuEl.style.transform = 'scale(0.95)';
      requestAnimationFrame(() => {
        menuEl.style.opacity = '';
        menuEl.style.transform = '';
      });
    });
  });

  // Focus the first menu item when the menu opens.
  createEffect(() => {
    if (!local.open) return;
    requestAnimationFrame(() => {
      if (!menuEl) return;
      const items = getMenuItems(menuEl);
      if (items.length > 0) items[0].focus();
    });
  });

  // Keyboard navigation + Escape handling.
  createEffect(() => {
    if (!local.open) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (!menuEl) return;
      const items = getMenuItems(menuEl);
      const focused = document.activeElement as HTMLElement | null;
      const currentIndex = focused ? items.indexOf(focused) : -1;

      switch (e.key) {
        case 'Escape':
          e.preventDefault();
          local.onClose();
          break;

        case 'ArrowDown':
          e.preventDefault();
          if (items.length === 0) break;
          if (currentIndex < items.length - 1) {
            items[currentIndex + 1].focus();
          } else {
            items[0].focus();
          }
          break;

        case 'ArrowUp':
          e.preventDefault();
          if (items.length === 0) break;
          if (currentIndex > 0) {
            items[currentIndex - 1].focus();
          } else {
            items[items.length - 1].focus();
          }
          break;

        case 'Home':
          e.preventDefault();
          if (items.length > 0) items[0].focus();
          break;

        case 'End':
          e.preventDefault();
          if (items.length > 0) items[items.length - 1].focus();
          break;

        case 'Enter':
        case ' ':
          // Let the browser handle activation of the focused element naturally,
          // but only if focus is already on a menu item.
          if (focused && items.includes(focused)) {
            // Space needs preventDefault to avoid page scroll; Enter is fine.
            if (e.key === ' ') e.preventDefault();
            focused.click();
          }
          break;

        default:
          break;
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    onCleanup(() => document.removeEventListener('keydown', handleKeyDown));
  });

  return (
    <Show when={local.open}>
      {/* Click-outside overlay - transparent, covers the whole viewport */}
      <div
        class={styles.overlay}
        aria-hidden="true"
        onClick={() => local.onClose()}
      />

      {/* Menu container - positioned absolutely via JS */}
      <div
        ref={menuEl}
        role="menu"
        class={styles.menu}
        style={{ top: '0px', left: '0px' }}
      >
        {local.children}
      </div>
    </Show>
  );
}
