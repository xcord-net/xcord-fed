import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import GifPicker from './GifPicker';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleGif = {
  id: 'g-1',
  title: 'Cat Wave',
  url: 'https://gifs.example/cat.gif',
  previewUrl: 'https://gifs.example/cat-prev.gif',
  width: 200,
  height: 200,
};

describe('GifPicker', () => {
  it('renders the picker dialog with header and search input', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 200, body: { gifs: [] } }),
    });
    const { getByLabelText, getByText } = render(() => <GifPicker onSelect={() => {}} />);
    expect(getByLabelText('GIF picker')).toBeInTheDocument();
    expect(getByText('GIFs')).toBeInTheDocument();
    expect(getByLabelText('Search GIFs')).toBeInTheDocument();
  });

  it('renders trending GIFs returned by the API', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 200, body: { gifs: [sampleGif] } }),
    });
    const { findByLabelText } = render(() => <GifPicker onSelect={() => {}} />);
    expect(await findByLabelText('Cat Wave')).toBeInTheDocument();
  });

  it('shows empty state when API returns no GIFs', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 200, body: { gifs: [] } }),
    });
    const { findByText, findByTestId } = render(() => <GifPicker onSelect={() => {}} />);
    expect(await findByTestId('gif-picker-empty')).toBeInTheDocument();
  });

  it('shows error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 500, body: { message: 'GIF service down' } }),
    });
    const { findByText } = render(() => <GifPicker onSelect={() => {}} />);
    expect(await findByText(/GIF service down|Failed to load trending GIFs/)).toBeInTheDocument();
  });

  it('invokes onSelect with the gif URL when a tile is clicked', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 200, body: { gifs: [sampleGif] } }),
    });
    const onSelect = vi.fn();
    const { findByLabelText } = render(() => <GifPicker onSelect={onSelect} />);
    fireEvent.click(await findByLabelText('Cat Wave'));
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(sampleGif.url));
  });

  it('calls onClose when Escape is pressed in the search input', async () => {
    mockFetch({
      'GET /api/v1/gifs/trending': () => ({ status: 200, body: { gifs: [] } }),
    });
    const onClose = vi.fn();
    const { getByLabelText } = render(() => <GifPicker onSelect={() => {}} onClose={onClose} />);
    fireEvent.keyDown(getByLabelText('Search GIFs'), { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });
});
