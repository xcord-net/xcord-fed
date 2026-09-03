import { describe, it, expect } from 'vitest';
import { fireEvent } from '@solidjs/testing-library';
import ForgotPassword from './ForgotPassword';
import { mockFetch } from '../tests/helpers/mockFetch';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';

describe('ForgotPassword', () => {
  it('renders heading and email input', () => {
    const { getByTestId } = renderWithRouter(() => <ForgotPassword />, { path: '/forgot-password' });
    expect(getByTestId('forgot-password-heading')).toHaveTextContent('Forgot your password?');
    expect(getByTestId('forgot-password-email-input')).toBeInTheDocument();
  });

  it('renders submit button enabled by default', () => {
    const { getByTestId } = renderWithRouter(() => <ForgotPassword />, { path: '/forgot-password' });
    const btn = getByTestId('forgot-password-submit-button') as HTMLButtonElement;
    expect(btn.disabled).toBe(false);
  });

  it('renders Back to Login link', () => {
    const { container } = renderWithRouter(() => <ForgotPassword />, { path: '/forgot-password' });
    const links = container.querySelectorAll('a');
    expect(Array.from(links).some(a => a.textContent?.includes('Back to Login'))).toBe(true);
  });

  it('shows success message after API responds 204', async () => {
    mockFetch({
      'POST /api/v1/auth/forgot-password': () => ({ status: 204, body: null }),
    });
    const { getByTestId, findByTestId } = renderWithRouter(() => <ForgotPassword />, { path: '/forgot-password' });
    fireEvent.input(getByTestId('forgot-password-email-input'), { target: { value: 'user@example.com' } });
    fireEvent.submit(getByTestId('forgot-password-form'));
    expect(await findByTestId('forgot-password-success')).toBeInTheDocument();
  });

  it('shows network error when API throws', async () => {
    mockFetch({
      'POST /api/v1/auth/forgot-password': () => { throw new Error('boom'); },
    });
    const { getByTestId, findByTestId } = renderWithRouter(() => <ForgotPassword />, { path: '/forgot-password' });
    fireEvent.input(getByTestId('forgot-password-email-input'), { target: { value: 'user@example.com' } });
    fireEvent.submit(getByTestId('forgot-password-form'));
    expect(await findByTestId('forgot-password-error')).toHaveTextContent(/Could not reach Xcord/);
  });
});
