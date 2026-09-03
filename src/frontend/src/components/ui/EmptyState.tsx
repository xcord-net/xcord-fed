import { Show, splitProps } from 'solid-js';
import type { Component, JSX } from 'solid-js';
import { Icon } from './Icon';
import styles from './EmptyState.module.css';

type LucideProps = JSX.SvgSVGAttributes<SVGSVGElement> & {
  size?: number | string;
  'stroke-width'?: number | string;
};

export interface EmptyStateProps {
  /** A lucide-solid icon. Omit on dense surfaces where a glyph is noise. */
  icon?: Component<LucideProps>;
  /** What is not here, stated plainly. Not an apology. */
  title: string;
  /** Optional second line: why it is empty, or what fills it. */
  body?: string;
  /** The one thing to do about it. */
  action?: { label: string; onClick: () => void };
  /** Compact form for lists and popovers, where a full panel would dominate. */
  dense?: boolean;
  'data-testid'?: string;
}

/**
 * The one way to say "there is nothing here yet".
 *
 * Before this, 36 components each rolled their own — seventeen different class
 * names for the same idea, and copy that mostly stopped at "No X". An empty
 * screen is the best chance the product gets to say what the screen is for, so
 * this shape makes room for that and for the action that fills it.
 */
export default function EmptyState(props: EmptyStateProps) {
  const [local] = splitProps(props, [
    'icon', 'title', 'body', 'action', 'dense', 'data-testid',
  ]);

  return (
    <div
      class={local.dense ? `${styles.root} ${styles.dense}` : styles.root}
      data-testid={local['data-testid']}
    >
      <Show when={local.icon}>
        {(glyph) => <Icon icon={glyph()} size={local.dense ? 20 : 28} class={styles.icon} />}
      </Show>

      <p class={styles.title} data-testid="empty-state-title">{local.title}</p>

      <Show when={local.body}>
        <p class={styles.body} data-testid="empty-state-body">{local.body}</p>
      </Show>

      <Show when={local.action}>
        {(action) => (
          <button
            type="button"
            class={styles.action}
            data-testid="empty-state-action"
            onClick={() => action().onClick()}
          >
            {action().label}
          </button>
        )}
      </Show>
    </div>
  );
}

export { EmptyState };
