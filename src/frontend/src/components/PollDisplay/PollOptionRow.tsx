import { Show } from 'solid-js';
import type { PollOption } from './helpers';
import { votePercentage } from './helpers';
import styles from './PollOptionRow.module.css';

interface PollOptionRowProps {
  option: PollOption;
  index: number;
  totalVotes: number;
  voted: boolean;
  isClosed: boolean;
  isVoting: boolean;
  onVote: (optionId: string) => void;
}

export default function PollOptionRow(props: PollOptionRowProps) {
  const pct = () => votePercentage(props.option, props.totalVotes);

  return (
    <div class={styles.optionWrapper}>
      <button
        data-testid={`poll-option-${props.index}`}
        classList={{
          [styles.optionButton]: true,
          [styles.optionButtonVoted]: props.voted,
          [styles.optionButtonDefault]: !props.voted && !props.isClosed,
          [styles.optionButtonClosed]: props.isClosed,
        }}
        onClick={() => !props.isClosed && props.onVote(props.option.id)}
        disabled={props.isClosed || props.isVoting}
        aria-pressed={props.voted}
        aria-label={`Vote for ${props.option.text}`}
      >
        {/* Progress bar background */}
        <div
          classList={{
            [styles.optionProgress]: true,
            [styles.optionProgressVoted]: props.voted,
            [styles.optionProgressDefault]: !props.voted,
          }}
          style={{ width: `${pct()}%` }}
        />

        {/* Content row */}
        <div class={styles.optionContent}>
          <div class={styles.optionLabelGroup}>
            <Show when={props.voted}>
              <span data-testid={`poll-option-${props.index}-voted`} class={styles.optionVotedCheck}>✓</span>
            </Show>
            <span class={styles.optionText}>{props.option.text}</span>
          </div>
          <span data-testid={`poll-option-${props.index}-count`} class={styles.optionPercent}>
            {pct()}%
          </span>
        </div>
      </button>
    </div>
  );
}
