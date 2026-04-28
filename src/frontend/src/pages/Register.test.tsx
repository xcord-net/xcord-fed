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
    const { container } = renderWithRouter(() => <Register />);
    expect(container.querySelector('#reg-username')).toBeInTheDocument();
    expect(container.querySelector('#reg-email')).toBeInTheDocument();
    expect(container.querySelector('#reg-password')).toBeInTheDocument();
  });

  it('calls auth.register with form values on submit', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    const { container } = renderWithRouter(() => <Register />);
    fireEvent.input(container.querySelector('#reg-username')!, { target: { value: 'alice' } });
    fireEvent.input(container.querySelector('#reg-email')!, { target: { value: 'a@b.c' } });
    fireEvent.input(container.querySelector('#reg-password')!, { target: { value: 'password11' } });
    fireEvent.submit(container.querySelector('form')!);
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
    const { container, findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(container.querySelector('#reg-username')!, { target: { value: 'alice' } });
    fireEvent.input(container.querySelector('#reg-email')!, { target: { value: 'a@b.c' } });
    fireEvent.input(container.querySelector('#reg-password')!, { target: { value: 'password11' } });
    fireEvent.submit(container.querySelector('form')!);
    expect(await findByTestId('register-error')).toHaveTextContent('Username taken');
  });

  it('disables submit button while loading', async () => {
    mockFetch({ 'GET /api/v1/config': () => ({ status: 200, body: { registrationEnabled: true } }) });
    let resolve!: () => void;
    mockAuth.register = vi.fn().mockReturnValue(new Promise<void>(r => { resolve = r; }));
    const { container, findByTestId } = renderWithRouter(() => <Register />);
    fireEvent.input(container.querySelector('#reg-username')!, { target: { value: 'a' } });
    fireEvent.input(container.querySelector('#reg-email')!, { target: { value: 'a@b.c' } });
    fireEvent.input(container.querySelector('#reg-password')!, { target: { value: 'p' } });
    fireEvent.submit(container.querySelector('form')!);
    const btn = await findByTestId('register-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn).toBeDisabled());
    resolve();
  });
});
