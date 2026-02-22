import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { VanityUrlInfo } from '../components/VanityInvite';
import { validateVanitySlug, buildVanityUrl } from '../components/VanityInvite';

// ---- Pure logic helpers mirrored from VanityInvite ----

function isSlugValid(slug: string): boolean {
  return validateVanitySlug(slug) === null;
}

// ---- Test data ----

const makeVanityInfo = (overrides: Partial<VanityUrlInfo> = {}): VanityUrlInfo => ({
  serverId: 'srv-1',
  slug: 'my-server',
  vanityUrl: '/invite/my-server',
  ...overrides,
});

// ---- Tests ----

describe('VanityInvite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Slug validation ----

  describe('validateVanitySlug', () => {
    it('accepts a valid slug with letters and numbers', () => {
      // Act & Assert
      expect(validateVanitySlug('myserver123')).toBeNull();
    });

    it('accepts a valid slug with hyphens', () => {
      // Act & Assert
      expect(validateVanitySlug('my-server')).toBeNull();
    });

    it('returns error when slug is empty', () => {
      // Act
      const error = validateVanitySlug('');

      // Assert
      expect(error).toBe('Slug is required.');
    });

    it('returns error when slug is too short (< 3 chars)', () => {
      // Act
      const error = validateVanitySlug('ab');

      // Assert
      expect(error).toBe('Slug must be at least 3 characters.');
    });

    it('accepts a slug exactly 3 characters long', () => {
      // Act & Assert
      expect(validateVanitySlug('abc')).toBeNull();
    });

    it('returns error when slug exceeds 32 characters', () => {
      // Arrange
      const longSlug = 'a'.repeat(33);

      // Act
      const error = validateVanitySlug(longSlug);

      // Assert
      expect(error).toBe('Slug must be 32 characters or fewer.');
    });

    it('accepts a slug exactly 32 characters long', () => {
      // Arrange
      const slug = 'a'.repeat(32);

      // Act & Assert
      expect(validateVanitySlug(slug)).toBeNull();
    });

    it('returns error when slug contains invalid characters', () => {
      // Act
      const error = validateVanitySlug('my server!');

      // Assert
      expect(error).toBe('Slug may only contain letters, numbers, and hyphens.');
    });

    it('returns error when slug starts with a hyphen', () => {
      // Act
      const error = validateVanitySlug('-myserver');

      // Assert
      expect(error).toBe('Slug must not start or end with a hyphen.');
    });

    it('returns error when slug ends with a hyphen', () => {
      // Act
      const error = validateVanitySlug('myserver-');

      // Assert
      expect(error).toBe('Slug must not start or end with a hyphen.');
    });

    it('accepts uppercase letters', () => {
      // Act & Assert
      expect(validateVanitySlug('MyServer')).toBeNull();
    });

    it('whitespace-only slug is treated as empty', () => {
      // Act
      const error = validateVanitySlug('   ');

      // Assert
      expect(error).toBe('Slug is required.');
    });
  });

  // ---- URL building ----

  describe('buildVanityUrl', () => {
    it('builds the correct vanity URL path', () => {
      // Act
      const url = buildVanityUrl('my-server');

      // Assert
      expect(url).toBe('/invite/my-server');
    });

    it('includes the slug verbatim in the URL', () => {
      // Act
      const url = buildVanityUrl('gaming-hub');

      // Assert
      expect(url).toContain('gaming-hub');
    });
  });

  // ---- VanityUrlInfo shape ----

  describe('VanityUrlInfo data shape', () => {
    it('has a serverId, slug, and vanityUrl', () => {
      // Arrange
      const info = makeVanityInfo();

      // Assert
      expect(info.serverId).toBe('srv-1');
      expect(info.slug).toBe('my-server');
      expect(info.vanityUrl).toBe('/invite/my-server');
    });

    it('slug can be null when no vanity URL is set', () => {
      // Arrange
      const info = makeVanityInfo({ slug: null, vanityUrl: null });

      // Assert
      expect(info.slug).toBeNull();
      expect(info.vanityUrl).toBeNull();
    });
  });

  // ---- API: save vanity URL ----

  describe('save vanity URL API', () => {
    it('PUT to save vanity URL hits the correct endpoint', async () => {
      // Arrange
      const serverId = 'srv-put-test';
      const updatedInfo = makeVanityInfo({ slug: 'new-slug', vanityUrl: '/invite/new-slug' });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => updatedInfo,
      });

      // Act
      const result = await api.put<VanityUrlInfo>(
        `/api/v1/servers/${serverId}/vanity-url`,
        { slug: 'new-slug' },
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/vanity-url`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ slug: 'new-slug' }),
        }),
      );
      expect(result.slug).toBe('new-slug');
    });

    it('PUT response contains updated vanityUrl', async () => {
      // Arrange
      const updatedInfo = makeVanityInfo({ slug: 'updated', vanityUrl: '/invite/updated' });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => updatedInfo,
      });

      // Act
      const result = await api.put<VanityUrlInfo>(
        '/api/v1/servers/srv-1/vanity-url',
        { slug: 'updated' },
      );

      // Assert
      expect(result.vanityUrl).toBe('/invite/updated');
    });
  });

  // ---- isSlugValid helper ----

  describe('isSlugValid convenience helper', () => {
    it('returns true for a valid slug', () => {
      expect(isSlugValid('valid-slug')).toBe(true);
    });

    it('returns false for an invalid slug', () => {
      expect(isSlugValid('')).toBe(false);
    });

    it('returns false for slug with spaces', () => {
      expect(isSlugValid('invalid slug')).toBe(false);
    });
  });
});
