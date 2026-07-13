import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';

const mockAuth = {
  register: vi.fn(),
};

vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuth,
}));

import Register from './Register';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

const disabledCaptcha = { captchaId: 'disabled', imageUrl: '', audioUrl: '' };

function mockConfigAndCaptcha() {
  mockFetch({
    'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }),
    'GET /api/v1/auth/captcha': () => ({ status: 200, body: disabledCaptcha }),
  });
}

describe('Register', () => {
  beforeEach(() => {
    mockAuth.register = vi.fn().mockResolvedValue(undefined);
  });

  it('renders heading and submit button', async () => {
    mockConfigAndCaptcha();
    const { findByTestId } = renderWithRouter(() => <Register />);
    expect(await findByTestId('register-heading')).toHaveTextContent('Create an account');
    expect(await findByTestId('register-submit-button')).toHaveTextContent('Register');
  });

  it('renders the username, email, and password inputs', async () => {
    mockConfigAndCaptcha();
    const { findByTestId } = renderWithRouter(() => <Register />);
    expect(await findByTestId('register-username-input')).toBeInTheDocument();
    expect(await findByTestId('register-email-input')).toBeInTheDocument();
    expect(await findByTestId('register-password-input')).toBeInTheDocument();
  });

  it('calls auth.register with form values and captcha fields on submit', async () => {
    mockConfigAndCaptcha();
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'alice' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'password11' } });
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn.disabled).toBe(false));
    fireEvent.submit(await findByTestId('register-form'));
    await waitFor(() =>
      expect(mockAuth.register).toHaveBeenCalledWith({
        username: 'alice',
        displayName: 'alice',
        email: 'a@b.c',
        password: 'password11',
        captchaId: 'disabled',
        captchaAnswer: '',
      }),
    );
  });

  it('shows error when register rejects', async () => {
    mockConfigAndCaptcha();
    mockAuth.register = vi.fn().mockRejectedValue(new Error('Username taken'));
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'alice' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'password11' } });
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn.disabled).toBe(false));
    fireEvent.submit(await findByTestId('register-form'));
    expect(await findByTestId('register-error')).toHaveTextContent('Username taken');
  });

  it('disables submit button while loading', async () => {
    mockConfigAndCaptcha();
    let resolve!: () => void;
    mockAuth.register = vi.fn().mockReturnValue(new Promise<void>(r => { resolve = r; }));
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'a' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'p' } });
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn.disabled).toBe(false));
    fireEvent.submit(await findByTestId('register-form'));
    await waitFor(() => expect(btn).toBeDisabled());
    resolve();
  });

  it('disables submit button until the captcha is answered when captcha is enabled', async () => {
    mockFetch({
      'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }),
      'GET /api/v1/auth/captcha': () => ({
        status: 200,
        body: { captchaId: 'c-1', imageUrl: '/api/v1/auth/captcha/c-1.gif', audioUrl: '/api/v1/auth/captcha/c-1.wav' },
      }),
    });
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'alice' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'password11' } });
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);

    fireEvent.input(await findByTestId('captcha-input'), { target: { value: 'ABCD' } });
    await waitFor(() => expect(btn.disabled).toBe(false));
  });
});
