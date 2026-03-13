import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';

// ---- Types ----

export interface PollOption {
  id: string;
  text: string;
  voteCount: number;
}

export interface Poll {
  question: string;
  options: PollOption[];
  allowMultiSelect: boolean;
  totalVotes: number;
  /** IDs of options the current user has voted for */
  userVotedOptionIds: string[];
  /** ISO 8601 expiry timestamp, or null if no expiry */
  expiresAt?: string;
  /** True if the poll is closed */
  isClosed: boolean;
}

interface PollDisplayProps {
  pollId: string;
  poll: Poll;
  /** Whether the current user can end this poll (is creator or has ManageMessages) */
  canEnd?: boolean;
  /** Called after a successful vote so the parent can refresh poll data */
  onVoted?: (updatedPoll: Poll) => void;
}

// ---- API helper ----

/** Fetch current poll state from the backend and map to frontend Poll type. */
async function fetchPollState(pollId: string): Promise<Poll | null> {
  try {
    const data = await api.get<{
      id: string;
      question: string;
      allowMultipleAnswers: boolean;
      isClosed: boolean;
      expiresAt?: string;
      options: Array<{ id: string; text: string; voteCount: number }>;
      userVotes?: string[];
    }>(`/api/v1/polls/${pollId}`);

    const totalVotes = data.options.reduce((sum, o) => sum + o.voteCount, 0);
    return {
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
    };
  } catch {
    return null;
  }
}

// ---- Helper ----

function votePercentage(option: PollOption, totalVotes: number): number {
  if (totalVotes === 0) return 0;
  return Math.round((option.voteCount / totalVotes) * 100);
}

// ---- Component ----

export default function PollDisplay(props: PollDisplayProps) {
  const [isVoting, setIsVoting] = createSignal(false);
  const [isEndingPoll, setIsEndingPoll] = createSignal(false);
  const [showEndConfirm, setShowEndConfirm] = createSignal(false);
  const [localPoll, setLocalPoll] = createSignal<Poll>(props.poll);

  // Keep in sync if parent passes a new poll (e.g. after SignalR update)
  // We use a simple approach: whenever props.poll reference changes, we sync.
  // SolidJS tracks fine-grained, so we just read props.poll inline in the JSX.
  const poll = () => localPoll();

  const hasVoted = () => poll().userVotedOptionIds.length > 0;
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
    <div class="mt-2 bg-xcord-bg-tertiary rounded-lg p-4 max-w-md">
      {/* Question */}
      <p class="text-xcord-text-primary font-semibold text-sm mb-3">{poll().question}</p>

      {/* Options */}
      <div class="space-y-2">
        <For each={poll().options}>
          {(option) => {
            const pct = () => votePercentage(option, poll().totalVotes);
            const voted = () => hasVotedForOption(option.id);

            return (
              <div class="relative">
                <button
                  class={`w-full text-left rounded overflow-hidden transition-colors border ${
                    voted()
                      ? 'border-xcord-brand'
                      : 'border-xcord-bg-primary hover:border-xcord-brand/40'
                  } ${isClosed() ? 'cursor-default' : 'cursor-pointer'}`}
                  onClick={() => !isClosed() && handleVote(option.id)}
                  disabled={isClosed() || isVoting()}
                  aria-pressed={voted()}
                  aria-label={`Vote for ${option.text}`}
                >
                  {/* Progress bar background */}
                  <div
                    class={`absolute inset-0 transition-all duration-300 ${
                      voted() ? 'bg-xcord-brand/20' : 'bg-xcord-bg-primary/60'
                    }`}
                    style={{ width: `${pct()}%` }}
                  />

                  {/* Content row */}
                  <div class="relative flex items-center justify-between px-3 py-2 gap-2">
                    <div class="flex items-center gap-2 min-w-0">
                      <Show when={voted()}>
                        <span class="text-xcord-brand text-xs flex-shrink-0">✓</span>
                      </Show>
                      <span class="text-xcord-text-primary text-sm truncate">{option.text}</span>
                    </div>
                    <span class="text-xcord-text-muted text-xs flex-shrink-0 font-medium">
                      {pct()}%
                    </span>
                  </div>
                </button>
              </div>
            );
          }}
        </For>
      </div>

      {/* Footer */}
      <div class="mt-3 flex items-center justify-between gap-2">
        <span class="text-xcord-text-muted text-xs">
          {poll().totalVotes} {poll().totalVotes === 1 ? 'vote' : 'votes'}
          <Show when={poll().allowMultiSelect}>
            <span class="ml-1">(multi-select)</span>
          </Show>
        </span>

        <div class="flex items-center gap-2">
          <Show when={isClosed()}>
            <span
              class="text-xcord-text-muted text-xs bg-xcord-bg-primary px-2 py-0.5 rounded"
              aria-label="Poll closed"
            >
              Closed
            </span>
          </Show>

          <Show when={!isClosed() && poll().expiresAt}>
            <span class="text-xcord-text-muted text-xs">
              Ends {new Date(poll().expiresAt!).toLocaleDateString()}
            </span>
          </Show>

          <Show when={!isClosed() && props.canEnd}>
            <button
              class="text-xcord-text-muted hover:text-red-400 text-xs px-2 py-0.5 rounded border border-xcord-bg-primary hover:border-red-400/40 transition-colors disabled:opacity-50"
              onClick={handleEndPoll}
              disabled={isEndingPoll()}
              aria-label="End poll"
            >
              End Poll
            </button>
          </Show>
        </div>
      </div>

      <Modal
        open={showEndConfirm()}
        onClose={() => setShowEndConfirm(false)}
        title="End Poll"
        size="sm"
        role="alertdialog"
      >
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-4">End this poll? Voting will be disabled and no further votes can be cast.</p>
          <div class="flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm text-xcord-text-primary bg-xcord-bg-primary hover:bg-xcord-bg-tertiary rounded transition-colors"
              onClick={() => setShowEndConfirm(false)}
            >
              Cancel
            </button>
            <button
              class="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded transition-colors disabled:opacity-50"
              disabled={isEndingPoll()}
              onClick={confirmEndPoll}
            >
              End Poll
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

// ---- Poll creation form (used inside MessageCompose area) ----

interface PollOption_Draft {
  id: string;
  text: string;
}

interface CreatePollFormProps {
  onSubmit: (pollData: {
    question: string;
    options: string[];
    allowMultiSelect: boolean;
    durationHours?: number;
  }) => void;
  onCancel: () => void;
}

export function CreatePollForm(props: CreatePollFormProps) {
  const [question, setQuestion] = createSignal('');
  const [options, setOptions] = createSignal<PollOption_Draft[]>([
    { id: '1', text: '' },
    { id: '2', text: '' },
  ]);
  const [allowMultiSelect, setAllowMultiSelect] = createSignal(false);
  const [durationHours, setDurationHours] = createSignal<number | undefined>(undefined);
  const [validationError, setValidationError] = createSignal<string | null>(null);

  let nextId = 3;

  const addOption = () => {
    if (options().length >= 10) return;
    setOptions([...options(), { id: String(nextId++), text: '' }]);
  };

  const removeOption = (id: string) => {
    if (options().length <= 2) return;
    setOptions(options().filter((o) => o.id !== id));
  };

  const updateOption = (id: string, text: string) => {
    setOptions(options().map((o) => (o.id === id ? { ...o, text } : o)));
  };

  const handleSubmit = () => {
    const q = question().trim();
    if (!q) {
      setValidationError('Please enter a question.');
      return;
    }
    const filledOptions = options().map((o) => o.text.trim()).filter(Boolean);
    if (filledOptions.length < 2) {
      setValidationError('Please provide at least 2 options.');
      return;
    }
    setValidationError(null);
    props.onSubmit({
      question: q,
      options: filledOptions,
      allowMultiSelect: allowMultiSelect(),
      durationHours: durationHours(),
    });
  };

  return (
    <div class="bg-xcord-bg-primary rounded-lg p-4 space-y-3 border border-xcord-bg-tertiary">
      <h4 class="text-xcord-text-primary font-semibold text-sm">Create Poll</h4>

      {/* Question */}
      <div>
        <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
          Question
        </label>
        <input
          type="text"
          class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
          placeholder="Ask a question..."
          value={question()}
          onInput={(e) => setQuestion(e.currentTarget.value)}
        />
      </div>

      {/* Options */}
      <div>
        <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
          Options ({options().length}/10)
        </label>
        <div class="space-y-2">
          <For each={options()}>
            {(opt, index) => (
              <div class="flex items-center gap-2">
                <input
                  type="text"
                  class="flex-1 bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                  placeholder={`Option ${index() + 1}`}
                  value={opt.text}
                  onInput={(e) => updateOption(opt.id, e.currentTarget.value)}
                />
                <Show when={options().length > 2}>
                  <button
                    class="text-xcord-text-muted hover:text-red-400 transition-colors text-xs px-1"
                    onClick={() => removeOption(opt.id)}
                    aria-label={`Remove option ${index() + 1}`}
                  >
                    ✕
                  </button>
                </Show>
              </div>
            )}
          </For>
        </div>
        <Show when={options().length < 10}>
          <button
            class="mt-2 text-xcord-brand hover:underline text-xs"
            onClick={addOption}
          >
            + Add option
          </button>
        </Show>
      </div>

      {/* Settings row */}
      <div class="flex items-center gap-6">
        {/* Multi-select toggle */}
        <label class="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            class="accent-xcord-brand"
            checked={allowMultiSelect()}
            onChange={(e) => setAllowMultiSelect(e.currentTarget.checked)}
          />
          <span class="text-xcord-text-muted text-xs">Allow multiple selections</span>
        </label>

        {/* Duration */}
        <div class="flex items-center gap-2">
          <label class="text-xcord-text-muted text-xs">Duration</label>
          <select
            class="bg-xcord-bg-tertiary text-xcord-text-primary text-xs rounded px-2 py-1 outline-none focus:ring-1 focus:ring-xcord-brand"
            value={durationHours() ?? ''}
            onChange={(e) => {
              const v = e.currentTarget.value;
              setDurationHours(v === '' ? undefined : Number(v));
            }}
          >
            <option value="">No limit</option>
            <option value="1">1 hour</option>
            <option value="6">6 hours</option>
            <option value="12">12 hours</option>
            <option value="24">24 hours</option>
            <option value="72">3 days</option>
            <option value="168">1 week</option>
          </select>
        </div>
      </div>

      <Show when={validationError()}>
        <p class="text-red-400 text-xs">{validationError()}</p>
      </Show>

      <div class="flex gap-2">
        <button
          class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors"
          onClick={handleSubmit}
        >
          Add Poll
        </button>
        <button
          class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
          onClick={props.onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}

export function toggleVoteSingleSelect(
  poll: Poll,
  optionId: string,
): { newVotedIds: string[]; totalVotesDelta: number } {
  const alreadyVoted = poll.userVotedOptionIds.includes(optionId);
  const newVotedIds = alreadyVoted ? [] : [optionId];
  const totalVotesDelta = alreadyVoted ? -1 : 1;
  return { newVotedIds, totalVotesDelta };
}

export function toggleVoteMultiSelect(
  poll: Poll,
  optionId: string,
): { newVotedIds: string[]; totalVotesDelta: number } {
  const alreadyVoted = poll.userVotedOptionIds.includes(optionId);
  const newVotedIds = alreadyVoted
    ? poll.userVotedOptionIds.filter((id) => id !== optionId)
    : [...poll.userVotedOptionIds, optionId];
  const totalVotesDelta = alreadyVoted ? -1 : 1;
  return { newVotedIds, totalVotesDelta };
}

export function isPollClosed(poll: Poll): boolean {
  if (poll.isClosed) return true;
  if (!poll.expiresAt) return false;
  return new Date(poll.expiresAt).getTime() < Date.now();
}

export function validatePollForm(question: string, options: string[]): string | null {
  if (!question.trim()) return 'Please enter a question.';
  const filled = options.filter((o) => o.trim().length > 0);
  if (filled.length < 2) return 'Please provide at least 2 options.';
  return null;
}

export { votePercentage };
