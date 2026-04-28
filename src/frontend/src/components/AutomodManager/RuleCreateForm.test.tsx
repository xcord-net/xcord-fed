import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import RuleCreateForm from './RuleCreateForm';
import { defaultTriggerConfig } from './helpers';

function defaultProps(overrides: Partial<Parameters<typeof RuleCreateForm>[0]> = {}) {
  return {
    name: '',
    triggerType: 'Keyword' as const,
    triggerConfig: defaultTriggerConfig('Keyword'),
    actionType: 'BlockMessage' as const,
    enabled: true,
    isCreating: false,
    onNameChange: vi.fn(),
    onTriggerTypeChange: vi.fn(),
    onTriggerConfigChange: vi.fn(),
    onActionTypeChange: vi.fn(),
    onEnabledChange: vi.fn(),
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    ...overrides,
  };
}

describe('RuleCreateForm', () => {
  it('renders without crashing with minimal props', () => {
    const { getByLabelText } = render(() => <RuleCreateForm {...defaultProps()} />);
    expect(getByLabelText('Create automod rule')).toBeInTheDocument();
  });

  it('renders the form heading and submit button label', () => {
    const { getByText } = render(() => <RuleCreateForm {...defaultProps()} />);
    expect(getByText('New Automod Rule')).toBeInTheDocument();
    expect(getByText('Create Rule')).toBeInTheDocument();
  });

  it('reflects the name prop in the input field', () => {
    const { container } = render(() => (
      <RuleCreateForm {...defaultProps({ name: 'Block Profanity' })} />
    ));
    const input = container.querySelector('#automod-rule-name') as HTMLInputElement;
    expect(input.value).toBe('Block Profanity');
  });

  it('invokes onNameChange when the name input is typed in', () => {
    const onNameChange = vi.fn();
    const { container } = render(() => (
      <RuleCreateForm {...defaultProps({ onNameChange })} />
    ));
    const input = container.querySelector('#automod-rule-name') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'New Name' } });
    expect(onNameChange).toHaveBeenCalledWith('New Name');
  });

  it('invokes onSubmit when the form is submitted', () => {
    const onSubmit = vi.fn();
    const { container } = render(() => (
      <RuleCreateForm {...defaultProps({ name: 'Some Rule', onSubmit })} />
    ));
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('disables submit and shows Creating... while isCreating is true', () => {
    const { getByText } = render(() => (
      <RuleCreateForm {...defaultProps({ isCreating: true })} />
    ));
    const btn = getByText('Creating...') as HTMLButtonElement;
    expect(btn).toBeDisabled();
  });

  it('invokes onCancel when the cancel button is clicked', () => {
    const onCancel = vi.fn();
    const { getByText } = render(() => (
      <RuleCreateForm {...defaultProps({ onCancel })} />
    ));
    fireEvent.click(getByText('Cancel'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
