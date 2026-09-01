import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';

const mockAuth = {
  login: vi.fn(),
  verifyTwoFactor: vi.fn(),
  validateAuth: vi.fn(),
};

vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuth,
}));

import Login from './Login';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('Login', () => {
  beforeEach(() => {
    mockAuth.login = vi.fn().mockResolvedValue({ authenticated: true, requiresTwoFactor: false });
    mockAuth.verifyTwoFactor = vi.fn().mockResolvedValue(undefined);
    mockAuth.validateAuth = vi.fn().mockResolvedValue(true);
  });

  it('renders heading, email, password inputs and submit button', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    expect(await findByTestId('login-heading')).toHaveTextContent('Welcome back!');
    expect(await findByTestId('login-email-input')).toBeInTheDocument();
    expect(await findByTestId('login-password-input')).toBeInTheDocument();
    expect(await findByTestId('login-submit-button')).toBeInTheDocument();
  });

  it('shows the Register link when registration is enabled', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }),
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    expect(await findByTestId('login-register-link')).toBeInTheDocument();
  });

  it('hides the dev login button when the server does not offer it', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
    });
    const { findByTestId, queryByTestId } = renderWithRouter(() => <Login />);
    // Wait for the config fetch to settle before asserting the absence.
    await findByTestId('login-submit-button');
    await waitFor(() => expect(queryByTestId('dev-login-button')).not.toBeInTheDocument());
  });

  it('shows the dev login button and signs in through it when enabled', async () => {
    const { calls } = mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false, devLoginEnabled: true } }),
      'POST /api/v1/test/dev-login': () => ({ status: 200, body: { authenticated: true } }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    fireEvent.click(await findByTestId('dev-login-button'));
    await waitFor(() => expect(mockAuth.validateAuth).toHaveBeenCalled());
    expect(calls).toContainEqual(
      expect.objectContaining({ method: 'POST', url: '/api/v1/test/dev-login' }),
    );
  });

  it('calls auth.login with email and password on submit', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    fireEvent.input(await findByTestId('login-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('login-password-input'), { target: { value: 'secret11' } });
    fireEvent.submit(await findByTestId('login-form'));
    await waitFor(() => expect(mockAuth.login).toHaveBeenCalledWith({ email: 'a@b.c', password: 'secret11' }));
  });

  it('shows error message when login rejects', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
    });
    mockAuth.login = vi.fn().mockRejectedValue(new Error('Invalid credentials'));
    const { findByTestId } = renderWithRouter(() => <Login />);
    fireEvent.input(await findByTestId('login-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('login-password-input'), { target: { value: 'wrong' } });
    fireEvent.submit(await findByTestId('login-form'));
    expect(await findByTestId('login-error')).toHaveTextContent('Invalid credentials');
  });

  it('switches to 2FA challenge form when login requires two factor', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
    });
    mockAuth.login = vi.fn().mockResolvedValue({
      authenticated: false,
      requiresTwoFactor: true,
      twoFactorToken: 'tok-1',
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    fireEvent.input(await findByTestId('login-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('login-password-input'), { target: { value: 'secret11' } });
    fireEvent.submit(await findByTestId('login-form'));
    expect(await findByTestId('2fa-challenge-form')).toBeInTheDocument();
    expect(await findByTestId('2fa-login-code-input')).toBeInTheDocument();
  });

  it('returns to login form when "Back to login" clicked from 2FA screen', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: false } }),
    });
    mockAuth.login = vi.fn().mockResolvedValue({
      authenticated: false,
      requiresTwoFactor: true,
      twoFactorToken: 'tok-1',
    });
    const { findByTestId } = renderWithRouter(() => <Login />);
    fireEvent.input(await findByTestId('login-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('login-password-input'), { target: { value: 'secret11' } });
    fireEvent.submit(await findByTestId('login-form'));
    fireEvent.click(await findByTestId('2fa-login-back-button'));
    expect(await findByTestId('login-heading')).toBeInTheDocument();
  });
});
