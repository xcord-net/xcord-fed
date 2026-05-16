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

describe('Register', () => {
  beforeEach(() => {
    mockAuth.register = vi.fn().mockResolvedValue(undefined);
  });

  it('renders heading and submit button', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    const { findByTestId } = renderWithRouter(() => <Register />);
    expect(await findByTestId('register-heading')).toHaveTextContent('Create an account');
    expect(await findByTestId('register-submit-button')).toHaveTextContent('Register');
  });

  it('renders the username, email, and password inputs', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    const { findByTestId } = renderWithRouter(() => <Register />);
    expect(await findByTestId('register-username-input')).toBeInTheDocument();
    expect(await findByTestId('register-email-input')).toBeInTheDocument();
    expect(await findByTestId('register-password-input')).toBeInTheDocument();
  });

  it('calls auth.register with form values on submit', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'alice' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'password11' } });
    fireEvent.submit(await findByTestId('register-form'));
    await waitFor(() =>
      expect(mockAuth.register).toHaveBeenCalledWith({
        username: 'alice',
        displayName: 'alice',
        email: 'a@b.c',
        password: 'password11',
      }),
    );
  });

  it('shows error when register rejects', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    mockAuth.register = vi.fn().mockRejectedValue(new Error('Username taken'));
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'alice' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'password11' } });
    fireEvent.submit(await findByTestId('register-form'));
    expect(await findByTestId('register-error')).toHaveTextContent('Username taken');
  });

  it('disables submit button while loading', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    let resolve!: () => void;
    mockAuth.register = vi.fn().mockReturnValue(new Promise<void>(r => { resolve = r; }));
    const { findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(await findByTestId('register-username-input'), { target: { value: 'a' } });
    fireEvent.input(await findByTestId('register-email-input'), { target: { value: 'a@b.c' } });
    fireEvent.input(await findByTestId('register-password-input'), { target: { value: 'p' } });
    fireEvent.submit(await findByTestId('register-form'));
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn).toBeDisabled());
    resolve();
  });
});
