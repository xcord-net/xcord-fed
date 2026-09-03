import { Show, createMemo, createSignal, onMount } from 'solid-js';
import { api } from '../../api/client';
import { usePolls } from '../../stores/poll.store';
import PollDisplay from '../PollDisplay';
import type { Poll } from '../PollDisplay';

interface PollContainerProps {
  pollId: string;
  isAuthor: boolean;
}

/** Fetches a poll from the backend and renders PollDisplay once loaded. */
export default function PollContainer(props: PollContainerProps) {
  const [poll, setPoll] = createSignal<Poll | null>(null);
  const polls = usePolls();

  /**
   * What was fetched, with any later announcement laid over it.
   *
   * The fetch happens once, when the message appears. Every vote after that
   * arrives as an announcement to the conversation, so the counts on screen
   * come from whichever is newer.
   */
  const displayed = createMemo<Poll | null>(() => {
    const loaded = poll();
    if (!loaded) return null;
    const tally = polls.tallyFor(props.pollId);
    if (!tally) return loaded;
    const options = loaded.options.map((o) => ({
      ...o,
      voteCount: tally[o.id] ?? o.voteCount,
    }));
    return {
      ...loaded,
      options,
      totalVotes: options.reduce((sum, o) => sum + o.voteCount, 0),
    };
  });

  onMount(async () => {
    try {
      const data = await api.get<{
        id: string;
        question: string;
        allowMultipleAnswers: boolean;
        isClosed: boolean;
        expiresAt?: string;
        options: Array<{ id: string; text: string; voteCount: number }>;
        userVotes?: string[];
      }>(`/api/v1/polls/${props.pollId}`);

      const totalVotes = data.options.reduce((sum, o) => sum + o.voteCount, 0);
      setPoll({
        question: data.question,
        options: data.options.map((o) => ({
          id: String(o.id),
          text: o.text,
          voteCount: o.voteCount,
        })),
        allowMultiSelect: data.allowMultipleAnswers,
        totalVotes,
        userVotedOptionIds: (data.userVotes ?? []).map(String),
        expiresAt: data.expiresAt,
        isClosed: data.isClosed,
      });
    } catch {
      // Poll failed to load - render nothing
    }
  });

  return (
    <Show when={displayed()}>
      {(p) => (
        <PollDisplay
          pollId={props.pollId}
          poll={p()}
          canEnd={props.isAuthor}
          onVoted={(updated) => setPoll(updated)}
        />
      )}
    </Show>
  );
}
