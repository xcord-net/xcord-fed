import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import GroupManager, { hasRole, toggleRole } from './GroupManager';
import { mockFetch } from '../../tests/helpers/mockFetch';

const sampleGroup = {
  id: 'g-1',
  serverId: 's-1',
  name: 'Admins',
  color: '#ed4245',
  roles: 0,
  position: 1,
  isHoisted: false,
  isMentionable: true,
};

describe('GroupManager helpers', () => {
  it('hasRole returns true when the bit is set', () => {
    expect(hasRole(0b101, 0b001)).toBe(true);
    expect(hasRole(0b101, 0b010)).toBe(false);
  });

  it('toggleRole flips a single bit', () => {
    expect(toggleRole(0, 1 << 3)).toBe(8);
    expect(toggleRole(8, 1 << 3)).toBe(0);
  });
});

describe('GroupManager', () => {
  it('renders the heading and create button', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }) });
    const { findByTestId, getByTestId } = render(() => <GroupManager serverId="s-1" />);
    expect(await findByTestId('group-manager-heading')).toHaveTextContent('Groups');
    expect(getByTestId('create-group-button')).toBeInTheDocument();
  });

  it('loads and displays groups from the API', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [sampleGroup] }) });
    const { findByTestId } = render(() => <GroupManager serverId="s-1" />);
    expect(await findByTestId('group-item-g-1')).toHaveTextContent('Admins');
  });

  it('shows an error banner when the load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/groups': () => ({ status: 500, body: { message: 'Server error' } }),
    });
    const { findByRole } = render(() => <GroupManager serverId="s-1" />);
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Failed to load groups|Server error/);
  });

  it('opens the create form when Create Group is clicked', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }) });
    const { findByTestId, getByText } = render(() => <GroupManager serverId="s-1" />);
    fireEvent.click(await findByTestId('create-group-button'));
    expect(getByText('Create New Group')).toBeInTheDocument();
  });

  it('creates a new group and selects it via the editor', async () => {
    const created = { ...sampleGroup, id: 'g-2', name: 'Helpers' };
    mockFetch({
      'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [] }),
      'POST /api/v1/servers/s-1/groups': () => ({ status: 200, body: created }),
    });
    const { findByTestId, container, getByTestId } = render(() => <GroupManager serverId="s-1" />);
    fireEvent.click(await findByTestId('create-group-button'));

    const nameInput = container.querySelector('#new-group-name') as HTMLInputElement;
    fireEvent.input(nameInput, { target: { value: 'Helpers' } });

    const form = container.querySelector('form')!;
    fireEvent.submit(form);

    // After creation the new group is shown in the list and editor opens.
    await waitFor(() => expect(getByTestId('group-item-g-2')).toHaveTextContent('Helpers'));
    await waitFor(() => expect(getByTestId('group-save-changes-button')).toBeInTheDocument());
  });

  it('shows the empty editor message before any group is selected', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/groups': () => ({ status: 200, body: [sampleGroup] }) });
    const { findByText } = render(() => <GroupManager serverId="s-1" />);
    expect(await findByText('Select a group to edit, or create a new one.')).toBeInTheDocument();
  });
});
