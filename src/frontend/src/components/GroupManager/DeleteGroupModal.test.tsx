import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import DeleteGroupModal from './DeleteGroupModal';

describe('DeleteGroupModal', () => {
  it('renders nothing when open is false', () => {
    const { queryByRole } = render(() => (
      <DeleteGroupModal
        open={false}
        groupName="Admins"
        isDeleting={false}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />
    ));
    expect(queryByRole('alertdialog')).toBeNull();
  });

  it('renders the alertdialog with title when open', () => {
    const { getByRole, getAllByText } = render(() => (
      <DeleteGroupModal
        open={true}
        groupName="Admins"
        isDeleting={false}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />
    ));
    expect(getByRole('alertdialog')).toBeInTheDocument();
    // 'Delete Group' appears in the modal title; 'Delete' alone is the confirm button.
    expect(getAllByText('Delete Group').length).toBeGreaterThan(0);
  });

  it('embeds the group name in the confirmation prompt', () => {
    const { container } = render(() => (
      <DeleteGroupModal
        open={true}
        groupName="Helpers"
        isDeleting={false}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />
    ));
    expect(container.textContent).toContain('"Helpers"');
  });

  it('invokes onConfirm when the Delete button is clicked', () => {
    const onConfirm = vi.fn();
    const { getByText } = render(() => (
      <DeleteGroupModal
        open={true}
        groupName="Admins"
        isDeleting={false}
        onClose={vi.fn()}
        onConfirm={onConfirm}
      />
    ));
    fireEvent.click(getByText('Delete'));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('invokes onClose when the Cancel button is clicked', () => {
    const onClose = vi.fn();
    const { getByText } = render(() => (
      <DeleteGroupModal
        open={true}
        groupName="Admins"
        isDeleting={false}
        onClose={onClose}
        onConfirm={vi.fn()}
      />
    ));
    fireEvent.click(getByText('Cancel'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('disables the Delete button and shows Deleting... while isDeleting is true', () => {
    const { getByText } = render(() => (
      <DeleteGroupModal
        open={true}
        groupName="Admins"
        isDeleting={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />
    ));
    const btn = getByText('Deleting...') as HTMLButtonElement;
    expect(btn).toBeDisabled();
  });
});
