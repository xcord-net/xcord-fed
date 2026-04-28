import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import OwnershipTransfer, { validateServerName } from './OwnershipTransfer';
import { mockFetch } from '../tests/helpers/mockFetch';

const baseProps = {
  serverId: 's-1',
  serverName: 'Cool Server',
  currentUserId: 'owner-1',
  ownerId: 'owner-1',
};

const otherMember = { userId: 'm-2', username: 'alice', displayName: 'Alice' };

describe('validateServerName', () => {
  it('returns error when names do not match', () => {
    expect(validateServerName('wrong', 'Cool Server')).toMatch(/does not match/i);
  });

  it('returns empty when name matches exactly', () => {
    expect(validateServerName('Cool Server', 'Cool Server')).toBe('');
  });

  it('trims whitespace before comparing', () => {
    expect(validateServerName('  Cool Server  ', 'Cool Server')).toBe('');
  });
});

describe('OwnershipTransfer', () => {
  it('renders nothing when current user is not the owner', () => {
    const { queryByTestId } = render(() => (
      <OwnershipTransfer {...baseProps} currentUserId="someone-else" />
    ));
    expect(queryByTestId('ownership-transfer-section')).toBeNull();
  });

  it('renders the section header for the owner', () => {
    const { getByTestId, getByText } = render(() => <OwnershipTransfer {...baseProps} />);
    expect(getByTestId('ownership-transfer-section')).toBeInTheDocument();
    expect(getByText('Server Ownership')).toBeInTheDocument();
  });

  it('opens the member selection dialog when transfer button clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({ status: 200, body: [otherMember] }),
    });
    const { getByTestId, findByTestId } = render(() => <OwnershipTransfer {...baseProps} />);
    fireEvent.click(getByTestId('transfer-ownership-open-button'));
    expect(await findByTestId('transfer-select-member-dialog')).toBeInTheDocument();
    expect(await findByTestId(`transfer-member-option-${otherMember.userId}`)).toBeInTheDocument();
  });

  it('shows empty state when there are no other members', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({ status: 200, body: [{ userId: 'owner-1', username: 'me' }] }),
    });
    const { getByTestId, findByText } = render(() => <OwnershipTransfer {...baseProps} />);
    fireEvent.click(getByTestId('transfer-ownership-open-button'));
    expect(await findByText('No other members to transfer to.')).toBeInTheDocument();
  });

  it('advances to confirmation step after selecting a member', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({ status: 200, body: [otherMember] }),
    });
    const { getByTestId, findByTestId } = render(() => <OwnershipTransfer {...baseProps} />);
    fireEvent.click(getByTestId('transfer-ownership-open-button'));
    fireEvent.click(await findByTestId(`transfer-member-option-${otherMember.userId}`));
    expect(await findByTestId('transfer-confirm-dialog')).toBeInTheDocument();
    expect(await findByTestId('transfer-server-name-input')).toBeInTheDocument();
  });

  it('disables confirm button until server name matches exactly', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({ status: 200, body: [otherMember] }),
    });
    const { getByTestId, findByTestId } = render(() => <OwnershipTransfer {...baseProps} />);
    fireEvent.click(getByTestId('transfer-ownership-open-button'));
    fireEvent.click(await findByTestId(`transfer-member-option-${otherMember.userId}`));
    const confirmBtn = await findByTestId('transfer-confirm-button') as HTMLButtonElement;
    expect(confirmBtn.disabled).toBe(true);
    const input = await findByTestId('transfer-server-name-input') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'Cool Server' } });
    await waitFor(() => expect(confirmBtn.disabled).toBe(false));
  });
});
