import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import styles from './ScheduledEvents.module.css';

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
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Scheduled Events</h2>
        <button
          data-testid="scheduled-events-create-button"
          class={styles.createBtn}
          onClick={() => setShowCreateForm(true)}
        >
          + Create Event
        </button>
      </div>

      {/* Create Event Form */}
      <Show when={showCreateForm()}>
        <div data-testid="scheduled-events-form" class={styles.formPanel}>
          <h3 class={styles.formTitle}>New Scheduled Event</h3>

          {/* Title */}
          <div>
            <label class={styles.fieldLabel}>
              Event Name *
            </label>
            <input
              data-testid="scheduled-events-name-input"
              type="text"
              class={styles.textInput}
              placeholder="Event name..."
              value={formTitle()}
              onInput={(e) => setFormTitle(e.currentTarget.value)}
            />
          </div>

          {/* Description */}
          <div>
            <label class={styles.fieldLabel}>
              Description
            </label>
            <textarea
              class={styles.textarea}
              placeholder="Describe the event..."
              rows={3}
              value={formDescription()}
              onInput={(e) => setFormDescription(e.currentTarget.value)}
            />
          </div>

          {/* Start + End times */}
          <div class={styles.timeGrid}>
            <div>
              <label class={styles.fieldLabel}>
                Start Time *
              </label>
              <input
                data-testid="scheduled-events-start-input"
                type="datetime-local"
                class={styles.textInput}
                value={formStartTime()}
                onInput={(e) => setFormStartTime(e.currentTarget.value)}
              />
            </div>
            <div>
              <label class={styles.fieldLabel}>
                End Time
              </label>
              <input
                type="datetime-local"
                class={styles.textInput}
                value={formEndTime()}
                onInput={(e) => setFormEndTime(e.currentTarget.value)}
              />
            </div>
          </div>

          {/* Location type */}
          <div>
            <label class={styles.fieldLabel}>
              Location Type
            </label>
            <select
              class={styles.selectInput}
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
              <label class={styles.fieldLabel}>
                Voice Channel ID
              </label>
              <input
                type="text"
                class={styles.textInput}
                placeholder="Channel ID..."
                value={formLocationChannelId()}
                onInput={(e) => setFormLocationChannelId(e.currentTarget.value)}
              />
            </div>
          </Show>

          <Show when={formLocationType() === 'External'}>
            <div>
              <label class={styles.fieldLabel}>
                External URL
              </label>
              <input
                type="url"
                class={styles.textInput}
                placeholder="https://..."
                value={formLocationExternalUrl()}
                onInput={(e) => setFormLocationExternalUrl(e.currentTarget.value)}
              />
            </div>
          </Show>

          <Show when={submitError()}>
            <p class={styles.errorText}>{submitError()}</p>
          </Show>

          <div class={styles.formButtons}>
            <button
              data-testid="scheduled-events-form-submit"
              class={styles.primaryBtn}
              onClick={handleCreateEvent}
              disabled={isSubmitting() || !isFormValid()}
            >
              {isSubmitting() ? 'Creating...' : 'Create Event'}
            </button>
            <button
              class={styles.secondaryBtn}
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Events list */}
      <div class={styles.listArea}>
        <Show when={isLoading()}>
          <div class={styles.loadingCenter}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoading() && events().length === 0}>
          <div class={styles.emptyState}>
            <div class={styles.emptyIcon}>📅</div>
            <p class={styles.emptyText}>No upcoming events</p>
            <button
              class={styles.scheduleLink}
              onClick={() => setShowCreateForm(true)}
            >
              Schedule an event
            </button>
          </div>
        </Show>

        <Show when={!isLoading() && events().length > 0}>
          <div data-testid="scheduled-events-list" class={styles.eventList}>
            <For each={events()}>
              {(event) => (
                <div data-testid="scheduled-event-item" class={styles.eventItem}>
                  <div class={styles.eventItemInner}>
                    <div class={styles.eventInfo}>
                      {/* Event name */}
                      <h3 class={styles.eventName}>{event.name}</h3>

                      {/* Date and time */}
                      <div class={styles.eventTimeRow}>
                        <span class={styles.eventTimeIcon}>📅</span>
                        <span class={styles.eventTimeText}>
                          {formatEventDate(event.scheduledStartTime)} at {formatEventTime(event.scheduledStartTime)}
                        </span>
                        <Show when={event.scheduledEndTime}>
                          <span class={styles.eventTimeText}>
                            - {formatEventTime(event.scheduledEndTime!)}
                          </span>
                        </Show>
                      </div>

                      {/* Location */}
                      <div class={styles.eventLocationRow}>
                        <Show when={event.channelId}>
                          <span class={styles.eventLocationIcon}>🔊</span>
                          <span class={styles.eventLocationText}>Voice Channel</span>
                        </Show>
                        <Show when={!event.channelId && event.location}>
                          <span class={styles.eventLocationIcon}>🔗</span>
                          <a
                            href={event.location}
                            target="_blank"
                            rel="noreferrer"
                            class={styles.eventLocationLink}
                            onClick={(e) => e.stopPropagation()}
                          >
                            {event.location}
                          </a>
                        </Show>
                      </div>

                      {/* Description */}
                      <Show when={event.description}>
                        <p class={styles.eventDescription}>
                          {event.description}
                        </p>
                      </Show>

                      {/* Interested count */}
                      <p class={styles.eventInterestedCount}>
                        {event.interestedCount} interested
                      </p>
                    </div>

                    {/* RSVP button */}
                    <button
                      class={event.isInterested ? styles.rsvpBtnActive : styles.rsvpBtnInactive}
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
