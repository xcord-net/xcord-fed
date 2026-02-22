import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import { validatePasswordChange } from '../components/PasswordChangeForm';

async function submitPasswordChange(
  currentPassword: string,
  newPassword: string,
  confirmPassword: string
): Promise<{ error: string; success: boolean }> {
  const validationError = validatePasswordChange(currentPassword, newPassword, confirmPassword);
  if (validationError) {
    return { error: validationError, success: false };
  }

  try {
    await api.post('/api/v1/auth/change-password', { currentPassword, newPassword });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Failed to change password', success: false };
  }
}

describe('password-change', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.setAuthenticated(false);
  });

  describe('validation', () => {
    it('should return error when passwords do not match', () => {
      // Arrange / Act
      const error = validatePasswordChange('current123', 'newpass123', 'different123');

      // Assert
      expect(error).toBe('New passwords do not match');
    });

    it('should return error when new password is too short', () => {
      // Arrange / Act
      const error = validatePasswordChange('current123', 'short', 'short');

      // Assert
      expect(error).toBe('New password must be at least 8 characters');
    });

    it('should return error when new password matches current password', () => {
      // Arrange / Act
      const error = validatePasswordChange('samepass1', 'samepass1', 'samepass1');

      // Assert
      expect(error).toBe('New password must be different from current password');
    });

    it('should return no error for valid input', () => {
      // Arrange / Act
      const error = validatePasswordChange('oldpass123', 'newpass456', 'newpass456');

      // Assert
      expect(error).toBe('');
    });
  });

  describe('API interaction', () => {
    it('should call change-password endpoint with correct payload on valid input', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      const result = await submitPasswordChange('oldpass123', 'newpass456', 'newpass456');

      // Assert
      expect(result.success).toBe(true);
      expect(result.error).toBe('');
      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/auth/change-password',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ currentPassword: 'oldpass123', newPassword: 'newpass456' }),
        })
      );
    });

    it('should not call API when validation fails', async () => {
      // Arrange
      globalThis.fetch = vi.fn();

      // Act
      const result = await submitPasswordChange('oldpass123', 'newpass456', 'mismatch');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('New passwords do not match');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should display error message from API on failure', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Current password is incorrect' }),
      });

      // Act
      const result = await submitPasswordChange('wrongcurrent', 'newpass456', 'newpass456');

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Current password is incorrect');
    });

    it('should return success and signal form should clear on successful change', async () => {
      // Arrange
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      // Act
      const result = await submitPasswordChange('oldpass123', 'newpass456', 'newpass456');

      // Assert — success signals the form should clear its fields
      expect(result.success).toBe(true);
      expect(result.error).toBe('');
    });
  });
});
