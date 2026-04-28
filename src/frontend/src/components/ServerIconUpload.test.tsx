import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ServerIconUpload, { validateServerImageFile } from './ServerIconUpload';
import type { Server } from '../types/server';

const server: Server = {
  id: 's-1',
  name: 'Cool Server',
  ownerId: 'u-1',
  createdAt: '2025-01-01T00:00:00Z',
};

describe('validateServerImageFile', () => {
  it('rejects non-image files', () => {
    expect(validateServerImageFile({ type: 'application/pdf', size: 100 })).toBe('Only image files are allowed.');
  });

  it('rejects files larger than 8 MB', () => {
    expect(validateServerImageFile({ type: 'image/png', size: 9 * 1024 * 1024 })).toBe('File size must be 8 MB or less.');
  });

  it('returns null for a valid small image', () => {
    expect(validateServerImageFile({ type: 'image/png', size: 1024 })).toBeNull();
  });
});

describe('ServerIconUpload', () => {
  it('renders icon and banner buttons with hint text', () => {
    const { getByTestId, getAllByText } = render(() => <ServerIconUpload server={server} />);
    expect(getByTestId('server-icon-button')).toBeInTheDocument();
    expect(getByTestId('server-banner-button')).toBeInTheDocument();
    expect(getAllByText(/Images only, max 8 MB/i).length).toBeGreaterThan(0);
  });

  it('shows the server name initial when no iconUrl is set', () => {
    const { getByText } = render(() => <ServerIconUpload server={server} />);
    expect(getByText('C')).toBeInTheDocument();
  });

  it('renders the icon image when iconUrl is provided', () => {
    const withIcon: Server = { ...server, iconUrl: 'https://cdn.example/icon.png' };
    const { getByTestId } = render(() => <ServerIconUpload server={withIcon} />);
    const img = getByTestId('server-icon-preview') as HTMLImageElement;
    expect(img.src).toContain('https://cdn.example/icon.png');
  });

  it('renders the banner image when bannerUrl is provided', () => {
    const withBanner: Server = { ...server, bannerUrl: 'https://cdn.example/banner.png' };
    const { getByTestId } = render(() => <ServerIconUpload server={withBanner} />);
    const img = getByTestId('server-banner-preview') as HTMLImageElement;
    expect(img.src).toContain('https://cdn.example/banner.png');
  });

  it('shows validation error when a non-image file is selected', () => {
    const { getByTestId } = render(() => <ServerIconUpload server={server} />);
    const input = getByTestId('icon-file-input') as HTMLInputElement;
    const file = new File(['x'], 'x.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    fireEvent.change(input);
    expect(getByTestId('upload-error')).toHaveTextContent('Only image files are allowed.');
  });

  it('triggers hidden file input click when icon button clicked', () => {
    const { getByTestId } = render(() => <ServerIconUpload server={server} />);
    const input = getByTestId('icon-file-input') as HTMLInputElement;
    const clickSpy = vi.spyOn(input, 'click');
    fireEvent.click(getByTestId('server-icon-button'));
    expect(clickSpy).toHaveBeenCalled();
  });
});
