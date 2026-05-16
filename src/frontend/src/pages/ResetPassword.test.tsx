import { describe, it, expect } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import ResetPassword from './ResetPassword';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';
import { withSearchParams } from '../tests/helpers/withSearchParams';

// ResetPassword reads `window.location.search` at onMount. Each test runs
// inside `withSearchParams(...)`, which snapshots the current URL + history
// state, pushes the desired query string, and restores it in a `finally`
// block even when the test body throws. No global `afterEach` cleanup is
// needed and no state leaks between tests.

describe('ResetPassword', () => {
  it('renders heading and submit button', async () => {
    await withSearchParams({ token: 'tok' }, async () => {
      const { findByTestId } = renderWithRouter(() => <ResetPassword />);
      expect(await findByTestId('reset-password-heading')).toHaveTextContent('Choose a new password');
      expect(await findByTestId('reset-password-submit-button')).toHaveTextContent('Reset Password');
    }, { pathname: '/reset-password' });
  });

  it('shows mismatch error when passwords differ', async () => {
    await withSearchParams({ token: 'tok' }, async () => {
      const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
      fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
      fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'different11' } });
      fireEvent.submit(container.querySelector('form')!);
      expect(await findByTestId('reset-password-error')).toHaveTextContent('Passwords do not match');
    }, { pathname: '/reset-password' });
  });

  it('shows length error when password is shorter than 8 characters', async () => {
    await withSearchParams({ token: 'tok' }, async () => {
      const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
      fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'short' } });
      fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'short' } });
      fireEvent.submit(container.querySelector('form')!);
      expect(await findByTestId('reset-password-error')).toHaveTextContent('Password must be at least 8 characters');
    }, { pathname: '/reset-password' });
  });

  it('shows missing token error when no token is in URL', async () => {
    await withSearchParams(null, async () => {
      const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
      fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
      fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
      fireEvent.submit(container.querySelector('form')!);
      expect(await findByTestId('reset-password-error')).toHaveTextContent('Invalid or missing reset token');
    }, { pathname: '/reset-password' });
  });

  it('shows success card after successful reset', async () => {
    await withSearchParams({ token: 'tok' }, async () => {
      mockFetch({ 'POST /api/v1/auth/reset-password': () => ({ status: 200, body: {} }) });
      const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
      fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
      fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
      fireEvent.submit(container.querySelector('form')!);
      expect(await findByTestId('reset-password-success')).toBeInTheDocument();
    }, { pathname: '/reset-password' });
  });

  it('shows API error message when reset fails', async () => {
    await withSearchParams({ token: 'tok' }, async () => {
      mockFetch({ 'POST /api/v1/auth/reset-password': () => ({ status: 400, body: { message: 'Token expired' } }) });
      const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
      fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
      fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
      fireEvent.submit(container.querySelector('form')!);
      await waitFor(async () => {
        expect((await findByTestId('reset-password-error')).textContent).toMatch(/Token expired|Failed to reset password/);
      });
    }, { pathname: '/reset-password' });
  });
});
