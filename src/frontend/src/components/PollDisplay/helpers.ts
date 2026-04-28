import { api } from '../../api/client';

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

// ---- API helper ----

/** Fetch current poll state from the backend and map to frontend Poll type. */
export async function fetchPollState(pollId: string): Promise<Poll | null> {
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

// ---- Pure helpers ----

export function votePercentage(option: PollOption, totalVotes: number): number {
  if (totalVotes === 0) return 0;
  return Math.round((option.voteCount / totalVotes) * 100);
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
