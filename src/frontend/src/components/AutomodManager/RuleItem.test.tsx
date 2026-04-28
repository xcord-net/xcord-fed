import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import RuleItem from './RuleItem';
import type { AutomodRule } from './helpers';

const sampleRule: AutomodRule = {
  id: 'r-1',
  serverId: 's-1',
  name: 'Block Profanity',
  enabled: true,
  triggerType: 'Keyword',
  triggerConfig: JSON.stringify({ keywords: ['badword'], matchWholeWord: false }),
  actionType: 'BlockMessage',
  actionConfig: null,
  exemptRoleIds: null,
  exemptChannelIds: null,
  exemptBots: false,
  createdAt: '2026-01-01T00:00:00Z',
};

function defaultProps(overrides: Partial<Parameters<typeof RuleItem>[0]> = {}) {
  return {
    rule: sampleRule,
    isEditing: false,
    isSavingEdit: false,
    confirmDelete: false,
    editName: sampleRule.name,
    editTriggerType: sampleRule.triggerType,
    editTriggerConfig: sampleRule.triggerConfig,
    editActionType: sampleRule.actionType,
    editEnabled: sampleRule.enabled,
    onStartEdit: vi.fn(),
    onCancelEdit: vi.fn(),
    onSaveEdit: vi.fn(),
    onEditNameChange: vi.fn(),
    onEditTriggerTypeChange: vi.fn(),
    onEditTriggerConfigChange: vi.fn(),
    onEditActionTypeChange: vi.fn(),
    onEditEnabledChange: vi.fn(),
    onRequestDelete: vi.fn(),
    onCancelDelete: vi.fn(),
    onConfirmDelete: vi.fn(),
    ...overrides,
  };
}

describe('RuleItem', () => {
  it('renders without crashing with minimal props', () => {
    const { getByLabelText } = render(() => <RuleItem {...defaultProps()} />);
    expect(getByLabelText('Automod rule: Block Profanity')).toBeInTheDocument();
  });

  it('shows the rule name, trigger label, and enabled badge', () => {
    const { getByText } = render(() => <RuleItem {...defaultProps()} />);
    expect(getByText('Block Profanity')).toBeInTheDocument();
    expect(getByText('Enabled')).toBeInTheDocument();
    // Trigger -> Action summary line includes both labels.
    expect(getByText(/Keyword Filter/)).toBeInTheDocument();
    expect(getByText(/Block Message/)).toBeInTheDocument();
  });

  it('shows Disabled badge when the rule is not enabled', () => {
    const { getByText } = render(() => (
      <RuleItem {...defaultProps({ rule: { ...sampleRule, enabled: false } })} />
    ));
    expect(getByText('Disabled')).toBeInTheDocument();
  });

  it('invokes onStartEdit when Edit is clicked', () => {
    const onStartEdit = vi.fn();
    const { getByLabelText } = render(() => (
      <RuleItem {...defaultProps({ onStartEdit })} />
    ));
    fireEvent.click(getByLabelText('Edit rule Block Profanity'));
    expect(onStartEdit).toHaveBeenCalledWith(sampleRule);
  });

  it('invokes onRequestDelete when Delete is clicked', () => {
    const onRequestDelete = vi.fn();
    const { getByLabelText } = render(() => (
      <RuleItem {...defaultProps({ onRequestDelete })} />
    ));
    fireEvent.click(getByLabelText('Delete rule Block Profanity'));
    expect(onRequestDelete).toHaveBeenCalledWith('r-1');
  });

  it('shows confirm delete buttons and fires onConfirmDelete on Yes', () => {
    const onConfirmDelete = vi.fn();
    const { getByLabelText, getByText } = render(() => (
      <RuleItem {...defaultProps({ confirmDelete: true, onConfirmDelete })} />
    ));
    expect(getByText('Delete?')).toBeInTheDocument();
    fireEvent.click(getByLabelText('Confirm delete'));
    expect(onConfirmDelete).toHaveBeenCalledWith('r-1');
  });

  it('renders the edit form when isEditing is true and submits on save', () => {
    const onSaveEdit = vi.fn();
    const { container, getByText } = render(() => (
      <RuleItem {...defaultProps({ isEditing: true, onSaveEdit })} />
    ));
    expect(getByText('Edit Rule')).toBeInTheDocument();
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(onSaveEdit).toHaveBeenCalledTimes(1);
  });
});
