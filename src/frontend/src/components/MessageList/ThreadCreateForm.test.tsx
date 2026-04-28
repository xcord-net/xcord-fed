import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ThreadCreateForm from './ThreadCreateForm';

function baseProps(overrides: Partial<Parameters<typeof ThreadCreateForm>[0]> = {}) {
  return {
    value: '',
    onInput: vi.fn(),
    onCancel: vi.fn(),
    onSubmit: vi.fn(),
    ...overrides,
  };
}

describe('ThreadCreateForm', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <ThreadCreateForm {...baseProps()} />);
    expect(getByTestId('thread-create-form')).toBeInTheDocument();
  });

  it('reflects the current value in the input', () => {
    const { getByTestId } = render(() => (
      <ThreadCreateForm {...baseProps({ value: 'release-notes' })} />
    ));
    const input = getByTestId('thread-name-input') as HTMLInputElement;
    expect(input.value).toBe('release-notes');
  });

  it('calls onInput with the new value when the input changes', () => {
    const onInput = vi.fn();
    const { getByTestId } = render(() => (
      <ThreadCreateForm {...baseProps({ onInput })} />
    ));
    const input = getByTestId('thread-name-input') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'design' } });
    expect(onInput).toHaveBeenCalledWith('design');
  });

  it('invokes onSubmit when the Create Thread button is clicked', () => {
    const onSubmit = vi.fn();
    const { getByTestId } = render(() => (
      <ThreadCreateForm {...baseProps({ onSubmit })} />
    ));
    fireEvent.click(getByTestId('thread-create-submit'));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('invokes onCancel when the Cancel button is clicked', () => {
    const onCancel = vi.fn();
    const { getByTestId } = render(() => (
      <ThreadCreateForm {...baseProps({ onCancel })} />
    ));
    fireEvent.click(getByTestId('thread-create-cancel'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('invokes onCancel when Escape is pressed inside the input', () => {
    const onCancel = vi.fn();
    const { getByTestId } = render(() => (
      <ThreadCreateForm {...baseProps({ onCancel })} />
    ));
    const input = getByTestId('thread-name-input');
    fireEvent.keyDown(input, { key: 'Escape' });
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
