import { Show, createSignal, onMount } from 'solid-js';
import { api } from '../../api/client';
import PollDisplay from '../PollDisplay';
import type { Poll } from '../PollDisplay';

interface PollContainerProps {
  pollId: string;
  isAuthor: boolean;
}

/** Fetches a poll from the backend and renders PollDisplay once loaded. */
export default function PollContainer(props: PollContainerProps) {
  const [poll, setPoll] = createSignal<Poll | null>(null);

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
    <Show when={poll()}>
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
