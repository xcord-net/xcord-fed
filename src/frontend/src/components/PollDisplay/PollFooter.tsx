import { Show } from 'solid-js';
import styles from './PollFooter.module.css';
import { formatDate } from '../../utils/datetime';

interface PollFooterProps {
  totalVotes: number;
  allowMultiSelect: boolean;
  isClosed: boolean;
  expiresAt?: string;
  canEnd?: boolean;
  isEndingPoll: boolean;
  onEndPoll: () => void;
}

export default function PollFooter(props: PollFooterProps) {
  return (
    <div class={styles.pollFooter}>
      <span data-testid="poll-total-votes" class={styles.totalVotes}>
        {props.totalVotes} {props.totalVotes === 1 ? 'vote' : 'votes'}
        <Show when={props.allowMultiSelect}>
          <span class={styles.multiSelectNote}>(multi-select)</span>
        </Show>
      </span>

      <div class={styles.footerActions}>
        <Show when={props.isClosed}>
          <span
            class={styles.closedBadge}
            aria-label="Poll closed"
          >
            Closed
          </span>
        </Show>

        <Show when={!props.isClosed && props.expiresAt}>
          <span class={styles.expiryText}>
            Ends {formatDate(props.expiresAt!)}
          </span>
        </Show>

        <Show when={!props.isClosed && props.canEnd}>
          <button
            class={styles.endPollButton}
            onClick={props.onEndPoll}
            disabled={props.isEndingPoll}
            aria-label="End poll"
          >
            End Poll
          </button>
        </Show>
      </div>
    </div>
  );
}
