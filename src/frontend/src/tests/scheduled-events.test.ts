import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { ScheduledEvent } from '../components/ScheduledEvents';
import {
  formatEventDate,
  formatEventTime,
  sortEventsByStartTime,
  toggleInterestedState,
  isEventFormValid,
} from '../components/ScheduledEvents';

// ---- Test data ----

const makeEvent = (overrides: Partial<ScheduledEvent> = {}): ScheduledEvent => ({
  id: 'evt-1',
  serverId: 'srv-1',
  name: 'Weekly Standup',
  description: 'Team sync every Monday',
  scheduledStartTime: new Date('2026-03-01T10:00:00Z').toISOString(),
  scheduledEndTime: new Date('2026-03-01T10:30:00Z').toISOString(),
  channelId: '123',
  status: 'Scheduled',
  interestedCount: 5,
  isInterested: false,
  createdAt: new Date('2026-02-01T00:00:00Z').toISOString(),
  ...overrides,
});

// ---- Tests ----

describe('ScheduledEvents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });


  // ---- Date/time formatting ----

  describe('date/time formatting', () => {
    const testDate = '2026-03-15T14:30:00Z';

    it('formatEventDate returns a string containing the month name and day', () => {
      // Act
      const result = formatEventDate(testDate);

      // Assert - must contain both the month name and the numeric day
      // "March 15", "Mar 15", "15 March" etc. are all acceptable
      expect(result).toMatch(/mar/i);
      expect(result).toMatch(/15/);
    });

    it('formatEventDate includes the day of month', () => {
      // Act
      const result = formatEventDate(testDate);

      // Assert - "15" should appear somewhere in the formatted string
      expect(result).toMatch(/15/);
    });

    it('formatEventTime returns a string matching HH:MM time format', () => {
      // Act
      const result = formatEventTime(testDate);

      // Assert - must match a recognisable time pattern such as "2:30 PM" or "14:30"
      expect(result).toMatch(/\d{1,2}:\d{2}/);
    });

    it('formatEventTime contains a colon separator (HH:MM format)', () => {
      // Act
      const result = formatEventTime(testDate);

      // Assert
      expect(result).toMatch(/:/);
    });

    it('events are sorted by start time ascending', () => {
      // Arrange
      const later = makeEvent({ id: 'e1', scheduledStartTime: new Date('2026-04-01T10:00:00Z').toISOString() });
      const earlier = makeEvent({ id: 'e2', scheduledStartTime: new Date('2026-03-01T10:00:00Z').toISOString() });
      const events = [later, earlier];

      // Act
      const sorted = sortEventsByStartTime(events);

      // Assert
      expect(sorted[0].id).toBe('e2');
      expect(sorted[1].id).toBe('e1');
    });
  });

  // ---- RSVP / Interested toggle ----

  describe('RSVP toggle logic', () => {
    it('toggling interested when not interested marks as interested', () => {
      // Arrange
      const event = makeEvent({ isInterested: false, interestedCount: 5 });

      // Act
      const updated = toggleInterestedState(event);

      // Assert
      expect(updated.isInterested).toBe(true);
      expect(updated.interestedCount).toBe(6);
    });

    it('toggling interested when already interested removes interest', () => {
      // Arrange
      const event = makeEvent({ isInterested: true, interestedCount: 5 });

      // Act
      const updated = toggleInterestedState(event);

      // Assert
      expect(updated.isInterested).toBe(false);
      expect(updated.interestedCount).toBe(4);
    });

    it('toggleInterestedState does not mutate the original event', () => {
      // Arrange
      const original = makeEvent({ isInterested: false, interestedCount: 3 });

      // Act
      toggleInterestedState(original);

      // Assert - original unchanged
      expect(original.isInterested).toBe(false);
      expect(original.interestedCount).toBe(3);
    });

    it('RSVP API call hits correct endpoint', async () => {
      // Arrange
      const serverId = 'srv-1';
      const eventId = 'evt-1';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true, status: 204 });

      // Act
      await api.put(`/api/v1/servers/${serverId}/events/${eventId}/rsvp`, {
        interested: true,
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/events/${eventId}/rsvp`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ interested: true }),
        }),
      );
    });
  });

  // ---- Create event form validation ----

  describe('create event form validation', () => {
    it('form is invalid when title is empty', () => {
      expect(isEventFormValid('', '2026-03-01T10:00')).toBe(false);
    });

    it('form is invalid when startTime is empty', () => {
      expect(isEventFormValid('My Event', '')).toBe(false);
    });

    it('form is valid when both title and startTime are provided', () => {
      expect(isEventFormValid('My Event', '2026-03-01T10:00')).toBe(true);
    });

    it('whitespace-only title is treated as invalid', () => {
      expect(isEventFormValid('   ', '2026-03-01T10:00')).toBe(false);
    });

    it('create event POST hits the correct URL', async () => {
      // Arrange
      const serverId = 'srv-post-test';
      const newEvent = makeEvent({ id: 'evt-new', name: 'New Event' });

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => newEvent,
      });

      // Act
      const result = await api.post<ScheduledEvent>(`/api/v1/servers/${serverId}/events`, {
        name: 'New Event',
        scheduledStartTime: new Date('2026-03-01T10:00:00Z').toISOString(),
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/events`,
        expect.objectContaining({
          method: 'POST',
        }),
      );
      expect(result.name).toBe('New Event');
    });
  });

});
