import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import CommandPalette, { filterCommands, buildCommandPreview, validateCommandArgs, type BotCommand } from './CommandPalette';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleCmd: BotCommand = {
  id: 'c-1',
  name: 'kick',
  description: 'Kick a user',
  parameters: [
    { name: 'user', description: 'who', required: true, type: 'user' },
    { name: 'reason', description: 'why', required: false, type: 'string' },
  ],
  botId: 'b-1',
  botName: 'ModBot',
};

describe('filterCommands', () => {
  it('returns all commands for empty query', () => {
    expect(filterCommands([sampleCmd], '')).toHaveLength(1);
  });

  it('matches by command name', () => {
    expect(filterCommands([sampleCmd], '/kic')).toHaveLength(1);
  });

  it('returns empty when nothing matches', () => {
    expect(filterCommands([sampleCmd], 'zzz')).toHaveLength(0);
  });
});

describe('buildCommandPreview', () => {
  it('formats required and optional parameters', () => {
    expect(buildCommandPreview(sampleCmd)).toBe('/kick <user> [reason]');
  });

  it('returns just /name when no parameters', () => {
    expect(buildCommandPreview({ ...sampleCmd, parameters: [] })).toBe('/kick');
  });
});

describe('validateCommandArgs', () => {
  it('returns error when required parameter missing', () => {
    expect(validateCommandArgs(sampleCmd, {})).toBe('Parameter "user" is required.');
  });

  it('returns null when required parameters provided', () => {
    expect(validateCommandArgs(sampleCmd, { user: 'alice' })).toBeNull();
  });
});

describe('CommandPalette', () => {
  it('renders the dialog with header label', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/commands': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => (
      <CommandPalette serverId="s-1" onSelectCommand={vi.fn()} onDismiss={vi.fn()} />
    ));
    expect(await findByText('Slash Commands')).toBeInTheDocument();
  });

  it('shows empty state when no commands match', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/commands': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => (
      <CommandPalette serverId="s-1" onSelectCommand={vi.fn()} onDismiss={vi.fn()} />
    ));
    expect(await findByText('No commands found')).toBeInTheDocument();
  });

  it('renders fetched commands in the list', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/commands': () => ({ status: 200, body: [sampleCmd] }) });
    const { findByText } = render(() => (
      <CommandPalette serverId="s-1" onSelectCommand={vi.fn()} onDismiss={vi.fn()} />
    ));
    expect(await findByText('/kick')).toBeInTheDocument();
    expect(await findByText('Kick a user')).toBeInTheDocument();
  });

  it('opens parameter form when clicking a command with parameters', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/commands': () => ({ status: 200, body: [sampleCmd] }) });
    const onSelect = vi.fn();
    const { findByText, container } = render(() => (
      <CommandPalette serverId="s-1" onSelectCommand={onSelect} onDismiss={vi.fn()} />
    ));
    fireEvent.click(await findByText('/kick'));
    await waitFor(() => expect(container.textContent).toContain('Send Command'));
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('dispatches immediately when command has no parameters', async () => {
    const noParams: BotCommand = { ...sampleCmd, name: 'ping', parameters: [] };
    mockFetch({ 'GET /api/v1/servers/s-1/commands': () => ({ status: 200, body: [noParams] }) });
    const onSelect = vi.fn();
    const { findByText } = render(() => (
      <CommandPalette serverId="s-1" onSelectCommand={onSelect} onDismiss={vi.fn()} />
    ));
    fireEvent.click(await findByText('/ping'));
    expect(onSelect).toHaveBeenCalledWith(noParams, {});
  });
});
