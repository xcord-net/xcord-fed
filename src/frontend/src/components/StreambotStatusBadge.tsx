import { Show } from 'solid-js';
import { tooltip } from '../directives/tooltip';
import type { BroadcastStreambotStatus } from '../stores/broadcast.store';
import styles from './StreambotStatusBadge.module.css';

// Prevent the tooltip directive from being tree-shaken.
void tooltip;

interface Props {
  name: string;
  status: BroadcastStreambotStatus;
  error?: string;
}

function statusClass(status: BroadcastStreambotStatus): string {
  switch (status) {
    case 'Connecting': return styles.statusConnecting;
    case 'Active': return styles.statusActive;
    case 'Failed': return styles.statusFailed;
    case 'Ended': return styles.statusEnded;
  }
}

export default function StreambotStatusBadge(props: Props) {
  const tooltipText = () => props.error ? `${props.status}: ${props.error}` : props.status;
  return (
    <span
      class={`${styles.badge} ${statusClass(props.status)}`}
      data-testid="streambot-status-badge"
      use:tooltip={tooltipText()}
    >
      <span class={styles.dot} aria-hidden="true" />
      <span class={styles.name}>{props.name}</span>
      <Show when={props.status === 'Failed' && props.error}>
        <span class={styles.errorIcon} aria-hidden="true">!</span>
      </Show>
    </span>
  );
}
