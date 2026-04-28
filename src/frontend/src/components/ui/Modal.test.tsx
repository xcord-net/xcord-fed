import { describe, it, expect, vi } from 'vitest';
import { createSignal } from 'solid-js';
import { render, fireEvent } from '@solidjs/testing-library';
import Modal from './Modal';

describe('Modal', () => {
  it('does not render content when closed', () => {
    const { queryByTestId } = render(() => (
      <Modal open={false} onClose={() => {}} data-testid="m1">content</Modal>
    ));
    expect(queryByTestId('m1')).toBeNull();
  });

  it('renders dialog when open with title', () => {
    const { getByTestId, getByText } = render(() => (
      <Modal open={true} onClose={() => {}} title="Hello" data-testid="m2">
        <p>body content</p>
      </Modal>
    ));
    const dialog = getByTestId('m2');
    expect(dialog).toBeInTheDocument();
    expect(dialog.getAttribute('role')).toBe('dialog');
    expect(getByText('Hello')).toBeInTheDocument();
    expect(getByText('body content')).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    const { getByLabelText } = render(() => (
      <Modal open={true} onClose={onClose} title="X">child</Modal>
    ));
    fireEvent.click(getByLabelText('Close dialog'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose when backdrop clicked', () => {
    const onClose = vi.fn();
    const { getByTestId } = render(() => (
      <Modal open={true} onClose={onClose} data-testid="m3">x</Modal>
    ));
    const dialog = getByTestId('m3');
    const backdrop = dialog.parentElement!;
    fireEvent.click(backdrop);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('uses alertdialog role when role prop is alertdialog', () => {
    const { getByTestId } = render(() => (
      <Modal open={true} onClose={() => {}} role="alertdialog" data-testid="m4">x</Modal>
    ));
    expect(getByTestId('m4').getAttribute('role')).toBe('alertdialog');
  });

  it('reacts to open prop changes', () => {
    const [open, setOpen] = createSignal(false);
    const { queryByTestId } = render(() => (
      <Modal open={open()} onClose={() => {}} data-testid="m5">x</Modal>
    ));
    expect(queryByTestId('m5')).toBeNull();
    setOpen(true);
    expect(queryByTestId('m5')).not.toBeNull();
  });
});
