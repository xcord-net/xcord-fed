import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import AvatarUpload, { validateAvatarFile } from './AvatarUpload';
import type { UserProfile } from '../types/profile';

const profile: UserProfile = {
  userId: 'u-1',
  username: 'octocat',
  displayName: 'Octo Cat',
  avatarUrl: undefined,
  createdAt: '2025-01-01T00:00:00Z',
};

describe('validateAvatarFile', () => {
  it('rejects non-image files', () => {
    expect(validateAvatarFile({ type: 'application/pdf', size: 100 })).toBe('Only image files are allowed.');
  });

  it('rejects files larger than 8 MB', () => {
    expect(validateAvatarFile({ type: 'image/png', size: 9 * 1024 * 1024 })).toBe('File size must be 8 MB or less.');
  });

  it('returns null for a valid small image', () => {
    expect(validateAvatarFile({ type: 'image/png', size: 1024 })).toBeNull();
  });
});

describe('AvatarUpload', () => {
  it('renders displayName and username from profile', () => {
    const { getByText } = render(() => <AvatarUpload profile={profile} />);
    expect(getByText('Octo Cat')).toBeInTheDocument();
    expect(getByText('@octocat')).toBeInTheDocument();
  });

  it('renders the avatar change button with hint text', () => {
    const { getByTestId, getByText } = render(() => <AvatarUpload profile={profile} />);
    expect(getByTestId('avatar-button')).toBeInTheDocument();
    expect(getByText('Images only, max 8 MB')).toBeInTheDocument();
  });

  it('shows the username initial when no avatar URL is set', () => {
    const { getByText } = render(() => <AvatarUpload profile={profile} />);
    expect(getByText('O')).toBeInTheDocument();
  });

  it('renders the image preview when avatarUrl is provided', () => {
    const { getByTestId } = render(() => (
      <AvatarUpload profile={{ ...profile, avatarUrl: 'https://cdn.example/x.png' }} />
    ));
    const img = getByTestId('avatar-preview') as HTMLImageElement;
    expect(img).toBeInTheDocument();
    expect(img.src).toContain('https://cdn.example/x.png');
  });

  it('exposes a hidden file input that accepts images', () => {
    const { getByTestId } = render(() => <AvatarUpload profile={profile} />);
    const input = getByTestId('avatar-file-input') as HTMLInputElement;
    expect(input.type).toBe('file');
    expect(input.accept).toBe('image/*');
  });
});
