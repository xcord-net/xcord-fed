import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import StickerPicker, {
  filterStickersByQuery,
  groupStickersByServer,
  validateStickerName,
} from './StickerPicker';
import type { Sticker } from './StickerPicker';
import { mockFetch } from '../tests/helpers/mockFetch';

const samplePack = {
  id: 'p-1',
  serverId: 's-1',
  name: 'Default',
  description: 'Default sticker pack',
  createdAt: '2025-01-01T00:00:00Z',
  stickers: [
    {
      id: 'sk-1',
      name: 'cat',
      tags: 'animal,cute',
      imageUrl: 'http://example.com/cat.png',
      createdAt: '2025-01-01T00:00:00Z',
    },
  ],
};

const sticker = (over: Partial<Sticker> = {}): Sticker => ({
  id: 'sk-1',
  serverId: 's-1',
  name: 'cat',
  imageUrl: 'http://example.com/cat.png',
  tags: ['animal'],
  createdAt: '2025-01-01T00:00:00Z',
  ...over,
});

describe('StickerPicker pure helpers', () => {
  it('filterStickersByQuery returns all when query is empty', () => {
    const list = [sticker(), sticker({ id: 'sk-2', name: 'dog', tags: ['animal'] })];
    expect(filterStickersByQuery(list, '')).toHaveLength(2);
  });

  it('filterStickersByQuery matches name, tags, and description', () => {
    const list = [
      sticker(),
      sticker({ id: 'sk-2', name: 'dog', tags: ['friendly'] }),
    ];
    expect(filterStickersByQuery(list, 'cat')).toHaveLength(1);
    expect(filterStickersByQuery(list, 'friendly')).toHaveLength(1);
  });

  it('groupStickersByServer groups by serverId', () => {
    const list = [
      sticker({ serverId: 's-1' }),
      sticker({ id: 'sk-2', serverId: 's-2' }),
    ];
    const groups = groupStickersByServer(list);
    expect(groups).toHaveLength(2);
    expect(groups[0].stickers).toHaveLength(1);
  });

  it('validateStickerName enforces length rules', () => {
    expect(validateStickerName('')).toMatch(/required/i);
    expect(validateStickerName('a')).toMatch(/2 characters/);
    expect(validateStickerName('a'.repeat(33))).toMatch(/32/);
    expect(validateStickerName('happy')).toBeNull();
  });
});

describe('StickerPicker', () => {
  it('renders the Stickers header and search input', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => (
      <StickerPicker serverId="s-1" />
    ));
    expect(await findByText('Stickers')).toBeInTheDocument();
    expect(await findByTestId('sticker-search-input')).toBeInTheDocument();
  });

  it('shows "No stickers found" empty state when there are no stickers', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <StickerPicker serverId="s-1" />);
    expect(await findByTestId('sticker-picker-empty-state')).toHaveTextContent(
      'No stickers yet',
    );
  });

  it('renders a sticker item from the loaded packs', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [samplePack] }),
    });
    const { findByTestId } = render(() => <StickerPicker serverId="s-1" />);
    expect(await findByTestId('sticker-item-sk-1')).toBeInTheDocument();
    expect(await findByTestId('sticker-select-button-sk-1')).toBeInTheDocument();
  });

  it('shows the upload toggle only when canManage is true', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [] }),
    });
    const { findByTestId, queryByTestId } = render(() => (
      <StickerPicker serverId="s-1" canManage={true} />
    ));
    expect(await findByTestId('sticker-upload-toggle-button')).toBeInTheDocument();
    expect(queryByTestId('sticker-upload-panel')).toBeNull();
  });

  it('reveals the upload form when the upload toggle is clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => (
      <StickerPicker serverId="s-1" canManage={true} />
    ));
    fireEvent.click(await findByTestId('sticker-upload-toggle-button'));
    expect(await findByTestId('sticker-upload-panel')).toBeInTheDocument();
    expect(await findByTestId('sticker-name-input')).toBeInTheDocument();
  });

  it('invokes onSelect when a sticker is clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/sticker-packs': () => ({ status: 200, body: [samplePack] }),
    });
    let picked: Sticker | null = null;
    const { findByTestId } = render(() => (
      <StickerPicker serverId="s-1" onSelect={(s) => { picked = s; }} />
    ));
    fireEvent.click(await findByTestId('sticker-select-button-sk-1'));
    await waitFor(() => expect(picked?.id).toBe('sk-1'));
  });
});
