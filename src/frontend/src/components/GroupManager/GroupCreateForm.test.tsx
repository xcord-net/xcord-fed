import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import GroupCreateForm from './GroupCreateForm';

describe('GroupCreateForm', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => (
      <GroupCreateForm
        newGroupName=""
        isCreating={false}
        onNameInput={vi.fn()}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    ));
    expect(getByTestId('create-group-submit-button')).toBeInTheDocument();
  });

  it('renders the form heading', () => {
    const { container } = render(() => (
      <GroupCreateForm
        newGroupName=""
        isCreating={false}
        onNameInput={vi.fn()}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    ));
    expect(container.textContent).toContain('Create New Group');
  });

  it('reflects newGroupName prop in the input field', () => {
    const { container } = render(() => (
      <GroupCreateForm
        newGroupName="Moderators"
        isCreating={false}
        onNameInput={vi.fn()}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    ));
    const input = container.querySelector('#new-group-name') as HTMLInputElement;
    expect(input.value).toBe('Moderators');
  });

  it('invokes onNameInput when text is typed', () => {
    const onNameInput = vi.fn();
    const { container } = render(() => (
      <GroupCreateForm
        newGroupName=""
        isCreating={false}
        onNameInput={onNameInput}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    ));
    const input = container.querySelector('#new-group-name') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'Helpers' } });
    expect(onNameInput).toHaveBeenCalledWith('Helpers');
  });

  it('invokes onSubmit when the form is submitted', () => {
    const onSubmit = vi.fn();
    const { container } = render(() => (
      <GroupCreateForm
        newGroupName="Helpers"
        isCreating={false}
        onNameInput={vi.fn()}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />
    ));
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('disables submit and shows Creating... while isCreating is true', () => {
    const { getByTestId } = render(() => (
      <GroupCreateForm
        newGroupName="Helpers"
        isCreating={true}
        onNameInput={vi.fn()}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    ));
    const btn = getByTestId('create-group-submit-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Creating...');
  });

  it('invokes onCancel when the cancel button is clicked', () => {
    const onCancel = vi.fn();
    const { getByText } = render(() => (
      <GroupCreateForm
        newGroupName=""
        isCreating={false}
        onNameInput={vi.fn()}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />
    ));
    fireEvent.click(getByText('Cancel'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
