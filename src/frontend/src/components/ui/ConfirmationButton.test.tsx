import { describe, it, expect, vi } from 'vitest';
import { createSignal } from 'solid-js';
import { render, fireEvent } from '@solidjs/testing-library';
import ConfirmationButton from './ConfirmationButton';

describe('ConfirmationButton', () => {
  function makeProps(overrides = {}) {
    return {
      isConfirming: false,
      onStartConfirm: vi.fn(),
      onConfirm: vi.fn(),
      onCancel: vi.fn(),
      ...overrides,
    };
  }

  it('renders label on initial button', () => {
    const { getByRole } = render(() => <ConfirmationButton {...makeProps({ label: 'Remove' })} />);
    expect(getByRole('button')).toHaveTextContent('Remove');
  });

  it('defaults label to Delete', () => {
    const { getByRole } = render(() => <ConfirmationButton {...makeProps()} />);
    expect(getByRole('button')).toHaveTextContent('Delete');
  });

  it('calls onStartConfirm when initial button clicked', () => {
    const onStartConfirm = vi.fn();
    const { getByRole } = render(() => <ConfirmationButton {...makeProps({ onStartConfirm })} />);
    fireEvent.click(getByRole('button'));
    expect(onStartConfirm).toHaveBeenCalledOnce();
  });

  it('shows confirm prompt when isConfirming is true', () => {
    const { getByText } = render(() => (
      <ConfirmationButton {...makeProps({ isConfirming: true, confirmText: 'Sure?' })} />
    ));
    expect(getByText('Sure?')).toBeInTheDocument();
    expect(getByText('Cancel')).toBeInTheDocument();
    expect(getByText('Confirm')).toBeInTheDocument();
  });

  it('fires onConfirm when Confirm clicked', () => {
    const onConfirm = vi.fn();
    const { getByText } = render(() => (
      <ConfirmationButton {...makeProps({ isConfirming: true, onConfirm })} />
    ));
    fireEvent.click(getByText('Confirm'));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it('fires onCancel when Cancel clicked', () => {
    const onCancel = vi.fn();
    const { getByText } = render(() => (
      <ConfirmationButton {...makeProps({ isConfirming: true, onCancel })} />
    ));
    fireEvent.click(getByText('Cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('disables Confirm and shows loading text while isLoading', () => {
    const { getByText } = render(() => (
      <ConfirmationButton {...makeProps({ isConfirming: true, isLoading: true, label: 'Delete' })} />
    ));
    const confirm = getByText('Delete...');
    expect(confirm).toBeDisabled();
  });

  it('uses testId for initial and confirm buttons', () => {
    const [confirming, setConfirming] = createSignal(false);
    const { getByTestId } = render(() => (
      <ConfirmationButton {...makeProps({ testId: 'remove-x' })} isConfirming={confirming()} />
    ));
    expect(getByTestId('remove-x')).toBeInTheDocument();
    setConfirming(true);
    expect(getByTestId('remove-x-confirm')).toBeInTheDocument();
  });
});
