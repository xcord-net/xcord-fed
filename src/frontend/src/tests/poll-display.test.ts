import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { Poll, PollOption } from '../components/PollDisplay';
import {
  votePercentage,
  toggleVoteSingleSelect,
  toggleVoteMultiSelect,
  isPollClosed,
  validatePollForm,
} from '../components/PollDisplay';

// ---- Test data ----

const makeOption = (overrides: Partial<PollOption> = {}): PollOption => ({
  id: 'opt-1',
  text: 'Option A',
  voteCount: 0,
  ...overrides,
});

const makePoll = (overrides: Partial<Poll> = {}): Poll => ({
  question: 'What is your favourite language?',
  options: [
    makeOption({ id: 'opt-1', text: 'TypeScript', voteCount: 10 }),
    makeOption({ id: 'opt-2', text: 'Rust', voteCount: 5 }),
    makeOption({ id: 'opt-3', text: 'Go', voteCount: 2 }),
  ],
  allowMultiSelect: false,
  totalVotes: 17,
  userVotedOptionIds: [],
  isClosed: false,
  ...overrides,
});

// ---- Tests ----

describe('PollDisplay', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Poll rendering ----

  // ---- Progress bar percentages ----

  describe('vote percentage calculation', () => {
    it('calculates correct percentage for an option', () => {
      // Arrange
      const option = makeOption({ voteCount: 5 });

      // Act
      const pct = votePercentage(option, 10);

      // Assert
      expect(pct).toBe(50);
    });

    it('rounds to nearest integer', () => {
      // Arrange
      const option = makeOption({ voteCount: 1 });

      // Act
      const pct = votePercentage(option, 3);

      // Assert - 1/3 = 33.33...% → 33
      expect(pct).toBe(33);
    });

    it('returns 0 when total votes is 0', () => {
      // Arrange
      const option = makeOption({ voteCount: 0 });

      // Act
      const pct = votePercentage(option, 0);

      // Assert
      expect(pct).toBe(0);
    });

    it('returns 100 when option has all votes', () => {
      // Arrange
      const option = makeOption({ voteCount: 10 });

      // Act
      const pct = votePercentage(option, 10);

      // Assert
      expect(pct).toBe(100);
    });
  });

  // ---- Single-select vote toggle ----

  describe('single-select vote toggle', () => {
    it('adds a vote when user has not voted for the option', () => {
      // Arrange
      const poll = makePoll({ userVotedOptionIds: [] });

      // Act
      const { newVotedIds, totalVotesDelta } = toggleVoteSingleSelect(poll, 'opt-1');

      // Assert
      expect(newVotedIds).toEqual(['opt-1']);
      expect(totalVotesDelta).toBe(1);
    });

    it('removes vote when user has already voted for the option', () => {
      // Arrange
      const poll = makePoll({ userVotedOptionIds: ['opt-1'] });

      // Act
      const { newVotedIds, totalVotesDelta } = toggleVoteSingleSelect(poll, 'opt-1');

      // Assert
      expect(newVotedIds).toEqual([]);
      expect(totalVotesDelta).toBe(-1);
    });

    it('switching from one option to another clears previous vote', () => {
      // Arrange - user voted for opt-1, now clicking opt-2
      const poll = makePoll({ userVotedOptionIds: ['opt-1'] });

      // Act - single-select: clicking opt-2 when opt-1 is voted → opt-1 stays, opt-2 gets added
      // The component re-renders both options; here we just test the new vote for opt-2
      const { newVotedIds } = toggleVoteSingleSelect(poll, 'opt-2');

      // Assert
      expect(newVotedIds).toEqual(['opt-2']);
    });
  });

  // ---- Multi-select vote toggle ----

  describe('multi-select mode', () => {
    it('adds a vote in multi-select mode', () => {
      // Arrange
      const poll = makePoll({ allowMultiSelect: true, userVotedOptionIds: ['opt-1'] });

      // Act
      const { newVotedIds } = toggleVoteMultiSelect(poll, 'opt-2');

      // Assert - both opt-1 and opt-2 are selected
      expect(newVotedIds).toContain('opt-1');
      expect(newVotedIds).toContain('opt-2');
    });

    it('removes a vote in multi-select mode when already voted', () => {
      // Arrange
      const poll = makePoll({
        allowMultiSelect: true,
        userVotedOptionIds: ['opt-1', 'opt-2'],
      });

      // Act
      const { newVotedIds, totalVotesDelta } = toggleVoteMultiSelect(poll, 'opt-1');

      // Assert
      expect(newVotedIds).not.toContain('opt-1');
      expect(newVotedIds).toContain('opt-2');
      expect(totalVotesDelta).toBe(-1);
    });

  });

  // ---- Already voted state ----


  // ---- Poll closed / expired state ----

  describe('poll closed state', () => {
    it('isClosed flag prevents further voting', () => {
      // Arrange
      const poll = makePoll({ isClosed: true });

      // Assert
      expect(isPollClosed(poll)).toBe(true);
    });

    it('expired poll is treated as closed', () => {
      // Arrange
      const poll = makePoll({
        isClosed: false,
        expiresAt: new Date(Date.now() - 1000).toISOString(), // 1 second ago
      });

      // Assert
      expect(isPollClosed(poll)).toBe(true);
    });

    it('poll with future expiry is not closed', () => {
      // Arrange
      const poll = makePoll({
        isClosed: false,
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(), // 1 hour from now
      });

      // Assert
      expect(isPollClosed(poll)).toBe(false);
    });

    it('poll with no expiry and isClosed=false is open', () => {
      // Arrange
      const poll = makePoll({ isClosed: false, expiresAt: undefined });

      // Assert
      expect(isPollClosed(poll)).toBe(false);
    });
  });

  // ---- Create poll form validation ----

  describe('create poll form validation', () => {
    it('validation passes with question and 2+ filled options', () => {
      // Arrange
      const question = 'Favourite colour?';
      const options = ['Red', 'Blue'];

      // Act
      const error = validatePollForm(question, options);

      // Assert
      expect(error).toBeNull();
    });

    it('validation fails when question is empty', () => {
      // Arrange
      const question = '';
      const options = ['Red', 'Blue'];

      // Act
      const error = validatePollForm(question, options);

      // Assert
      expect(error).toBe('Please enter a question.');
    });

    it('validation fails when fewer than 2 options are filled', () => {
      // Arrange
      const question = 'Is this valid?';
      const options = ['Only one option', ''];

      // Act
      const error = validatePollForm(question, options);

      // Assert
      expect(error).toBe('Please provide at least 2 options.');
    });

    it('validation fails when all options are blank', () => {
      // Arrange
      const question = 'Question?';
      const options = ['', '   '];

      // Act
      const error = validatePollForm(question, options);

      // Assert
      expect(error).toBe('Please provide at least 2 options.');
    });

    it('whitespace-only question is treated as empty', () => {
      // Arrange
      const question = '   ';
      const options = ['Red', 'Blue'];

      // Act
      const error = validatePollForm(question, options);

      // Assert
      expect(error).toBe('Please enter a question.');
    });
  });

  // ---- Vote API call ----

  describe('vote API', () => {
    it('vote PUT request hits correct URL with optionId and action', async () => {
      // Arrange
      const conversationId = 'conv-1';
      const messageId = 'msg-1';
      const optionId = 'opt-2';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => makePoll({ userVotedOptionIds: [optionId] }),
      });

      // Act
      const updated = await api.put<Poll>(
        `/api/v1/conversations/${conversationId}/messages/${messageId}/polls/vote`,
        { optionId, action: 'add' },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/conversations/${conversationId}/messages/${messageId}/polls/vote`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ optionId, action: 'add' }),
        }),
      );
      expect(updated.userVotedOptionIds).toContain(optionId);
    });
  });
});
