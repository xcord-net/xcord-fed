import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';

// Logic mirroring TwoFactorSetup.tsx — extracted for unit testing

async function enableTwoFactor(): Promise<{ error: string; success: boolean }> {
  try {
    await api.post('/api/v1/auth/2fa/enable');
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Failed to initiate 2FA setup', success: false };
  }
}

async function confirmEnableTwoFactor(
  code: string
): Promise<{ error: string; success: boolean }> {
  if (!code.trim()) {
    return { error: 'Please enter the verification code', success: false };
  }
  try {
    await api.post('/api/v1/auth/2fa/confirm-enable', { code: code.trim() });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Invalid verification code', success: false };
  }
}

async function disableTwoFactor(
  password: string
): Promise<{ error: string; success: boolean }> {
  if (!password.trim()) {
    return { error: 'Please enter your current password', success: false };
  }
  try {
    await api.post('/api/v1/auth/2fa/disable', { currentPassword: password.trim() });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Invalid password', success: false };
  }
}

describe('two-factor', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.setAuthenticated(false);
  });

  describe('enable flow', () => {
    it('should call enable endpoint to initiate setup', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      const result = await enableTwoFactor();

      // Assert
      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/enable',
        expect.objectContaining({ method: 'POST' })
      );
    });

    it('should return error when enable endpoint fails', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
      });

      // Act
      const result = await enableTwoFactor();

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Server error');
    });
  });

  describe('confirm-enable flow', () => {
    it('should call confirm-enable endpoint with code', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      const result = await confirmEnableTwoFactor('123456');

      // Assert
      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/confirm-enable',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ code: '123456' }),
        })
      );
    });

    it('should return error when code is empty', async () => {
      // Arrange
      globalThis.fetch = vi.fn();

      // Act
      const result = await confirmEnableTwoFactor('');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Please enter the verification code');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should return error when code is whitespace only', async () => {
      // Arrange
      globalThis.fetch = vi.fn();

      // Act
      const result = await confirmEnableTwoFactor('   ');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Please enter the verification code');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should show error from API when confirm code is invalid', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Code has expired' }),
      });

      // Act
      const result = await confirmEnableTwoFactor('000000');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Code has expired');
    });

    it('should fall back to generic error when API returns no message', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({}),
      });

      // Act
      const result = await confirmEnableTwoFactor('000000');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Invalid verification code');
    });
  });

  describe('disable flow', () => {
    it('should call disable endpoint with current password', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      const result = await disableTwoFactor('mypassword');

      // Assert
      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/disable',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ currentPassword: 'mypassword' }),
        })
      );
    });

    it('should return error when disable password is empty', async () => {
      // Arrange
      globalThis.fetch = vi.fn();

      // Act
      const result = await disableTwoFactor('');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Please enter your current password');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should show error from API when disable password is invalid', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Invalid code' }),
      });

      // Act
      const result = await disableTwoFactor('wrongpassword');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Invalid code');
    });

    it('should fall back to generic error when API returns no message on disable', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({}),
      });

      // Act
      const result = await disableTwoFactor('wrongpassword');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Invalid password');
    });
  });

  describe('loading state', () => {
    it('should resolve successfully indicating loading completed during enable', async () => {
      // Arrange
      api.setAuthenticated(true);
      let resolveRequest!: () => void;
      globalThis.fetch = vi.fn().mockReturnValue(
        new Promise<Response>((resolve) => {
          resolveRequest = () =>
            resolve({
              ok: true,
              status: 204,
            } as Response);
        })
      );

      // Act — start the request but do not await yet
      const pending = enableTwoFactor();

      // Assert — fetch was called (loading began)
      expect(globalThis.fetch).toHaveBeenCalledTimes(1);

      // Complete the request
      resolveRequest();
      const result = await pending;

      // Assert — loading finished and resolved successfully
      expect(result.success).toBe(true);
    });

    it('should resolve successfully indicating loading completed during confirm-enable', async () => {
      // Arrange
      api.setAuthenticated(true);
      let resolveRequest!: () => void;
      globalThis.fetch = vi.fn().mockReturnValue(
        new Promise<Response>((resolve) => {
          resolveRequest = () =>
            resolve({
              ok: true,
              status: 204,
            } as Response);
        })
      );

      // Act
      const pending = confirmEnableTwoFactor('123456');

      // Assert — fetch was called
      expect(globalThis.fetch).toHaveBeenCalledTimes(1);

      resolveRequest();
      const result = await pending;

      expect(result.success).toBe(true);
    });
  });
});
