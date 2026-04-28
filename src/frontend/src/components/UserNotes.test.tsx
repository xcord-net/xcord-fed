import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import UserNotes from './UserNotes';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('UserNotes', () => {
  it('renders the heading and lookup form', () => {
    const { getByText, container } = render(() => <UserNotes />);
    expect(getByText('User Notes')).toBeInTheDocument();
    expect(container.querySelector('#note-username-input')).not.toBeNull();
    expect(container.querySelector('#user-notes-find-btn')).not.toBeNull();
  });

  it('disables the Find button when no username is entered', () => {
    const { container } = render(() => <UserNotes />);
    const find = container.querySelector('#user-notes-find-btn') as HTMLButtonElement;
    expect(find).toBeDisabled();
    fireEvent.input(
      container.querySelector('#note-username-input')!,
      { target: { value: 'alice' } },
    );
    expect(find).not.toBeDisabled();
  });

  it('shows a not-found message when lookup fails', async () => {
    mockFetch({
      'GET /api/v1/users/by-username/ghost': () => ({ status: 404, body: { message: 'Not found' } }),
    });
    const { container, findByText } = render(() => <UserNotes />);
    fireEvent.input(
      container.querySelector('#note-username-input')!,
      { target: { value: 'ghost' } },
    );
    fireEvent.click(container.querySelector('#user-notes-find-btn')!);
    expect(await findByText(/User not found/)).toBeInTheDocument();
  });

  it('loads existing note content when a user is found', async () => {
    mockFetch({
      'GET /api/v1/users/by-username/alice': () =>
        ({ status: 200, body: { id: 'u-1', username: 'alice', displayName: 'Alice' } }),
      'GET /api/v1/users/u-1/notes': () =>
        ({ status: 200, body: { id: 'n-1', targetUserId: 'u-1', content: 'remember her birthday', createdAt: '2025-01-01T00:00:00Z' } }),
    });
    const { container, findByDisplayValue, findByText } = render(() => <UserNotes />);
    fireEvent.input(
      container.querySelector('#note-username-input')!,
      { target: { value: 'alice' } },
    );
    fireEvent.click(container.querySelector('#user-notes-find-btn')!);
    expect(await findByText('Alice')).toBeInTheDocument();
    expect(await findByDisplayValue('remember her birthday')).toBeInTheDocument();
  });

  it('saves a new note via PUT and shows the success indicator', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/by-username/bob': () =>
        ({ status: 200, body: { id: 'u-2', username: 'bob' } }),
      'GET /api/v1/users/u-2/notes': () => ({ status: 404, body: {} }),
      'PUT /api/v1/users/u-2/notes': () =>
        ({ status: 200, body: { id: 'n-2', targetUserId: 'u-2', content: 'hi', createdAt: '2025-01-01T00:00:00Z' } }),
    });
    const { container, findByText } = render(() => <UserNotes />);
    fireEvent.input(
      container.querySelector('#note-username-input')!,
      { target: { value: 'bob' } },
    );
    fireEvent.click(container.querySelector('#user-notes-find-btn')!);
    await findByText('bob');
    fireEvent.input(
      container.querySelector('#note-content-input')!,
      { target: { value: 'hi' } },
    );
    fireEvent.click(container.querySelector('#user-notes-save-btn')!);
    await waitFor(() => expect(calls.calls.some(c => c.method === 'PUT' && c.url.endsWith('/notes'))).toBe(true));
    expect(await findByText('Saved!')).toBeInTheDocument();
  });

  it('does not perform a lookup for an empty username', () => {
    const { container } = render(() => <UserNotes />);
    // No mockFetch installed - if a fetch were issued, the default setup throws.
    fireEvent.click(container.querySelector('#user-notes-find-btn')!);
  });
});
