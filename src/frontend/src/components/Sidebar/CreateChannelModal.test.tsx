import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import CreateChannelModal from './CreateChannelModal';
import { Capability } from '../../types/channel';

function makeProps(overrides: Partial<Parameters<typeof CreateChannelModal>[0]> = {}) {
  return {
    open: true,
    name: '',
    capabilities: Capability.Chat,
    onNameInput: vi.fn(),
    onToggleCapability: vi.fn(),
    onClose: vi.fn(),
    onSubmit: vi.fn(),
    ...overrides,
  };
}

describe('CreateChannelModal', () => {
  it('does not render when closed', () => {
    const { queryByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ open: false })} />
    ));
    expect(queryByTestId('create-channel-name-input')).toBeNull();
    expect(queryByTestId('create-channel-submit-button')).toBeNull();
  });

  it('renders the title and form fields when open', () => {
    const { getAllByText, getByTestId } = render(() => (
      <CreateChannelModal {...makeProps()} />
    ));
    // 'Create Channel' appears in both the title (h2) and the submit button
    expect(getAllByText('Create Channel').length).toBeGreaterThan(1);
    expect(getByTestId('create-channel-name-input')).toBeInTheDocument();
    expect(getByTestId('capability-checkbox-chat')).toBeInTheDocument();
    expect(getByTestId('capability-checkbox-voice')).toBeInTheDocument();
  });

  it('reflects the current name prop in the input value', () => {
    const { getByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ name: 'announcements' })} />
    ));
    const input = getByTestId('create-channel-name-input') as HTMLInputElement;
    expect(input.value).toBe('announcements');
  });

  it('checks the chat capability checkbox when capabilities include Chat', () => {
    const { getByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ capabilities: Capability.Chat })} />
    ));
    const chat = getByTestId('capability-checkbox-chat') as HTMLInputElement;
    const voice = getByTestId('capability-checkbox-voice') as HTMLInputElement;
    expect(chat.checked).toBe(true);
    expect(voice.checked).toBe(false);
  });

  it('invokes onNameInput as the user types', () => {
    const onNameInput = vi.fn();
    const { getByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ onNameInput })} />
    ));
    const input = getByTestId('create-channel-name-input') as HTMLInputElement;
    input.value = 'general';
    fireEvent.input(input);
    expect(onNameInput).toHaveBeenCalledWith('general');
  });

  it('invokes onToggleCapability with the toggled bit when a checkbox changes', () => {
    const onToggleCapability = vi.fn();
    const { getByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ onToggleCapability })} />
    ));
    fireEvent.click(getByTestId('capability-checkbox-voice'));
    expect(onToggleCapability).toHaveBeenCalledWith(Capability.Voice);
  });

  it('invokes onSubmit when the create button is clicked and onClose for cancel', () => {
    const onSubmit = vi.fn();
    const onClose = vi.fn();
    const { getByTestId } = render(() => (
      <CreateChannelModal {...makeProps({ onSubmit, onClose })} />
    ));
    fireEvent.click(getByTestId('create-channel-submit-button'));
    expect(onSubmit).toHaveBeenCalledOnce();

    fireEvent.click(getByTestId('create-channel-cancel-button'));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
