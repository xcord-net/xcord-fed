import { For, createEffect, createSignal, on } from 'solid-js';
import { api } from '../../api/client';
import type { Poll } from './helpers';
import { fetchPollState } from './helpers';
import PollOptionRow from './PollOptionRow';
import PollFooter from './PollFooter';
import EndPollConfirmModal from './EndPollConfirmModal';
import Flexbox from '../ui/Flexbox';
import styles from './PollDisplay.module.css';

interface PollDisplayProps {
  pollId: string;
  poll: Poll;
  /** Whether the current user can end this poll (is creator or has ManageMessages) */
  canEnd?: boolean;
  /** Called after a successful vote so the parent can refresh poll data */
  onVoted?: (updatedPoll: Poll) => void;
}

export default function PollDisplay(props: PollDisplayProps) {
  const [isVoting, setIsVoting] = createSignal(false);
  const [isEndingPoll, setIsEndingPoll] = createSignal(false);
  const [showEndConfirm, setShowEndConfirm] = createSignal(false);
  const [localPoll, setLocalPoll] = createSignal<Poll>(props.poll);

  /**
   * The local copy exists only to hold the optimistic vote between the click
   * and the server's answer. It is not the source of truth: it used to be
   * seeded from props once and never touched again, which meant a poll only
   * ever moved for the person who voted - everyone else watched a frozen
   * count, because the announced tally the parent lays over `props.poll` had
   * nowhere to land.
   *
   * So whatever the parent hands down next - a refetch, or an announcement
   * from someone else's vote - is newer than the optimistic guess and replaces
   * it.
   */
  createEffect(on(() => props.poll, (incoming) => setLocalPoll(incoming), { defer: true }));

  const poll = () => localPoll();

  const isExpired = () => {
    if (!poll().expiresAt) return false;
    return new Date(poll().expiresAt!).getTime() < Date.now();
  };
  const isClosed = () => poll().isClosed || isExpired();

  const hasVotedForOption = (optionId: string) =>
    poll().userVotedOptionIds.includes(optionId);

  const handleEndPoll = () => {
    if (isClosed() || isEndingPoll()) return;
    setShowEndConfirm(true);
  };

  const confirmEndPoll = async () => {
    setShowEndConfirm(false);
    setIsEndingPoll(true);
    try {
      await api.post(`/api/v1/polls/${props.pollId}/end`, {});
      setLocalPoll({ ...poll(), isClosed: true });
    } catch {
      // swallow - show no error, state will remain
    } finally {
      setIsEndingPoll(false);
    }
  };

  const handleVote = async (optionId: string) => {
    if (isClosed() || isVoting()) return;

    const currentPoll = poll();
    const alreadyVoted = hasVotedForOption(optionId);

    // For single-select: if voted elsewhere, remove old vote then add new
    // For multi-select: toggle the option
    let newVotedIds: string[];
    if (currentPoll.allowMultiSelect) {
      newVotedIds = alreadyVoted
        ? currentPoll.userVotedOptionIds.filter((id) => id !== optionId)
        : [...currentPoll.userVotedOptionIds, optionId];
    } else {
      newVotedIds = alreadyVoted ? [] : [optionId];
    }

    // Optimistic update
    const voteDelta = newVotedIds.includes(optionId) ? 1 : -1;
    const optimisticPoll: Poll = {
      ...currentPoll,
      userVotedOptionIds: newVotedIds,
      totalVotes: currentPoll.totalVotes + voteDelta,
      options: currentPoll.options.map((opt) =>
        opt.id === optionId
          ? { ...opt, voteCount: opt.voteCount + voteDelta }
          : opt,
      ),
    };
    setLocalPoll(optimisticPoll);

    setIsVoting(true);
    try {
      if (newVotedIds.length === 0) {
        // Retracting all votes
        await api.delete(`/api/v1/polls/${props.pollId}/vote`);
        // Reload the updated poll state
        const refreshed = await fetchPollState(props.pollId);
        if (refreshed) {
          setLocalPoll(refreshed);
          props.onVoted?.(refreshed);
        }
      } else {
        // Voting (replaces any existing vote for single-select, adds for multi-select)
        await api.put(
          `/api/v1/polls/${props.pollId}/vote`,
          { optionIds: newVotedIds.map(String) },
        );
        // Reload the updated poll state
        const refreshed = await fetchPollState(props.pollId);
        if (refreshed) {
          setLocalPoll(refreshed);
          props.onVoted?.(refreshed);
        }
      }
    } catch {
      // Revert on failure
      setLocalPoll(currentPoll);
    } finally {
      setIsVoting(false);
    }
  };

  return (
    <div class={styles.pollContainer}>
      {/* Question */}
      <p data-testid="poll-question" class={styles.pollQuestion}>{poll().question}</p>

      {/* Options */}
      <Flexbox direction="vertical" gap={0.5} class={styles.optionList}>
        <For each={poll().options}>
          {(option, index) => (
            <PollOptionRow
              option={option}
              index={index()}
              totalVotes={poll().totalVotes}
              voted={hasVotedForOption(option.id)}
              isClosed={isClosed()}
              isVoting={isVoting()}
              onVote={handleVote}
            />
          )}
        </For>
      </Flexbox>

      {/* Footer */}
      <PollFooter
        totalVotes={poll().totalVotes}
        allowMultiSelect={poll().allowMultiSelect}
        isClosed={isClosed()}
        expiresAt={poll().expiresAt}
        canEnd={props.canEnd}
        isEndingPoll={isEndingPoll()}
        onEndPoll={handleEndPoll}
      />

      <EndPollConfirmModal
        open={showEndConfirm()}
        isEndingPoll={isEndingPoll()}
        onCancel={() => setShowEndConfirm(false)}
        onConfirm={confirmEndPoll}
      />
    </div>
  );
}
