import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import AccountDeletion, { validateDeletionRequest, formatDeletionDate } from './AccountDeletion';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('validateDeletionRequest', () => {
  it('returns error when password is empty/whitespace', () => {
    expect(validateDeletionRequest('')).toBe('Password is required');
    expect(validateDeletionRequest('   ')).toBe('Password is required');
  });

  it('returns empty string for non-empty password', () => {
    expect(validateDeletionRequest('hunter2')).toBe('');
  });
});

describe('formatDeletionDate', () => {
  it('formats an ISO date as a localized long date', () => {
    const out = formatDeletionDate('2025-06-15T00:00:00Z');
    expect(out).toMatch(/2025/);
  });
});

describe('AccountDeletion', () => {
  it('renders the danger zone heading and Delete Account button when not scheduled', () => {
    const { getByTestId } = render(() => <AccountDeletion />);
    expect(getByTestId('danger-zone-section')).toBeInTheDocument();
    expect(getByTestId('delete-account-button')).toHaveTextContent('Delete Account');
  });

  it('opens the confirmation dialog when Delete Account is clicked', async () => {
    const { getByTestId, findByTestId } = render(() => <AccountDeletion />);
    fireEvent.click(getByTestId('delete-account-button'));
    expect(await findByTestId('delete-account-dialog')).toBeInTheDocument();
    expect(await findByTestId('delete-account-password-input')).toBeInTheDocument();
  });

  it('shows scheduled-deletion warning when scheduledDeletionAt is set', () => {
    const { getByTestId } = render(() => (
      <AccountDeletion scheduledDeletionAt="2025-06-15T00:00:00Z" />
    ));
    expect(getByTestId('scheduled-deletion-warning')).toBeInTheDocument();
    expect(getByTestId('cancel-deletion-button')).toHaveTextContent('Cancel Account Deletion');
  });

  it('schedules deletion and calls onDeletionScheduled on success', async () => {
    mockFetch({
      'POST /api/v1/users/@me/delete': () => ({
        status: 200,
        body: { scheduledDeletionAt: '2025-12-01T00:00:00Z' },
      }),
    });
    const onScheduled = vi.fn();
    const { getByTestId, findByTestId } = render(() => (
      <AccountDeletion onDeletionScheduled={onScheduled} />
    ));
    fireEvent.click(getByTestId('delete-account-button'));
    const input = await findByTestId('delete-account-password-input');
    fireEvent.input(input, { target: { value: 'hunter22' } });
    fireEvent.click(getByTestId('delete-account-confirm-button'));
    await waitFor(() => expect(onScheduled).toHaveBeenCalledWith('2025-12-01T00:00:00Z'));
  });

  it('cancels deletion and calls onDeletionCancelled on success', async () => {
    mockFetch({
      'POST /api/v1/users/@me/cancel-deletion': () => ({ status: 200, body: {} }),
    });
    const onCancelled = vi.fn();
    const { getByTestId } = render(() => (
      <AccountDeletion
        scheduledDeletionAt="2025-06-15T00:00:00Z"
        onDeletionCancelled={onCancelled}
      />
    ));
    fireEvent.click(getByTestId('cancel-deletion-button'));
    await waitFor(() => expect(onCancelled).toHaveBeenCalled());
  });
});
