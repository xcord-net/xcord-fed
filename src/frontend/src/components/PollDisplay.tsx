import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';
import styles from './PollDisplay.module.css';

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
    <div class={styles.pollContainer}>
      {/* Question */}
      <p data-testid="poll-question" class={styles.pollQuestion}>{poll().question}</p>

      {/* Options */}
      <div class={styles.optionList}>
        <For each={poll().options}>
          {(option, index) => {
            const pct = () => votePercentage(option, poll().totalVotes);
            const voted = () => hasVotedForOption(option.id);

            return (
              <div class={styles.optionWrapper}>
                <button
                  data-testid={`poll-option-${index()}`}
                  classList={{
                    [styles.optionButton]: true,
                    [styles.optionButtonVoted]: voted(),
                    [styles.optionButtonDefault]: !voted() && !isClosed(),
                    [styles.optionButtonClosed]: isClosed(),
                  }}
                  onClick={() => !isClosed() && handleVote(option.id)}
                  disabled={isClosed() || isVoting()}
                  aria-pressed={voted()}
                  aria-label={`Vote for ${option.text}`}
                >
                  {/* Progress bar background */}
                  <div
                    classList={{
                      [styles.optionProgress]: true,
                      [styles.optionProgressVoted]: voted(),
                      [styles.optionProgressDefault]: !voted(),
                    }}
                    style={{ width: `${pct()}%` }}
                  />

                  {/* Content row */}
                  <div class={styles.optionContent}>
                    <div class={styles.optionLabelGroup}>
                      <Show when={voted()}>
                        <span data-testid={`poll-option-${index()}-voted`} class={styles.optionVotedCheck}>✓</span>
                      </Show>
                      <span class={styles.optionText}>{option.text}</span>
                    </div>
                    <span data-testid={`poll-option-${index()}-count`} class={styles.optionPercent}>
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
      <div class={styles.pollFooter}>
        <span data-testid="poll-total-votes" class={styles.totalVotes}>
          {poll().totalVotes} {poll().totalVotes === 1 ? 'vote' : 'votes'}
          <Show when={poll().allowMultiSelect}>
            <span class={styles.multiSelectNote}>(multi-select)</span>
          </Show>
        </span>

        <div class={styles.footerActions}>
          <Show when={isClosed()}>
            <span
              class={styles.closedBadge}
              aria-label="Poll closed"
            >
              Closed
            </span>
          </Show>

          <Show when={!isClosed() && poll().expiresAt}>
            <span class={styles.expiryText}>
              Ends {new Date(poll().expiresAt!).toLocaleDateString()}
            </span>
          </Show>

          <Show when={!isClosed() && props.canEnd}>
            <button
              class={styles.endPollButton}
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
        <div class={styles.confirmModalBody}>
          <p class={styles.confirmModalText}>End this poll? Voting will be disabled and no further votes can be cast.</p>
          <div class={styles.confirmModalActions}>
            <button
              class={styles.confirmCancelButton}
              onClick={() => setShowEndConfirm(false)}
            >
              Cancel
            </button>
            <button
              class={styles.confirmEndButton}
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
    <div class={styles.createPollForm}>
      <h4 class={styles.createPollTitle}>Create Poll</h4>

      {/* Question */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Question
        </label>
        <input
          data-testid="poll-question-input"
          type="text"
          class={styles.textInput}
          placeholder="Ask a question..."
          value={question()}
          onInput={(e) => setQuestion(e.currentTarget.value)}
        />
      </div>

      {/* Options */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Options ({options().length}/10)
        </label>
        <div class={styles.optionInputList}>
          <For each={options()}>
            {(opt, index) => (
              <div class={styles.optionInputRow}>
                <input
                  data-testid={`poll-option-input-${index()}`}
                  type="text"
                  class={styles.optionTextInput}
                  placeholder={`Option ${index() + 1}`}
                  value={opt.text}
                  onInput={(e) => updateOption(opt.id, e.currentTarget.value)}
                />
                <Show when={options().length > 2}>
                  <button
                    class={styles.removeOptionButton}
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
            data-testid="poll-add-option-button"
            class={styles.addOptionButton}
            onClick={addOption}
          >
            + Add option
          </button>
        </Show>
      </div>

      {/* Settings row */}
      <div class={styles.settingsRow}>
        {/* Multi-select toggle */}
        <label class={styles.checkboxLabel}>
          <input
            type="checkbox"
            class={styles.checkboxInput}
            checked={allowMultiSelect()}
            onChange={(e) => setAllowMultiSelect(e.currentTarget.checked)}
          />
          <span class={styles.checkboxText}>Allow multiple selections</span>
        </label>

        {/* Duration */}
        <div class={styles.durationGroup}>
          <label class={styles.durationLabel}>Duration</label>
          <select
            class={styles.durationSelect}
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
        <p class={styles.validationError}>{validationError()}</p>
      </Show>

      <div class={styles.formActions}>
        <button
          data-testid="poll-submit-button"
          class={styles.submitButton}
          onClick={handleSubmit}
        >
          Add Poll
        </button>
        <button
          data-testid="poll-cancel-button"
          class={styles.cancelButton}
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
