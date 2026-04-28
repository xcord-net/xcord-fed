import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import TwoFactorSetup from './TwoFactorSetup';
import { useModals } from '../stores/modal.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('TwoFactorSetup', () => {
  beforeEach(() => {
    useModals().reset();
  });

  it('renders the heading and description', () => {
    const { getByText } = render(() => <TwoFactorSetup />);
    expect(getByText('Two-Factor Authentication')).toBeInTheDocument();
    expect(getByText(/extra layer of security/i)).toBeInTheDocument();
  });

  it('shows Disabled status and Enable button when 2FA is not enabled', () => {
    const { getByTestId } = render(() => <TwoFactorSetup twoFactorEnabled={false} />);
    expect(getByTestId('2fa-status-indicator').getAttribute('data-2fa-enabled')).toBe(
      'false',
    );
    expect(getByTestId('2fa-enable-button')).toHaveTextContent(/Enable 2FA/i);
  });

  it('shows Enabled status and Disable button when 2FA is already on', () => {
    const { getByTestId } = render(() => <TwoFactorSetup twoFactorEnabled={true} />);
    expect(getByTestId('2fa-status-indicator').getAttribute('data-2fa-enabled')).toBe(
      'true',
    );
    expect(getByTestId('2fa-disable-button')).toHaveTextContent(/Disable 2FA/i);
  });

  it('clicking Enable transitions into the verification-code phase', async () => {
    mockFetch({
      'POST /api/v1/auth/2fa/enable': () => ({ status: 204, body: null }),
    });
    const { getByTestId, findByTestId } = render(() => (
      <TwoFactorSetup twoFactorEnabled={false} />
    ));
    fireEvent.click(getByTestId('2fa-enable-button'));
    expect(await findByTestId('2fa-code-input')).toBeInTheDocument();
    expect(await findByTestId('2fa-verify-button')).toBeInTheDocument();
  });

  it('shows an error when Verify is clicked with an empty code', async () => {
    mockFetch({
      'POST /api/v1/auth/2fa/enable': () => ({ status: 204, body: null }),
    });
    const { getByTestId, findByTestId } = render(() => (
      <TwoFactorSetup twoFactorEnabled={false} />
    ));
    fireEvent.click(getByTestId('2fa-enable-button'));
    fireEvent.click(await findByTestId('2fa-verify-button'));
    expect(await findByTestId('2fa-error')).toHaveTextContent(
      /Please enter the verification code/i,
    );
  });

  it('clicking Disable transitions to the password-confirm phase', async () => {
    const { getByTestId, findByTestId } = render(() => (
      <TwoFactorSetup twoFactorEnabled={true} />
    ));
    fireEvent.click(getByTestId('2fa-disable-button'));
    expect(await findByTestId('2fa-disable-password-input')).toBeInTheDocument();
    expect(await findByTestId('2fa-disable-confirm-button')).toBeInTheDocument();
  });

  it('disable flow succeeds and sets the success message', async () => {
    const calls = mockFetch({
      'POST /api/v1/auth/2fa/disable': () => ({ status: 204, body: null }),
    });
    const { getByTestId, findByTestId } = render(() => (
      <TwoFactorSetup twoFactorEnabled={true} />
    ));
    fireEvent.click(getByTestId('2fa-disable-button'));
    fireEvent.input(await findByTestId('2fa-disable-password-input'), {
      target: { value: 'hunter22' },
    });
    fireEvent.click(await findByTestId('2fa-disable-confirm-button'));
    await waitFor(() =>
      expect(
        calls.calls.some(
          c => c.method === 'POST' && c.url === '/api/v1/auth/2fa/disable',
        ),
      ).toBe(true),
    );
    expect(await findByTestId('2fa-success')).toHaveTextContent(/disabled successfully/i);
  });
});
