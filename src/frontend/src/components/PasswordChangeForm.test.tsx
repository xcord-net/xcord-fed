import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import PasswordChangeForm, { validatePasswordChange } from './PasswordChangeForm';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('validatePasswordChange', () => {
  it('returns mismatch error when passwords differ', () => {
    expect(validatePasswordChange('old', 'newpass1', 'other')).toBe('New passwords do not match');
  });

  it('returns length error for short password', () => {
    expect(validatePasswordChange('old', 'short', 'short')).toBe('New password must be at least 8 characters');
  });

  it('rejects identical current and new password', () => {
    expect(validatePasswordChange('samepass', 'samepass', 'samepass')).toBe('New password must be different from current password');
  });

  it('returns empty string for valid input', () => {
    expect(validatePasswordChange('oldpass1', 'newpass1', 'newpass1')).toBe('');
  });
});

describe('PasswordChangeForm', () => {
  function fill(container: HTMLElement, current: string, next: string, confirm: string) {
    fireEvent.input(container.querySelector('[data-testid="current-password-input"]')!, { target: { value: current } });
    fireEvent.input(container.querySelector('[data-testid="new-password-input"]')!, { target: { value: next } });
    fireEvent.input(container.querySelector('[data-testid="confirm-password-input"]')!, { target: { value: confirm } });
  }

  it('renders heading and submit button', () => {
    const { getByTestId } = render(() => <PasswordChangeForm />);
    expect(getByTestId('change-password-heading')).toHaveTextContent('Change Password');
    expect(getByTestId('change-password-submit-button')).toHaveTextContent('Change Password');
  });

  it('shows validation error on submit when passwords mismatch', async () => {
    const { container, getByTestId, findByTestId } = render(() => <PasswordChangeForm />);
    fill(container, 'currpass1', 'newpass1', 'mismatch');
    fireEvent.click(getByTestId('change-password-submit-button'));
    const err = await findByTestId('change-password-error');
    expect(err).toHaveTextContent('New passwords do not match');
  });

  it('submits to API and shows success message on 200', async () => {
    mockFetch({
      'POST /api/v1/auth/change-password': () => ({ status: 200, body: {} }),
    });
    const { container, getByTestId, findByTestId } = render(() => <PasswordChangeForm />);
    fill(container, 'oldpass11', 'newpass11', 'newpass11');
    fireEvent.click(getByTestId('change-password-submit-button'));
    const success = await findByTestId('change-password-success');
    expect(success).toHaveTextContent('Password changed successfully');
  });

  it('shows error message when API rejects', async () => {
    mockFetch({
      'POST /api/v1/auth/change-password': () => ({ status: 400, body: { message: 'Wrong current password' } }),
    });
    const { container, getByTestId, findByTestId } = render(() => <PasswordChangeForm />);
    fill(container, 'wrongpass', 'newpass11', 'newpass11');
    fireEvent.click(getByTestId('change-password-submit-button'));
    const err = await findByTestId('change-password-error');
    expect(err.textContent).toMatch(/Wrong current password|Failed to change password/);
  });

  it('disables submit button while loading', async () => {
    let resolve!: (v: unknown) => void;
    mockFetch({
      'POST /api/v1/auth/change-password': () => new Promise(r => { resolve = r; }) as Promise<{ status: number; body: object }>,
    });
    const { container, getByTestId } = render(() => <PasswordChangeForm />);
    fill(container, 'oldpass11', 'newpass11', 'newpass11');
    const btn = getByTestId('change-password-submit-button') as HTMLButtonElement;
    fireEvent.click(btn);
    await waitFor(() => expect(btn).toBeDisabled());
    resolve({ status: 200, body: {} });
  });
});
