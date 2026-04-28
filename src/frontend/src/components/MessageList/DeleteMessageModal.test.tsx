import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import DeleteMessageModal from './DeleteMessageModal';

function baseProps(overrides: Partial<Parameters<typeof DeleteMessageModal>[0]> = {}) {
  return {
    open: true,
    onClose: vi.fn(),
    onConfirm: vi.fn(),
    ...overrides,
  };
}

describe('DeleteMessageModal', () => {
  it('renders without crashing when open', () => {
    const { getByTestId } = render(() => <DeleteMessageModal {...baseProps()} />);
    expect(getByTestId('delete-message-dialog')).toBeInTheDocument();
  });

  it('renders confirmation copy when open', () => {
    const { getByText } = render(() => <DeleteMessageModal {...baseProps()} />);
    expect(
      getByText('Are you sure you want to delete this message? This cannot be undone.'),
    ).toBeInTheDocument();
  });

  it('does not render dialog content when open is false', () => {
    const { queryByTestId } = render(() => (
      <DeleteMessageModal {...baseProps({ open: false })} />
    ));
    expect(queryByTestId('delete-message-dialog')).toBeNull();
  });

  it('invokes onConfirm when the Delete button is clicked', () => {
    const onConfirm = vi.fn();
    const { getByTestId } = render(() => (
      <DeleteMessageModal {...baseProps({ onConfirm })} />
    ));
    fireEvent.click(getByTestId('delete-message-confirm-button'));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('invokes onClose when the Cancel button is clicked', () => {
    const onClose = vi.fn();
    const { getByTestId } = render(() => (
      <DeleteMessageModal {...baseProps({ onClose })} />
    ));
    fireEvent.click(getByTestId('delete-message-cancel-button'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('uses the alertdialog role for assistive tech', () => {
    const { getByRole } = render(() => <DeleteMessageModal {...baseProps()} />);
    expect(getByRole('alertdialog')).toBeInTheDocument();
  });
});
