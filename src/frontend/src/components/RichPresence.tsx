import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export type ActivityType = 'Playing' | 'Listening' | 'Watching' | 'Streaming' | 'Custom';

export interface Activity {
  type: ActivityType;
  name: string;
  details?: string;
  state?: string;
  emoji?: string;
  url?: string;
  startedAt?: string;
}

export interface RichPresenceData {
  status: 'online' | 'idle' | 'dnd' | 'offline';
  activity?: Activity;
}

interface RichPresenceProps {
  userId: string;
  presence?: RichPresenceData;
  compact?: boolean;
}

// ---- Helpers ----

const activityVerb: Record<ActivityType, string> = {
  Playing: 'Playing',
  Listening: 'Listening to',
  Watching: 'Watching',
  Streaming: 'Streaming',
  Custom: '',
};

export function formatElapsedTime(startedAt: string): string {
  const startMs = new Date(startedAt).getTime();
  const nowMs = Date.now();
  const diffMs = nowMs - startMs;

  if (diffMs < 0) return '0 min';

  const totalSeconds = Math.floor(diffMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return `${minutes} min`;
}

export function getActivityLabel(activity: Activity): string {
  const verb = activityVerb[activity.type];
  if (activity.type === 'Custom') {
    const prefix = activity.emoji ? `${activity.emoji} ` : '';
    return `${prefix}${activity.name}`;
  }
  return verb ? `${verb} ${activity.name}` : activity.name;
}

export function buildPresencePayload(
  status: RichPresenceData['status'],
  activity?: Activity,
): Record<string, unknown> {
  return { status, activity: activity ?? null };
}

// ---- Component ----

export default function RichPresence(props: RichPresenceProps) {
  const [isUpdating, setIsUpdating] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [showEditor, setShowEditor] = createSignal(false);

  // Editor state
  const [editorStatus, setEditorStatus] = createSignal<RichPresenceData['status']>('online');
  const [editorType, setEditorType] = createSignal<ActivityType>('Playing');
  const [editorName, setEditorName] = createSignal('');
  const [editorDetails, setEditorDetails] = createSignal('');
  const [editorEmoji, setEditorEmoji] = createSignal('');

  const activity = () => props.presence?.activity;
  const status = () => props.presence?.status ?? 'offline';

  const savePresence = async () => {
    setIsUpdating(true);
    setError(null);
    try {
      const act: Activity | undefined =
        editorName().trim()
          ? {
              type: editorType(),
              name: editorName().trim(),
              details: editorDetails().trim() || undefined,
              emoji: editorEmoji().trim() || undefined,
              startedAt: new Date().toISOString(),
            }
          : undefined;

      const payload = buildPresencePayload(editorStatus(), act);
      await api.put('/api/v1/users/@me/presence', payload);
      setShowEditor(false);
    } catch {
      setError('Failed to update presence. Please try again.');
    } finally {
      setIsUpdating(false);
    }
  };

  const activityTypeOptions: ActivityType[] = ['Playing', 'Listening', 'Watching', 'Streaming', 'Custom'];

  return (
    <div class="text-sm">
      {/* Compact display (for member list / profile popover) */}
      <Show when={props.compact}>
        <Show when={activity()}>
          <div class="flex items-center gap-1.5 text-xcord-text-muted text-xs">
            <Show when={activity()!.emoji}>
              <span>{activity()!.emoji}</span>
            </Show>
            <span class="truncate">{getActivityLabel(activity()!)}</span>
          </div>
        </Show>
      </Show>

      {/* Full display */}
      <Show when={!props.compact}>
        <div class="space-y-2">
          {/* Current activity card */}
          <Show when={activity()}>
            <div class="bg-xcord-bg-primary rounded p-3 space-y-1">
              <p class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide">
                {activityVerb[activity()!.type] || 'Status'}
              </p>
              <div class="flex items-center gap-2">
                <Show when={activity()!.emoji}>
                  <span class="text-lg">{activity()!.emoji}</span>
                </Show>
                <div class="flex-1 min-w-0">
                  <p class="text-xcord-text-primary font-medium truncate">{activity()!.name}</p>
                  <Show when={activity()!.details}>
                    <p class="text-xcord-text-muted text-xs truncate">{activity()!.details}</p>
                  </Show>
                  <Show when={activity()!.state}>
                    <p class="text-xcord-text-muted text-xs truncate">{activity()!.state}</p>
                  </Show>
                  <Show when={activity()!.startedAt}>
                    <p class="text-xcord-text-muted text-xs mt-0.5">
                      {formatElapsedTime(activity()!.startedAt!)} elapsed
                    </p>
                  </Show>
                </div>
              </div>
            </div>
          </Show>

          <Show when={!activity()}>
            <p class="text-xcord-text-muted text-xs italic">No activity</p>
          </Show>

          {/* Edit button */}
          <button
            class="text-xcord-brand text-xs hover:underline"
            onClick={() => setShowEditor((v) => !v)}
            aria-label="Edit presence"
          >
            {showEditor() ? 'Cancel' : 'Set Activity'}
          </button>

          {/* Editor */}
          <Show when={showEditor()}>
            <div class="bg-xcord-bg-primary rounded p-3 space-y-3">
              {/* Status select */}
              <div>
                <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                  Status
                </label>
                <select
                  class="w-full bg-xcord-bg-secondary text-xcord-text-primary rounded px-2 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                  value={editorStatus()}
                  onChange={(e) =>
                    setEditorStatus(e.currentTarget.value as RichPresenceData['status'])
                  }
                >
                  <option value="online">Online</option>
                  <option value="idle">Idle</option>
                  <option value="dnd">Do Not Disturb</option>
                  <option value="offline">Invisible</option>
                </select>
              </div>

              {/* Activity type */}
              <div>
                <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                  Activity Type
                </label>
                <div class="flex flex-wrap gap-1">
                  <For each={activityTypeOptions}>
                    {(type) => (
                      <button
                        class={`px-2 py-1 rounded text-xs transition-colors ${
                          editorType() === type
                            ? 'bg-xcord-brand text-white'
                            : 'bg-xcord-bg-secondary text-xcord-text-muted hover:text-xcord-text-primary'
                        }`}
                        onClick={() => setEditorType(type)}
                      >
                        {type}
                      </button>
                    )}
                  </For>
                </div>
              </div>

              {/* Activity name */}
              <div>
                <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                  {editorType() === 'Custom' ? 'Custom Status' : 'Name'}
                </label>
                <input
                  type="text"
                  class="w-full bg-xcord-bg-secondary text-xcord-text-primary placeholder-xcord-text-muted rounded px-2 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                  placeholder={editorType() === 'Custom' ? 'What\'s on your mind?' : 'Activity name...'}
                  value={editorName()}
                  onInput={(e) => setEditorName(e.currentTarget.value)}
                />
              </div>

              {/* Emoji (custom only) */}
              <Show when={editorType() === 'Custom'}>
                <div>
                  <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                    Emoji
                  </label>
                  <input
                    type="text"
                    class="w-full bg-xcord-bg-secondary text-xcord-text-primary placeholder-xcord-text-muted rounded px-2 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                    placeholder="e.g. :wave:"
                    value={editorEmoji()}
                    onInput={(e) => setEditorEmoji(e.currentTarget.value)}
                  />
                </div>
              </Show>

              {/* Details */}
              <Show when={editorType() !== 'Custom'}>
                <div>
                  <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                    Details (optional)
                  </label>
                  <input
                    type="text"
                    class="w-full bg-xcord-bg-secondary text-xcord-text-primary placeholder-xcord-text-muted rounded px-2 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                    placeholder="e.g. Level 45 Warrior"
                    value={editorDetails()}
                    onInput={(e) => setEditorDetails(e.currentTarget.value)}
                  />
                </div>
              </Show>

              <Show when={error()}>
                <p class="text-red-400 text-xs">{error()}</p>
              </Show>

              <button
                class="w-full bg-xcord-brand text-white py-1.5 rounded text-sm font-medium hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                onClick={savePresence}
                disabled={isUpdating()}
                aria-label="Save presence"
              >
                {isUpdating() ? 'Saving...' : 'Save'}
              </button>
            </div>
          </Show>
        </div>
      </Show>
    </div>
  );
}

export { activityVerb };
