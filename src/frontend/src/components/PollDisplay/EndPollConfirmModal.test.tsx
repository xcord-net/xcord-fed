import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import EndPollConfirmModal from './EndPollConfirmModal';

function baseProps(overrides: Partial<Parameters<typeof EndPollConfirmModal>[0]> = {}) {
  return {
    open: true,
    isEndingPoll: false,
    onCancel: vi.fn(),
    onConfirm: vi.fn(),
    ...overrides,
  };
}

describe('EndPollConfirmModal', () => {
  it('renders without crashing when open', () => {
    const { getByRole } = render(() => <EndPollConfirmModal {...baseProps()} />);
    expect(getByRole('alertdialog')).toBeInTheDocument();
  });

  it('renders the End Poll title and confirmation text', () => {
    const { container } = render(() => <EndPollConfirmModal {...baseProps()} />);
    expect(container.textContent).toContain('End Poll');
    expect(container.textContent).toContain('Voting will be disabled');
  });

  it('does not render dialog content when open is false', () => {
    const { queryByRole } = render(() => <EndPollConfirmModal {...baseProps({ open: false })} />);
    expect(queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('invokes onCancel when the Cancel button is clicked', () => {
    const onCancel = vi.fn();
    const { getAllByRole } = render(() => <EndPollConfirmModal {...baseProps({ onCancel })} />);
    const cancelBtn = getAllByRole('button').find((b) => b.textContent === 'Cancel')!;
    fireEvent.click(cancelBtn);
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('invokes onConfirm when the End Poll button is clicked', () => {
    const onConfirm = vi.fn();
    const { getAllByRole } = render(() => <EndPollConfirmModal {...baseProps({ onConfirm })} />);
    const confirmBtn = getAllByRole('button').find((b) => b.textContent === 'End Poll')!;
    fireEvent.click(confirmBtn);
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('disables the End Poll button while isEndingPoll is true', () => {
    const { getAllByRole } = render(() => (
      <EndPollConfirmModal {...baseProps({ isEndingPoll: true })} />
    ));
    const confirmBtn = getAllByRole('button').find((b) => b.textContent === 'End Poll') as HTMLButtonElement;
    expect(confirmBtn).toBeDisabled();
  });
});
