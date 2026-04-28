import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import PollOptionRow from './PollOptionRow';
import type { PollOption } from './helpers';

function makeOption(overrides: Partial<PollOption> = {}): PollOption {
  return {
    id: 'opt-1',
    text: 'TypeScript',
    voteCount: 5,
    ...overrides,
  };
}

function baseProps(overrides: Partial<Parameters<typeof PollOptionRow>[0]> = {}) {
  return {
    option: makeOption(),
    index: 0,
    totalVotes: 10,
    voted: false,
    isClosed: false,
    isVoting: false,
    onVote: vi.fn(),
    ...overrides,
  };
}

describe('PollOptionRow', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <PollOptionRow {...baseProps()} />);
    expect(getByTestId('poll-option-0')).toBeInTheDocument();
  });

  it('renders the option text and computed percentage', () => {
    const { getByTestId, container } = render(() => (
      <PollOptionRow {...baseProps({ option: makeOption({ text: 'Rust', voteCount: 5 }), totalVotes: 10 })} />
    ));
    expect(container.textContent).toContain('Rust');
    expect(getByTestId('poll-option-0-count')).toHaveTextContent('50%');
  });

  it('shows the voted check indicator when voted is true', () => {
    const { getByTestId } = render(() => <PollOptionRow {...baseProps({ voted: true })} />);
    expect(getByTestId('poll-option-0-voted')).toBeInTheDocument();
  });

  it('invokes onVote with the option id when clicked', () => {
    const onVote = vi.fn();
    const { getByTestId } = render(() => (
      <PollOptionRow {...baseProps({ option: makeOption({ id: 'opt-42' }), onVote })} />
    ));
    fireEvent.click(getByTestId('poll-option-0'));
    expect(onVote).toHaveBeenCalledWith('opt-42');
  });

  it('disables the button and does not invoke onVote when isClosed is true', () => {
    const onVote = vi.fn();
    const { getByTestId } = render(() => (
      <PollOptionRow {...baseProps({ isClosed: true, onVote })} />
    ));
    const btn = getByTestId('poll-option-0') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    fireEvent.click(btn);
    expect(onVote).not.toHaveBeenCalled();
  });

  it('renders 0% when totalVotes is zero', () => {
    const { getByTestId } = render(() => (
      <PollOptionRow {...baseProps({ option: makeOption({ voteCount: 0 }), totalVotes: 0 })} />
    ));
    expect(getByTestId('poll-option-0-count')).toHaveTextContent('0%');
  });
});
