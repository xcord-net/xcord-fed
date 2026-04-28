import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import PollFooter from './PollFooter';

function baseProps(overrides: Partial<Parameters<typeof PollFooter>[0]> = {}) {
  return {
    totalVotes: 0,
    allowMultiSelect: false,
    isClosed: false,
    isEndingPoll: false,
    onEndPoll: vi.fn(),
    ...overrides,
  };
}

describe('PollFooter', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <PollFooter {...baseProps()} />);
    expect(getByTestId('poll-total-votes')).toBeInTheDocument();
  });

  it('uses singular "vote" label when totalVotes is 1', () => {
    const { getByTestId } = render(() => <PollFooter {...baseProps({ totalVotes: 1 })} />);
    expect(getByTestId('poll-total-votes')).toHaveTextContent('1 vote');
  });

  it('uses plural "votes" label and shows multi-select note when enabled', () => {
    const { getByTestId, container } = render(() => (
      <PollFooter {...baseProps({ totalVotes: 5, allowMultiSelect: true })} />
    ));
    expect(getByTestId('poll-total-votes')).toHaveTextContent('5 votes');
    expect(container.textContent).toContain('(multi-select)');
  });

  it('shows Closed badge and hides End Poll button when isClosed is true', () => {
    const { container, queryByLabelText } = render(() => (
      <PollFooter {...baseProps({ isClosed: true, canEnd: true })} />
    ));
    expect(queryByLabelText('Poll closed')).toBeInTheDocument();
    expect(queryByLabelText('End poll')).not.toBeInTheDocument();
  });

  it('invokes onEndPoll when the End Poll button is clicked', () => {
    const onEndPoll = vi.fn();
    const { getByLabelText } = render(() => (
      <PollFooter {...baseProps({ canEnd: true, onEndPoll })} />
    ));
    fireEvent.click(getByLabelText('End poll'));
    expect(onEndPoll).toHaveBeenCalledTimes(1);
  });

  it('disables End Poll button while isEndingPoll is true', () => {
    const { getByLabelText } = render(() => (
      <PollFooter {...baseProps({ canEnd: true, isEndingPoll: true })} />
    ));
    const btn = getByLabelText('End poll') as HTMLButtonElement;
    expect(btn).toBeDisabled();
  });
});
