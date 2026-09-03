import { Show, type JSX } from 'solid-js';
import Modal from './Modal';
import styles from './Surface.module.css';

type SurfaceSize = 'sm' | 'md' | 'lg' | 'xl';

export interface SurfaceProps {
  /**
   * Render as a pane in the Deck's content area rather than as a dialog over
   * it. The Deck opens settings as tabs; the same surfaces are still reachable
   * as dialogs from places that have no strip to put a tab in.
   */
  inline?: boolean;
  onClose: () => void;
  'aria-label'?: string;
  'data-testid'?: string;
  /** Ignored when inline: a pane is as wide as the pane. */
  size?: SurfaceSize;
  children: JSX.Element;
}

/**
 * One body, two hosts.
 *
 * Settings used to be dialogs only. Under the Deck they are tabs, and a tab is
 * not something you dismiss with Escape or trap focus inside. Rather than fork
 * every settings screen into a modal copy and a pane copy, the screen declares
 * its content once and this decides how to host it — so the test ids, headings
 * and behaviour stay identical either way.
 */
export default function Surface(props: SurfaceProps) {
  return (
    <Show
      when={props.inline}
      fallback={
        <Modal
          open={true}
          onClose={() => props.onClose()}
          aria-label={props['aria-label']}
          data-testid={props['data-testid']}
          size={props.size}
        >
          {props.children}
        </Modal>
      }
    >
      <section
        class={styles.pane}
        data-testid={props['data-testid']}
        aria-label={props['aria-label']}
      >
        {props.children}
      </section>
    </Show>
  );
}
