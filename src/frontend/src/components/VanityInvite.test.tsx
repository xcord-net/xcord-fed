import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import VanityInvite, { validateVanitySlug, buildVanityUrl } from './VanityInvite';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('validateVanitySlug', () => {
  it('rejects empty slug', () => {
    expect(validateVanitySlug('   ')).toBe('Slug is required.');
  });

  it('rejects too short slug', () => {
    expect(validateVanitySlug('ab')).toBe('Slug must be at least 3 characters.');
  });

  it('rejects invalid characters', () => {
    expect(validateVanitySlug('abc!def')).toBe('Slug may only contain letters, numbers, and hyphens.');
  });

  it('rejects leading/trailing hyphens', () => {
    expect(validateVanitySlug('-abc')).toBe('Slug must not start or end with a hyphen.');
  });

  it('returns null for a valid slug', () => {
    expect(validateVanitySlug('my-server')).toBeNull();
  });
});

describe('buildVanityUrl', () => {
  it('prefixes the slug with /invite/', () => {
    expect(buildVanityUrl('foo')).toBe('/invite/foo');
  });
});

describe('VanityInvite', () => {
  it('renders the heading', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/vanity-url': () => ({ status: 200, body: { serverId: 's-1', slug: null, vanityUrl: null } }) });
    const { findByText } = render(() => <VanityInvite serverId="s-1" />);
    expect(await findByText('Vanity Invite URL')).toBeInTheDocument();
  });

  it('shows current vanity URL when slug is set', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/vanity-url': () => ({ status: 200, body: { serverId: 's-1', slug: 'cool', vanityUrl: '/invite/cool' } }),
    });
    const { findByText } = render(() => <VanityInvite serverId="s-1" isOwner />);
    expect(await findByText('/invite/cool')).toBeInTheDocument();
  });

  it('shows no-vanity message when no slug exists', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/vanity-url': () => ({ status: 200, body: { serverId: 's-1', slug: null, vanityUrl: null } }),
    });
    const { findByText } = render(() => <VanityInvite serverId="s-1" isOwner />);
    expect(await findByText(/No vanity URL set/)).toBeInTheDocument();
  });

  it('shows Edit button when isOwner is true', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/vanity-url': () => ({ status: 200, body: { serverId: 's-1', slug: 'a', vanityUrl: '/invite/a' } }),
    });
    const { findByLabelText } = render(() => <VanityInvite serverId="s-1" isOwner />);
    expect(await findByLabelText('Edit Vanity URL')).toBeInTheDocument();
  });

  it('opens edit form when Edit button clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/vanity-url': () => ({ status: 200, body: { serverId: 's-1', slug: 'a', vanityUrl: '/invite/a' } }),
    });
    const { findByLabelText } = render(() => <VanityInvite serverId="s-1" isOwner />);
    fireEvent.click(await findByLabelText('Edit Vanity URL'));
    expect(await findByLabelText('Vanity URL slug')).toBeInTheDocument();
  });
});
