import { describe, it, expect } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import ConfirmEmail from './ConfirmEmail';
import { mockFetch } from '../tests/helpers/mockFetch';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';

describe('ConfirmEmail', () => {
  it('renders heading and submit button', () => {
    const { getByTestId } = renderWithRouter(() => <ConfirmEmail />, { path: '/confirm-email' });
    expect(getByTestId('confirm-email-heading')).toHaveTextContent('Confirm your email');
    expect(getByTestId('confirm-email-submit-button')).toBeInTheDocument();
  });

  it('disables submit button when code is not 6 digits', () => {
    const { getByTestId } = renderWithRouter(() => <ConfirmEmail />, { path: '/confirm-email' });
    const btn = getByTestId('confirm-email-submit-button') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });

  it('enables submit button when code is exactly 6 chars', async () => {
    const { container, getByTestId } = renderWithRouter(() => <ConfirmEmail />, { path: '/confirm-email' });
    const input = container.querySelector('#confirmation-code') as HTMLInputElement;
    fireEvent.input(input, { target: { value: '123456' } });
    const btn = getByTestId('confirm-email-submit-button') as HTMLButtonElement;
    await waitFor(() => expect(btn.disabled).toBe(false));
  });

  it('shows error when API rejects the code', async () => {
    mockFetch({
      'POST /api/v1/auth/confirm-email': () => ({ status: 400, body: { message: 'Invalid code' } }),
    });
    const { container, getByTestId, findByText } = renderWithRouter(() => <ConfirmEmail />, { path: '/confirm-email' });
    const input = container.querySelector('#confirmation-code') as HTMLInputElement;
    fireEvent.input(input, { target: { value: '123456' } });
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(await findByText(/Invalid code|Invalid confirmation code/)).toBeInTheDocument();
    // Submit button should be visible again after the error.
    expect(getByTestId('confirm-email-submit-button')).toBeInTheDocument();
  });
});
