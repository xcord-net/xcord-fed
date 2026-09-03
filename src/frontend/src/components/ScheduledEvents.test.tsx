import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ScheduledEvents, {
  sortEventsByStartTime,
  toggleInterestedState,
  isEventFormValid,
  formatEventDate,
} from './ScheduledEvents';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleEvent = {
  id: 'e-1',
  serverId: 's-1',
  name: 'Game Night',
  description: 'Friday game night',
  scheduledStartTime: '2030-06-15T19:00:00Z',
  scheduledEndTime: '2030-06-15T22:00:00Z',
  status: 'Scheduled',
  interestedCount: 3,
  createdAt: '2025-01-01T00:00:00Z',
  location: 'https://example.com/game',
};

describe('ScheduledEvents pure helpers', () => {
  it('sortEventsByStartTime sorts ascending by start time', () => {
    const a = { id: '1', scheduledStartTime: '2030-01-02T00:00:00Z' };
    const b = { id: '2', scheduledStartTime: '2030-01-01T00:00:00Z' };
    const sorted = sortEventsByStartTime([a, b] as never);
    expect(sorted[0].id).toBe('2');
  });

  it('toggleInterestedState flips isInterested and adjusts count', () => {
    const flipped = toggleInterestedState({ ...sampleEvent, isInterested: false } as never);
    expect(flipped.isInterested).toBe(true);
    expect(flipped.interestedCount).toBe(4);
    const back = toggleInterestedState(flipped);
    expect(back.isInterested).toBe(false);
    expect(back.interestedCount).toBe(3);
  });

  it('isEventFormValid requires title and start time', () => {
    expect(isEventFormValid('', '2030-01-01T00:00')).toBe(false);
    expect(isEventFormValid('Hello', '')).toBe(false);
    expect(isEventFormValid('Hello', '2030-01-01T00:00')).toBe(true);
  });

  it('formatEventDate returns a localized date string containing the year', () => {
    expect(formatEventDate('2030-06-15T19:00:00Z')).toMatch(/2030/);
  });
});

describe('ScheduledEvents', () => {
  it('renders the Scheduled Events header and Create button', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => <ScheduledEvents serverId="s-1" />);
    expect(await findByText('Scheduled Events')).toBeInTheDocument();
    expect(await findByTestId('scheduled-events-create-button')).toBeInTheDocument();
  });

  it('shows the empty state when there are no events', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => <ScheduledEvents serverId="s-1" />);
    expect(await findByTestId('scheduled-events-empty')).toBeInTheDocument();
  });

  it('renders an event item with name and interested count', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [sampleEvent] }),
    });
    const { findByText, findByTestId } = render(() => <ScheduledEvents serverId="s-1" />);
    expect(await findByTestId('scheduled-event-item')).toBeInTheDocument();
    expect(await findByText('Game Night')).toBeInTheDocument();
    expect(await findByText('3 interested')).toBeInTheDocument();
  });

  it('reveals the create form when the Create Event button is clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [] }),
    });
    const { findByTestId, queryByTestId } = render(() => <ScheduledEvents serverId="s-1" />);
    expect(queryByTestId('scheduled-events-form')).toBeNull();
    fireEvent.click(await findByTestId('scheduled-events-create-button'));
    expect(await findByTestId('scheduled-events-form')).toBeInTheDocument();
  });

  it('disables the form submit button until name and start time are provided', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <ScheduledEvents serverId="s-1" />);
    fireEvent.click(await findByTestId('scheduled-events-create-button'));
    const submit = (await findByTestId('scheduled-events-form-submit')) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
  });

  it('issues PUT /events/:id/rsvp when Interested is toggled', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/events': () => ({ status: 200, body: [sampleEvent] }),
      'PUT /api/v1/servers/s-1/events/e-1/rsvp': () => ({ status: 204, body: null }),
    });
    const { findByLabelText } = render(() => <ScheduledEvents serverId="s-1" />);
    fireEvent.click(await findByLabelText('Mark as interested'));
    await waitFor(() =>
      expect(
        calls.calls.some(
          (c) => c.method === 'PUT' && c.url === '/api/v1/servers/s-1/events/e-1/rsvp',
        ),
      ).toBe(true),
    );
  });
});
