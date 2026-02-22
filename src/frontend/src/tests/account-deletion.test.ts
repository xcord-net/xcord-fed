import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import {
  validateDeletionRequest,
  formatDeletionDate,
} from '../components/AccountDeletion';

describe('account-deletion', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.setAuthenticated(false);
  });

  describe('confirmation dialog', () => {
    it('should require password in confirmation dialog', () => {
      const error = validateDeletionRequest('');
      expect(error).toBe('Password is required');
    });

    it('should not show error when password is provided', () => {
      const error = validateDeletionRequest('mypassword123');
      expect(error).toBe('');
    });

    it('should reject whitespace-only password', () => {
      const error = validateDeletionRequest('   ');
      expect(error).toBe('Password is required');
    });
  });

  describe('pending deletion state', () => {
    it('should display formatted deletion date when deletion is pending', () => {
      const scheduledDeletionAt = '2026-03-04T12:00:00Z';
      const formatted = formatDeletionDate(scheduledDeletionAt);
      expect(formatted.length).toBeGreaterThan(0);
      expect(formatted).toContain('2026');
    });
  });

  describe('API calls - schedule deletion', () => {
    it('should call delete endpoint with password on valid input', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ scheduledDeletionAt: '2026-03-04T12:00:00Z' }),
      });

      await api.post<{ scheduledDeletionAt: string }>(
        '/api/v1/users/@me/delete',
        { password: 'mypassword123' }
      );

      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/delete',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ password: 'mypassword123' }),
        })
      );
    });

    it('should not call API when password validation fails', async () => {
      globalThis.fetch = vi.fn();

      const error = validateDeletionRequest('');
      expect(error).toBe('Password is required');
      expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('should return error from API on incorrect password', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Password is incorrect' }),
      });

      await expect(
        api.post('/api/v1/users/@me/delete', { password: 'wrongpassword' })
      ).rejects.toMatchObject({ error: 'Password is incorrect' });
    });

    it('should return generic error when API returns no message', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({}),
      });

      await expect(
        api.post('/api/v1/users/@me/delete', { password: 'mypassword123' })
      ).rejects.toBeDefined();
    });
  });

  describe('API calls - cancel deletion', () => {
    it('should call cancel-deletion endpoint', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      });

      await api.post('/api/v1/users/@me/cancel-deletion');

      expect(globalThis.fetch).toHaveBeenCalledWith(
        '/api/v1/users/@me/cancel-deletion',
        expect.objectContaining({ method: 'POST' })
      );
    });

    it('should return error when cancellation fails', async () => {
      api.setAuthenticated(true);
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'No deletion is scheduled' }),
      });

      await expect(
        api.post('/api/v1/users/@me/cancel-deletion')
      ).rejects.toMatchObject({ error: 'No deletion is scheduled' });
    });
  });
});
