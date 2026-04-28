import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import PinList from './PinList';
import { usePins } from '../stores/pin.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const samplePin = {
  id: 'm-1',
  conversationId: 'c-1',
  authorId: 'u-1',
  authorUsername: 'alice',
  type: 'Default',
  content: 'pinned content',
  isPinned: true,
  createdAt: new Date('2025-01-01').toISOString(),
};

describe('PinList', () => {
  beforeEach(() => {
    usePins().reset();
  });

  it('renders the panel header', () => {
    mockFetch({ 'GET /api/v1/conversations/c-1/pins': () => ({ status: 200, body: { messages: [] } }) });
    const { getByTestId, getByText } = render(() => <PinList conversationId="c-1" />);
    expect(getByTestId('pin-list-panel')).toBeInTheDocument();
    expect(getByText('Pinned Messages')).toBeInTheDocument();
  });

  it('shows empty state when there are no pins', async () => {
    mockFetch({ 'GET /api/v1/conversations/c-1/pins': () => ({ status: 200, body: { messages: [] } }) });
    const { findByTestId } = render(() => <PinList conversationId="c-1" />);
    expect(await findByTestId('pin-list-empty')).toHaveTextContent('No pinned messages');
  });

  it('renders pinned messages from the store', () => {
    mockFetch({ 'GET /api/v1/conversations/c-1/pins': () => ({ status: 200, body: { messages: [samplePin] } }) });
    const { getAllByTestId, container } = render(() => <PinList conversationId="c-1" />);
    return waitFor(() => {
      expect(getAllByTestId('pin-list-item').length).toBe(1);
      expect(container.textContent).toContain('pinned content');
      expect(container.textContent).toContain('alice');
    });
  });

  it('calls unpin endpoint when Unpin button clicked', async () => {
    const fetchState = mockFetch({
      'GET /api/v1/conversations/c-1/pins': () => ({ status: 200, body: { messages: [samplePin] } }),
      'DELETE /api/v1/conversations/c-1/messages/m-1/pin': () => ({ status: 204, body: null }),
    });
    const { findByTestId } = render(() => <PinList conversationId="c-1" />);
    fireEvent.click(await findByTestId('pin-list-unpin-button'));
    await waitFor(() => {
      expect(fetchState.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/messages/m-1/pin'))).toBe(true);
    });
  });

  it('falls back to "U" avatar and "Unknown User" when authorUsername is missing', async () => {
    const noAuthor = { ...samplePin, authorUsername: undefined };
    mockFetch({ 'GET /api/v1/conversations/c-1/pins': () => ({ status: 200, body: { messages: [noAuthor] } }) });
    const { findByTestId, container } = render(() => <PinList conversationId="c-1" />);
    await findByTestId('pin-list-item');
    expect(container.textContent).toContain('Unknown User');
  });
});
