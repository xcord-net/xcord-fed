import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import UserProfileEditor from './UserProfileEditor';
import { useProfiles } from '../stores/profile.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleProfile = {
  userId: 'u-1',
  username: 'alice',
  displayName: 'Alice',
  bio: 'hello world',
  pronouns: 'she/her',
  createdAt: '2025-01-01T00:00:00Z',
  twoFactorEnabled: false,
  scheduledDeletionAt: null,
};

describe('UserProfileEditor', () => {
  beforeEach(() => {
    useProfiles().reset();
  });

  it('renders the User Profile heading and Edit toggle', () => {
    mockFetch({ 'GET /api/v1/users/@me': () => ({ status: 200, body: sampleProfile }) });
    const { getByText, getByTestId } = render(() => <UserProfileEditor />);
    expect(getByText('User Profile')).toBeInTheDocument();
    expect(getByTestId('profile-edit-button')).toHaveTextContent('Edit');
  });

  it('renders Server Profile heading when serverId is provided', () => {
    mockFetch({
      'GET /api/v1/users/@me': () => ({ status: 200, body: sampleProfile }),
      'GET /api/v1/servers/s-1/members/me': () => ({ status: 200, body: { userId: 'u-1', serverId: 's-1', nickname: 'Liz' } }),
    });
    const { getByText } = render(() => <UserProfileEditor serverId="s-1" />);
    expect(getByText('Server Profile')).toBeInTheDocument();
  });

  it('shows the loaded display name from the profile store', async () => {
    mockFetch({ 'GET /api/v1/users/@me': () => ({ status: 200, body: sampleProfile }) });
    const { findByTestId } = render(() => <UserProfileEditor />);
    expect(await findByTestId('profile-display-name')).toHaveTextContent('Alice');
  });

  it('toggles into edit mode and reveals the Save button', async () => {
    mockFetch({ 'GET /api/v1/users/@me': () => ({ status: 200, body: sampleProfile }) });
    const { findByTestId, getByTestId } = render(() => <UserProfileEditor />);
    await findByTestId('profile-display-name');
    fireEvent.click(getByTestId('profile-edit-button'));
    expect(getByTestId('profile-save-button')).toBeInTheDocument();
    expect(getByTestId('profile-edit-button')).toHaveTextContent('Cancel');
  });

  it('saves profile updates and shows success message', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me': () => ({ status: 200, body: sampleProfile }),
      'PATCH /api/v1/users/@me': () => ({
        status: 200,
        body: { ...sampleProfile, displayName: 'Alice2' },
      }),
    });
    const { findByTestId, getByTestId } = render(() => <UserProfileEditor />);
    await findByTestId('profile-display-name');
    fireEvent.click(getByTestId('profile-edit-button'));
    fireEvent.input(getByTestId('profile-display-name-input'), { target: { value: 'Alice2' } });
    fireEvent.click(getByTestId('profile-save-button'));
    await waitFor(() => expect(calls.calls.some(c => c.method === 'PATCH')).toBe(true));
    expect(await findByTestId('profile-save-success')).toHaveTextContent('Profile saved successfully.');
  });
});
