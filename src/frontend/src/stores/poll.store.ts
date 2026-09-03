import { createSignal, createRoot } from 'solid-js';

/** Latest vote counts for a poll, by option id. */
export type PollTally = Record<string, number>;

const store = createRoot(() => {
  const [tallies, setTallies] = createSignal<Record<string, PollTally>>({});
  return { tallies, setTallies };
});

/**
 * Vote counts announced after a poll was rendered.
 *
 * A poll is fetched once, when its message appears, and never again - so
 * without somewhere to put an update, votes only ever moved for whoever
 * reloaded. The server announces the whole tally on every vote and retraction,
 * which is what this holds; the component overlays it on what it fetched.
 */
export function usePolls() {
  return {
    /** The announced tally for a poll, or undefined if none has arrived. */
    tallyFor(pollId: string): PollTally | undefined {
      return store.tallies()[pollId];
    },

    applyTally(pollId: string, options: { id: string; voteCount: number }[]): void {
      const tally: PollTally = {};
      for (const option of options) tally[String(option.id)] = option.voteCount;
      store.setTallies({ ...store.tallies(), [pollId]: tally });
    },

    reset(): void {
      store.setTallies({});
    },
  };
}
