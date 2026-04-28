import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import CreatePollForm from './CreatePollForm';

function baseProps(overrides: Partial<Parameters<typeof CreatePollForm>[0]> = {}) {
  return {
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    ...overrides,
  };
}

describe('CreatePollForm', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <CreatePollForm {...baseProps()} />);
    expect(getByTestId('poll-question-input')).toBeInTheDocument();
  });

  it('renders the title, two default option inputs, submit and cancel buttons', () => {
    const { getByTestId, container } = render(() => <CreatePollForm {...baseProps()} />);
    expect(container.textContent).toContain('Create Poll');
    expect(getByTestId('poll-option-input-0')).toBeInTheDocument();
    expect(getByTestId('poll-option-input-1')).toBeInTheDocument();
    expect(getByTestId('poll-submit-button')).toBeInTheDocument();
    expect(getByTestId('poll-cancel-button')).toBeInTheDocument();
  });

  it('shows validation error when submitting without a question', () => {
    const onSubmit = vi.fn();
    const { getByTestId, container } = render(() => <CreatePollForm {...baseProps({ onSubmit })} />);
    fireEvent.click(getByTestId('poll-submit-button'));
    expect(container.textContent).toContain('Please enter a question.');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('shows validation error when fewer than 2 options are filled', () => {
    const onSubmit = vi.fn();
    const { getByTestId, container } = render(() => <CreatePollForm {...baseProps({ onSubmit })} />);
    fireEvent.input(getByTestId('poll-question-input'), { target: { value: 'Best language?' } });
    fireEvent.input(getByTestId('poll-option-input-0'), { target: { value: 'TypeScript' } });
    fireEvent.click(getByTestId('poll-submit-button'));
    expect(container.textContent).toContain('Please provide at least 2 options.');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('invokes onSubmit with collected poll data when valid', () => {
    const onSubmit = vi.fn();
    const { getByTestId } = render(() => <CreatePollForm {...baseProps({ onSubmit })} />);
    fireEvent.input(getByTestId('poll-question-input'), { target: { value: 'Best language?' } });
    fireEvent.input(getByTestId('poll-option-input-0'), { target: { value: 'TypeScript' } });
    fireEvent.input(getByTestId('poll-option-input-1'), { target: { value: 'Rust' } });
    fireEvent.click(getByTestId('poll-submit-button'));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit).toHaveBeenCalledWith({
      question: 'Best language?',
      options: ['TypeScript', 'Rust'],
      allowMultiSelect: false,
      durationHours: undefined,
    });
  });

  it('adds a new option input when "Add option" is clicked', () => {
    const { getByTestId, queryByTestId } = render(() => <CreatePollForm {...baseProps()} />);
    expect(queryByTestId('poll-option-input-2')).not.toBeInTheDocument();
    fireEvent.click(getByTestId('poll-add-option-button'));
    expect(queryByTestId('poll-option-input-2')).toBeInTheDocument();
  });

  it('invokes onCancel when the Cancel button is clicked', () => {
    const onCancel = vi.fn();
    const { getByTestId } = render(() => <CreatePollForm {...baseProps({ onCancel })} />);
    fireEvent.click(getByTestId('poll-cancel-button'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
