import { describe, it, expect, afterEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import ResetPassword from './ResetPassword';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

function setSearch(token?: string) {
  // Components read window.location.search at onMount; jsdom requires us to
  // navigate via history.pushState so the URL reflects the desired query string.
  const url = token ? `/reset-password?token=${token}` : '/reset-password';
  window.history.pushState({}, '', url);
}

describe('ResetPassword', () => {
  afterEach(() => {
    window.history.pushState({}, '', '/');
  });
  it('renders heading and submit button', async () => {
    setSearch('tok');
    const { findByTestId } = renderWithRouter(() => <ResetPassword />);
    expect(await findByTestId('reset-password-heading')).toHaveTextContent('Choose a new password');
    expect(await findByTestId('reset-password-submit-button')).toHaveTextContent('Reset Password');
  });

  it('shows mismatch error when passwords differ', async () => {
    setSearch('tok');
    const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
    fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
    fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'different11' } });
    fireEvent.submit(container.querySelector('form')!);
    expect(await findByTestId('reset-password-error')).toHaveTextContent('Passwords do not match');
  });

  it('shows length error when password is shorter than 8 characters', async () => {
    setSearch('tok');
    const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
    fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'short' } });
    fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'short' } });
    fireEvent.submit(container.querySelector('form')!);
    expect(await findByTestId('reset-password-error')).toHaveTextContent('Password must be at least 8 characters');
  });

  it('shows missing token error when no token is in URL', async () => {
    setSearch();
    const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
    fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
    fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
    fireEvent.submit(container.querySelector('form')!);
    expect(await findByTestId('reset-password-error')).toHaveTextContent('Invalid or missing reset token');
  });

  it('shows success card after successful reset', async () => {
    setSearch('tok');
    mockFetch({ 'POST /api/v1/auth/reset-password': () => ({ status: 200, body: {} }) });
    const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
    fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
    fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
    fireEvent.submit(container.querySelector('form')!);
    expect(await findByTestId('reset-password-success')).toBeInTheDocument();
  });

  it('shows API error message when reset fails', async () => {
    setSearch('tok');
    mockFetch({ 'POST /api/v1/auth/reset-password': () => ({ status: 400, body: { message: 'Token expired' } }) });
    const { container, findByTestId } = renderWithRouter(() => <ResetPassword />);
    fireEvent.input(container.querySelector('#reset-new-password')!, { target: { value: 'longenough1' } });
    fireEvent.input(container.querySelector('#reset-confirm-password')!, { target: { value: 'longenough1' } });
    fireEvent.submit(container.querySelector('form')!);
    await waitFor(async () => {
      expect((await findByTestId('reset-password-error')).textContent).toMatch(/Token expired|Failed to reset password/);
    });
  });
});
