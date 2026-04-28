import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import GroupEditForm from './GroupEditForm';

function baseProps(overrides: Partial<Parameters<typeof GroupEditForm>[0]> = {}) {
  return {
    editName: 'Admins',
    editColor: '#d4943a',
    editRoles: 0,
    isSaving: false,
    saveSuccess: '',
    saveError: '',
    onNameInput: vi.fn(),
    onColorChange: vi.fn(),
    onRolesChange: vi.fn(),
    onDeleteClick: vi.fn(),
    onSubmit: vi.fn(),
    ...overrides,
  };
}

describe('GroupEditForm', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <GroupEditForm {...baseProps()} />);
    expect(getByTestId('group-save-changes-button')).toBeInTheDocument();
  });

  it('renders the Edit Group heading and delete button', () => {
    const { container, getByTestId } = render(() => <GroupEditForm {...baseProps()} />);
    expect(container.textContent).toContain('Edit Group');
    expect(getByTestId('delete-group-button')).toBeInTheDocument();
  });

  it('reflects editName in the name input', () => {
    const { container } = render(() => <GroupEditForm {...baseProps({ editName: 'Moderators' })} />);
    const input = container.querySelector('#edit-group-name') as HTMLInputElement;
    expect(input.value).toBe('Moderators');
  });

  it('invokes onNameInput when name is typed', () => {
    const onNameInput = vi.fn();
    const { container } = render(() => <GroupEditForm {...baseProps({ onNameInput })} />);
    const input = container.querySelector('#edit-group-name') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'Helpers' } });
    expect(onNameInput).toHaveBeenCalledWith('Helpers');
  });

  it('invokes onDeleteClick when delete button is clicked', () => {
    const onDeleteClick = vi.fn();
    const { getByTestId } = render(() => <GroupEditForm {...baseProps({ onDeleteClick })} />);
    fireEvent.click(getByTestId('delete-group-button'));
    expect(onDeleteClick).toHaveBeenCalledTimes(1);
  });

  it('invokes onRolesChange when a permission checkbox is toggled', () => {
    const onRolesChange = vi.fn();
    const { getByTestId } = render(() => <GroupEditForm {...baseProps({ onRolesChange })} />);
    const checkbox = getByTestId('permission-view-channels').querySelector('input')!;
    fireEvent.click(checkbox);
    expect(onRolesChange).toHaveBeenCalledTimes(1);
    // Toggling bit 0 from 0 should produce 1 (1 << 0).
    expect(onRolesChange).toHaveBeenCalledWith(1);
  });

  it('shows saveSuccess message when provided', () => {
    const { container } = render(() => (
      <GroupEditForm {...baseProps({ saveSuccess: 'Group saved successfully.' })} />
    ));
    expect(container.textContent).toContain('Group saved successfully.');
  });

  it('shows saveError message when provided', () => {
    const { container } = render(() => (
      <GroupEditForm {...baseProps({ saveError: 'Failed to save group.' })} />
    ));
    expect(container.textContent).toContain('Failed to save group.');
  });

  it('disables the save button and shows Saving... when isSaving is true', () => {
    const { getByTestId } = render(() => <GroupEditForm {...baseProps({ isSaving: true })} />);
    const btn = getByTestId('group-save-changes-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Saving...');
  });

  it('invokes onSubmit when the form is submitted', () => {
    const onSubmit = vi.fn();
    const { container } = render(() => <GroupEditForm {...baseProps({ onSubmit })} />);
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
