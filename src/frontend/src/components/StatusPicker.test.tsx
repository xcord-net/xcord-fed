import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';

const updatePresenceMock = vi.fn(async () => undefined);
vi.mock('../stores/signalr.store', () => ({
  useSignalR: () => ({ updatePresence: updatePresenceMock }),
}));

const mockAuth = { user: { id: 'me-1', username: 'me', email: '' } };
vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuth,
}));

import StatusPicker from './StatusPicker';

describe('StatusPicker', () => {
  beforeEach(() => {
    updatePresenceMock.mockClear();
  });

  it('renders the trigger button collapsed', () => {
    const { getByTestId } = render(() => <StatusPicker />);
    const btn = getByTestId('nav-set-status-button');
    expect(btn).toBeInTheDocument();
    expect(btn.getAttribute('aria-expanded')).toBe('false');
  });

  it('opens the menu when trigger is clicked', async () => {
    const { getByTestId } = render(() => <StatusPicker />);
    fireEvent.click(getByTestId('nav-set-status-button'));
    await waitFor(() => expect(getByTestId('nav-set-status-button').getAttribute('aria-expanded')).toBe('true'));
    expect(getByTestId('status-option-online')).toBeInTheDocument();
  });

  it('lists all four status options when open', async () => {
    const { getByTestId } = render(() => <StatusPicker />);
    fireEvent.click(getByTestId('nav-set-status-button'));
    await waitFor(() => getByTestId('status-option-online'));
    expect(getByTestId('status-option-online')).toBeInTheDocument();
    expect(getByTestId('status-option-idle')).toBeInTheDocument();
    expect(getByTestId('status-option-dnd')).toBeInTheDocument();
    expect(getByTestId('status-option-offline')).toBeInTheDocument();
  });

  it('invokes signalR.updatePresence on selection', async () => {
    const { getByTestId } = render(() => <StatusPicker />);
    fireEvent.click(getByTestId('nav-set-status-button'));
    await waitFor(() => getByTestId('status-option-dnd'));
    fireEvent.click(getByTestId('status-option-dnd'));
    await waitFor(() => expect(updatePresenceMock).toHaveBeenCalledWith('dnd'));
  });

  it('closes menu after selection', async () => {
    const { getByTestId, queryByTestId } = render(() => <StatusPicker />);
    fireEvent.click(getByTestId('nav-set-status-button'));
    await waitFor(() => getByTestId('status-option-online'));
    fireEvent.click(getByTestId('status-option-online'));
    await waitFor(() => expect(queryByTestId('status-option-online')).toBeNull());
  });
});
