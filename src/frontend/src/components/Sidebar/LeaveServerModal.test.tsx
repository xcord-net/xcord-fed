import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import LeaveServerModal from './LeaveServerModal';

describe('LeaveServerModal', () => {
  it('does not render content when closed', () => {
    const { queryByTestId } = render(() => (
      <LeaveServerModal open={false} onClose={() => {}} onConfirm={() => {}} />
    ));
    expect(queryByTestId('leave-server-dialog')).toBeNull();
    expect(queryByTestId('leave-server-confirm-button')).toBeNull();
  });

  it('renders the dialog with confirmation copy when open', () => {
    const { getByTestId, getByText } = render(() => (
      <LeaveServerModal open={true} onClose={() => {}} onConfirm={() => {}} />
    ));
    expect(getByTestId('leave-server-dialog')).toBeInTheDocument();
    expect(getByText('Are you sure you want to leave this server?')).toBeInTheDocument();
  });

  it('uses the alertdialog role', () => {
    const { getByTestId } = render(() => (
      <LeaveServerModal open={true} onClose={() => {}} onConfirm={() => {}} />
    ));
    expect(getByTestId('leave-server-dialog').getAttribute('role')).toBe('alertdialog');
  });

  it('invokes onClose when the cancel button is clicked', () => {
    const onClose = vi.fn();
    const onConfirm = vi.fn();
    const { getByTestId } = render(() => (
      <LeaveServerModal open={true} onClose={onClose} onConfirm={onConfirm} />
    ));
    fireEvent.click(getByTestId('leave-server-cancel-button'));
    expect(onClose).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('invokes onConfirm when the leave button is clicked', () => {
    const onClose = vi.fn();
    const onConfirm = vi.fn();
    const { getByTestId } = render(() => (
      <LeaveServerModal open={true} onClose={onClose} onConfirm={onConfirm} />
    ));
    fireEvent.click(getByTestId('leave-server-confirm-button'));
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onClose).not.toHaveBeenCalled();
  });
});
