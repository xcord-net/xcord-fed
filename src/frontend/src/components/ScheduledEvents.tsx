import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface ScheduledEvent {
  id: string;
  serverId: string;
  name: string;
  description?: string;
  channelId?: string;
  location?: string;
  scheduledStartTime: string;
  scheduledEndTime?: string;
  status: string;
  interestedCount: number;
  createdAt: string;
  // Client-only state (not returned by API)
  isInterested?: boolean;
}

interface ScheduledEventsProps {
  serverId: string;
}

// ---- Helpers ----

function formatEventDate(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleDateString([], {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function formatEventTime(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function formatDateTimeLocal(dateString: string): string {
  const date = new Date(dateString);
  // datetime-local input format: YYYY-MM-DDTHH:MM
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

// ---- Component ----

export default function ScheduledEvents(props: ScheduledEventsProps) {
  const [events, setEvents] = createSignal<ScheduledEvent[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);

  // Create form state
  const [formTitle, setFormTitle] = createSignal('');
  const [formDescription, setFormDescription] = createSignal('');
  const [formStartTime, setFormStartTime] = createSignal('');
  const [formEndTime, setFormEndTime] = createSignal('');
  const [formLocationType, setFormLocationType] = createSignal<'VoiceChannel' | 'External'>('External');
  const [formLocationChannelId, setFormLocationChannelId] = createSignal('');
  const [formLocationExternalUrl, setFormLocationExternalUrl] = createSignal('');

  const loadEvents = async (serverId: string) => {
    setIsLoading(true);
    try {
      const data = await api.get<ScheduledEvent[]>(`/api/v1/servers/${serverId}/events`);
      // Sort by start time ascending
      const sorted = [...data].sort(
        (a, b) => new Date(a.scheduledStartTime).getTime() - new Date(b.scheduledStartTime).getTime(),
      );
      setEvents(sorted);
    } catch {
      setEvents([]);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadEvents(serverId);
    }
  });

  const toggleInterested = async (event: ScheduledEvent) => {
    // Optimistic update
    setEvents((prev) =>
      prev.map((e) =>
        e.id === event.id
          ? {
              ...e,
              isInterested: !e.isInterested,
              interestedCount: e.isInterested ? e.interestedCount - 1 : e.interestedCount + 1,
            }
          : e,
      ),
    );

    try {
      await api.put(`/api/v1/servers/${props.serverId}/events/${event.id}/rsvp`, {
        interested: !event.isInterested,
      });
    } catch {
      // Revert on failure
      setEvents((prev) =>
        prev.map((e) =>
          e.id === event.id
            ? {
                ...e,
                isInterested: event.isInterested,
                interestedCount: event.interestedCount,
              }
            : e,
        ),
      );
    }
  };

  const handleCreateEvent = async () => {
    const titleVal = formTitle().trim();
    if (!titleVal || !formStartTime()) return;

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const payload: Record<string, unknown> = {
        name: titleVal,
        description: formDescription().trim() || undefined,
        scheduledStartTime: new Date(formStartTime()).toISOString(),
        scheduledEndTime: formEndTime() ? new Date(formEndTime()).toISOString() : undefined,
      };

      if (formLocationType() === 'VoiceChannel') {
        payload.channelId = formLocationChannelId().trim() || undefined;
      } else {
        payload.location = formLocationExternalUrl().trim() || undefined;
      }

      const newEvent = await api.post<ScheduledEvent>(
        `/api/v1/servers/${props.serverId}/events`,
        payload,
      );

      setEvents((prev) => {
        const updated = [...prev, newEvent];
        return updated.sort(
          (a, b) => new Date(a.scheduledStartTime).getTime() - new Date(b.scheduledStartTime).getTime(),
        );
      });

      // Reset form
      setFormTitle('');
      setFormDescription('');
      setFormStartTime('');
      setFormEndTime('');
      setFormLocationType('External');
      setFormLocationChannelId('');
      setFormLocationExternalUrl('');
      setShowCreateForm(false);
    } catch {
      setSubmitError('Failed to create event. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancelCreate = () => {
    setShowCreateForm(false);
    setFormTitle('');
    setFormDescription('');
    setFormStartTime('');
    setFormEndTime('');
    setFormLocationType('External');
    setFormLocationChannelId('');
    setFormLocationExternalUrl('');
    setSubmitError(null);
  };

  const isFormValid = () => formTitle().trim().length > 0 && formStartTime().length > 0;

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Scheduled Events</h2>
        <button
          class="bg-xcord-brand text-white px-3 py-1.5 rounded hover:bg-xcord-brand-hover transition-colors text-sm"
          onClick={() => setShowCreateForm(true)}
        >
          + Create Event
        </button>
      </div>

      {/* Create Event Form */}
      <Show when={showCreateForm()}>
        <div class="px-4 py-4 bg-xcord-bg-primary border-b border-xcord-bg-tertiary space-y-3 flex-shrink-0 overflow-y-auto max-h-[60vh]">
          <h3 class="text-xcord-text-primary font-semibold text-sm">New Scheduled Event</h3>

          {/* Title */}
          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Event Name *
            </label>
            <input
              type="text"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
              placeholder="Event name..."
              value={formTitle()}
              onInput={(e) => setFormTitle(e.currentTarget.value)}
            />
          </div>

          {/* Description */}
          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Description
            </label>
            <textarea
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand resize-none"
              placeholder="Describe the event..."
              rows={3}
              value={formDescription()}
              onInput={(e) => setFormDescription(e.currentTarget.value)}
            />
          </div>

          {/* Start + End times */}
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                Start Time *
              </label>
              <input
                type="datetime-local"
                class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                value={formStartTime()}
                onInput={(e) => setFormStartTime(e.currentTarget.value)}
              />
            </div>
            <div>
              <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                End Time
              </label>
              <input
                type="datetime-local"
                class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                value={formEndTime()}
                onInput={(e) => setFormEndTime(e.currentTarget.value)}
              />
            </div>
          </div>

          {/* Location type */}
          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Location Type
            </label>
            <select
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
              value={formLocationType()}
              onChange={(e) =>
                setFormLocationType(e.currentTarget.value as 'VoiceChannel' | 'External')
              }
            >
              <option value="External">External / URL</option>
              <option value="VoiceChannel">Voice Channel</option>
            </select>
          </div>

          {/* Conditional location field */}
          <Show when={formLocationType() === 'VoiceChannel'}>
            <div>
              <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                Voice Channel ID
              </label>
              <input
                type="text"
                class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                placeholder="Channel ID..."
                value={formLocationChannelId()}
                onInput={(e) => setFormLocationChannelId(e.currentTarget.value)}
              />
            </div>
          </Show>

          <Show when={formLocationType() === 'External'}>
            <div>
              <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                External URL
              </label>
              <input
                type="url"
                class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                placeholder="https://..."
                value={formLocationExternalUrl()}
                onInput={(e) => setFormLocationExternalUrl(e.currentTarget.value)}
              />
            </div>
          </Show>

          <Show when={submitError()}>
            <p class="text-red-400 text-xs">{submitError()}</p>
          </Show>

          <div class="flex gap-2 pt-1">
            <button
              class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleCreateEvent}
              disabled={isSubmitting() || !isFormValid()}
            >
              {isSubmitting() ? 'Creating...' : 'Create Event'}
            </button>
            <button
              class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Events list */}
      <div class="flex-1 overflow-y-auto">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!isLoading() && events().length === 0}>
          <div class="flex flex-col items-center justify-center h-48 space-y-3">
            <div class="text-4xl text-xcord-text-muted">📅</div>
            <p class="text-xcord-text-muted text-sm">No upcoming events</p>
            <button
              class="text-xcord-brand hover:underline text-sm"
              onClick={() => setShowCreateForm(true)}
            >
              Schedule an event
            </button>
          </div>
        </Show>

        <Show when={!isLoading() && events().length > 0}>
          <div class="divide-y divide-xcord-bg-tertiary">
            <For each={events()}>
              {(event) => (
                <div class="px-4 py-4">
                  <div class="flex items-start justify-between gap-3">
                    <div class="flex-1 min-w-0">
                      {/* Event name */}
                      <h3 class="text-xcord-text-primary font-semibold text-sm">{event.name}</h3>

                      {/* Date and time */}
                      <div class="flex items-center gap-2 mt-1">
                        <span class="text-xcord-text-muted text-xs">📅</span>
                        <span class="text-xcord-text-muted text-xs">
                          {formatEventDate(event.scheduledStartTime)} at {formatEventTime(event.scheduledStartTime)}
                        </span>
                        <Show when={event.scheduledEndTime}>
                          <span class="text-xcord-text-muted text-xs">
                            - {formatEventTime(event.scheduledEndTime!)}
                          </span>
                        </Show>
                      </div>

                      {/* Location */}
                      <div class="flex items-center gap-2 mt-0.5">
                        <Show when={event.channelId}>
                          <span class="text-xcord-text-muted text-xs">🔊</span>
                          <span class="text-xcord-text-muted text-xs">Voice Channel</span>
                        </Show>
                        <Show when={!event.channelId && event.location}>
                          <span class="text-xcord-text-muted text-xs">🔗</span>
                          <a
                            href={event.location}
                            target="_blank"
                            rel="noreferrer"
                            class="text-xcord-brand text-xs hover:underline truncate"
                            onClick={(e) => e.stopPropagation()}
                          >
                            {event.location}
                          </a>
                        </Show>
                      </div>

                      {/* Description */}
                      <Show when={event.description}>
                        <p class="text-xcord-text-muted text-xs mt-1.5 line-clamp-2">
                          {event.description}
                        </p>
                      </Show>

                      {/* Interested count */}
                      <p class="text-xcord-text-muted text-xs mt-2">
                        {event.interestedCount} interested
                      </p>
                    </div>

                    {/* RSVP button */}
                    <button
                      class={`flex-shrink-0 px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                        event.isInterested
                          ? 'bg-xcord-brand text-white hover:bg-xcord-brand-hover'
                          : 'bg-xcord-bg-tertiary text-xcord-text-muted hover:bg-xcord-bg-primary hover:text-xcord-text-primary'
                      }`}
                      onClick={() => toggleInterested(event)}
                      aria-label={event.isInterested ? 'Remove interest' : 'Mark as interested'}
                    >
                      {event.isInterested ? 'Interested ✓' : 'Interested'}
                    </button>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}

export function sortEventsByStartTime(events: ScheduledEvent[]): ScheduledEvent[] {
  return [...events].sort(
    (a, b) => new Date(a.scheduledStartTime).getTime() - new Date(b.scheduledStartTime).getTime(),
  );
}

export function toggleInterestedState(event: ScheduledEvent): ScheduledEvent {
  return {
    ...event,
    isInterested: !event.isInterested,
    interestedCount: event.isInterested
      ? event.interestedCount - 1
      : event.interestedCount + 1,
  };
}

export function isEventFormValid(title: string, startTime: string): boolean {
  return title.trim().length > 0 && startTime.length > 0;
}

export { formatEventDate, formatEventTime, formatDateTimeLocal };
