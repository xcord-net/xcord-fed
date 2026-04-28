import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ProfileDecorations from './ProfileDecorations';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleDecorations = [
  { id: 'b-1', name: 'Sunset Banner', category: 'banners' },
  { id: 'f-1', name: 'Gold Frame', category: 'frames' },
  { id: 'e-1', name: 'Sparkle Effect', category: 'effects' },
];

describe('ProfileDecorations', () => {
  it('renders the heading and category tabs', async () => {
    mockFetch({
      'GET /api/v1/profile-decorations': () => ({ status: 200, body: [] }),
      'GET /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
    });
    const { findByText } = render(() => <ProfileDecorations />);
    expect(await findByText('Profile Decorations')).toBeInTheDocument();
    expect(await findByText('Profile Banners')).toBeInTheDocument();
    expect(await findByText('Avatar Frames')).toBeInTheDocument();
    expect(await findByText('Profile Effects')).toBeInTheDocument();
  });

  it('shows empty-state text when no decorations are available in active tab', async () => {
    mockFetch({
      'GET /api/v1/profile-decorations': () => ({ status: 200, body: [] }),
      'GET /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
    });
    const { findByText } = render(() => <ProfileDecorations />);
    expect(
      await findByText(/No decorations available in this category/),
    ).toBeInTheDocument();
  });

  it('renders banner decorations on the active (default) tab', async () => {
    mockFetch({
      'GET /api/v1/profile-decorations': () => ({ status: 200, body: sampleDecorations }),
      'GET /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
    });
    const { findByLabelText } = render(() => <ProfileDecorations />);
    expect(await findByLabelText('Select Sunset Banner')).toBeInTheDocument();
  });

  it('switches the visible decorations when clicking a different tab', async () => {
    mockFetch({
      'GET /api/v1/profile-decorations': () => ({ status: 200, body: sampleDecorations }),
      'GET /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
    });
    const { findByLabelText, findByText, queryByLabelText } = render(() => (
      <ProfileDecorations />
    ));
    await findByLabelText('Select Sunset Banner');
    fireEvent.click(await findByText('Avatar Frames'));
    await waitFor(() => {
      expect(queryByLabelText('Select Sunset Banner')).toBeNull();
      expect(queryByLabelText('Select Gold Frame')).not.toBeNull();
    });
  });

  it('saves selections via PUT and shows the success message', async () => {
    const calls = mockFetch({
      'GET /api/v1/profile-decorations': () => ({ status: 200, body: sampleDecorations }),
      'GET /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
      'PUT /api/v1/users/@me/decorations': () => ({ status: 200, body: {} }),
    });
    const { findByLabelText, findByText } = render(() => <ProfileDecorations />);
    fireEvent.click(await findByLabelText('Select Sunset Banner'));
    fireEvent.click(await findByLabelText('Save decorations'));
    await waitFor(() => expect(calls.calls.some(c => c.method === 'PUT')).toBe(true));
    expect(await findByText('Saved!')).toBeInTheDocument();
  });
});
