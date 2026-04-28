import { describe, it, expect, vi } from 'vitest';
import { render, waitFor } from '@solidjs/testing-library';
import { mockFetch } from '../../tests/helpers/mockFetch';

// Stub PollDisplay so this test focuses on PollContainer's fetch+map+render logic.
vi.mock('../PollDisplay', () => ({
  default: (p: { pollId: string; poll: { question: string; totalVotes: number } }) => (
    <div data-testid="mock-poll-display" data-poll-id={p.pollId}>
      <span data-testid="mock-poll-question">{p.poll.question}</span>
      <span data-testid="mock-poll-total">{p.poll.totalVotes}</span>
    </div>
  ),
}));

import PollContainer from './PollContainer';

const samplePoll = {
  id: 'p-1',
  question: 'Tabs or spaces?',
  allowMultipleAnswers: false,
  isClosed: false,
  options: [
    { id: 'o-1', text: 'Tabs', voteCount: 3 },
    { id: 'o-2', text: 'Spaces', voteCount: 5 },
  ],
  userVotes: ['o-1'],
};

describe('PollContainer', () => {
  it('renders nothing before the poll has loaded', () => {
    mockFetch({
      'GET /api/v1/polls/p-1': () => new Promise(() => undefined),
    });
    const { queryByTestId } = render(() => <PollContainer pollId="p-1" isAuthor={false} />);
    expect(queryByTestId('mock-poll-display')).toBeNull();
  });

  it('renders PollDisplay after the poll loads', async () => {
    mockFetch({ 'GET /api/v1/polls/p-1': () => ({ status: 200, body: samplePoll }) });
    const { findByTestId } = render(() => <PollContainer pollId="p-1" isAuthor={false} />);
    const display = await findByTestId('mock-poll-display');
    expect(display).toBeInTheDocument();
    expect(display.getAttribute('data-poll-id')).toBe('p-1');
  });

  it('forwards the poll question to PollDisplay', async () => {
    mockFetch({ 'GET /api/v1/polls/p-1': () => ({ status: 200, body: samplePoll }) });
    const { findByTestId } = render(() => <PollContainer pollId="p-1" isAuthor={false} />);
    const q = await findByTestId('mock-poll-question');
    expect(q.textContent).toBe('Tabs or spaces?');
  });

  it('computes totalVotes by summing per-option voteCounts', async () => {
    mockFetch({ 'GET /api/v1/polls/p-1': () => ({ status: 200, body: samplePoll }) });
    const { findByTestId } = render(() => <PollContainer pollId="p-1" isAuthor={false} />);
    const total = await findByTestId('mock-poll-total');
    expect(total.textContent).toBe('8');
  });

  it('renders nothing when the poll fetch errors', async () => {
    mockFetch({ 'GET /api/v1/polls/p-1': () => ({ status: 500, body: { error: 'boom' } }) });
    const { queryByTestId } = render(() => <PollContainer pollId="p-1" isAuthor={false} />);
    // Wait a tick so onMount's promise rejects and the catch branch runs.
    await waitFor(() => {
      expect(queryByTestId('mock-poll-display')).toBeNull();
    });
  });
});
