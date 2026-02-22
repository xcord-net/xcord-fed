import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface StageParticipant {
  userId: string;
  displayName: string;
  avatarUrl?: string;
  role: 'host' | 'speaker' | 'audience';
  isMuted: boolean;
}

export interface StageChannelData {
  channelId: string;
  channelName: string;
  serverId: string;
  topic?: string;
  speakers: StageParticipant[];
  audience: StageParticipant[];
  myUserId: string;
  myRole: 'host' | 'speaker' | 'audience';
  hasPendingSpeakRequest: boolean;
}

export interface StageChannelProps {
  serverId: string;
  channelId: string;
  currentUserId: string;
}

// ---- Helpers ----

export function getParticipantRole(participant: StageParticipant): string {
  if (participant.role === 'host') return 'Host';
  if (participant.role === 'speaker') return 'Speaker';
  return 'Audience';
}

export function canHostInvite(myRole: StageChannelData['myRole']): boolean {
  return myRole === 'host';
}

export function buildSpeakRequestUrl(serverId: string, channelId: string): string {
  return `/api/v1/servers/${serverId}/channels/${channelId}/stage/request-to-speak`;
}

export function sortSpeakersFirst(participants: StageParticipant[]): StageParticipant[] {
  return [...participants].sort((a, b) => {
    const order: Record<StageParticipant['role'], number> = { host: 0, speaker: 1, audience: 2 };
    return order[a.role] - order[b.role];
  });
}

export function moveSpeakerToAudience(
  stage: StageChannelData,
  userId: string,
): StageChannelData {
  const speaker = stage.speakers.find((p) => p.userId === userId);
  if (!speaker) return stage;
  return {
    ...stage,
    speakers: stage.speakers.filter((p) => p.userId !== userId),
    audience: [...stage.audience, { ...speaker, role: 'audience' }],
  };
}

export function moveAudienceToSpeaker(
  stage: StageChannelData,
  userId: string,
): StageChannelData {
  const member = stage.audience.find((p) => p.userId === userId);
  if (!member) return stage;
  return {
    ...stage,
    audience: stage.audience.filter((p) => p.userId !== userId),
    speakers: [...stage.speakers, { ...member, role: 'speaker' }],
  };
}

// ---- Component ----

export default function StageChannel(props: StageChannelProps) {
  const [stage, setStage] = createSignal<StageChannelData | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [isRequestingToSpeak, setIsRequestingToSpeak] = createSignal(false);

  const loadStage = async (serverId: string, channelId: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.get<StageChannelData>(
        `/api/v1/servers/${serverId}/channels/${channelId}/stage`,
      );
      setStage(data);
    } catch {
      setError('Failed to load stage channel.');
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    const channelId = props.channelId;
    if (serverId && channelId) {
      loadStage(serverId, channelId);
    }
  });

  const requestToSpeak = async () => {
    const current = stage();
    if (!current) return;
    setIsRequestingToSpeak(true);
    setError(null);
    try {
      await api.post(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/stage/request-to-speak`,
        {},
      );
      setStage({ ...current, hasPendingSpeakRequest: true });
    } catch {
      setError('Failed to request to speak.');
    } finally {
      setIsRequestingToSpeak(false);
    }
  };

  const cancelSpeakRequest = async () => {
    const current = stage();
    if (!current) return;
    try {
      await api.post(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/stage/cancel-speak-request`,
        {},
      );
      setStage({ ...current, hasPendingSpeakRequest: false });
    } catch {
      setError('Failed to cancel speak request.');
    }
  };

  const inviteToSpeak = async (userId: string) => {
    const current = stage();
    if (!current) return;
    try {
      await api.post(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/stage/invite-to-speak`,
        { userId },
      );
      // Optimistic update: move from audience to speakers
      setStage({
        ...current,
        audience: current.audience.filter((p) => p.userId !== userId),
        speakers: [
          ...current.speakers,
          { ...current.audience.find((p) => p.userId === userId)!, role: 'speaker' },
        ],
      });
    } catch {
      setError('Failed to invite user to speak.');
    }
  };

  const moveToAudience = async (userId: string) => {
    const current = stage();
    if (!current) return;
    try {
      await api.post(
        `/api/v1/servers/${props.serverId}/channels/${props.channelId}/stage/move-to-audience`,
        { userId },
      );
      // Optimistic update: move from speakers to audience
      setStage({
        ...current,
        speakers: current.speakers.filter((p) => p.userId !== userId),
        audience: [
          ...current.audience,
          { ...current.speakers.find((p) => p.userId === userId)!, role: 'audience' },
        ],
      });
    } catch {
      setError('Failed to move user to audience.');
    }
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary" aria-label="Stage channel">
      <Show when={isLoading()}>
        <div class="flex items-center justify-center h-32">
          <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      <Show when={error()}>
        <div class="px-4 py-2">
          <p class="text-red-400 text-sm">{error()}</p>
        </div>
      </Show>

      <Show when={!isLoading() && stage()}>
        {(stageData) => (
          <div class="flex flex-col h-full">
            {/* Header */}
            <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex-shrink-0">
              <h2 class="text-xcord-text-primary font-semibold">{stageData().channelName}</h2>
              <Show when={stageData().topic}>
                <p class="text-xcord-text-muted text-sm mt-0.5">{stageData().topic}</p>
              </Show>
            </div>

            <div class="flex-1 overflow-y-auto px-4 py-3 space-y-6">
              {/* Speakers section */}
              <div>
                <h3 class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide mb-2">
                  Speakers — {stageData().speakers.length}
                </h3>
                <Show when={stageData().speakers.length === 0}>
                  <p class="text-xcord-text-muted text-sm">No speakers yet</p>
                </Show>
                <div class="grid grid-cols-3 gap-3">
                  <For each={stageData().speakers}>
                    {(participant) => (
                      <div class="flex flex-col items-center gap-1 p-2 bg-xcord-bg-primary rounded-lg">
                        <div class="w-12 h-12 rounded-full bg-xcord-brand flex items-center justify-center text-white font-semibold text-lg">
                          {participant.displayName.charAt(0).toUpperCase()}
                        </div>
                        <span class="text-xcord-text-primary text-xs font-medium text-center truncate w-full">
                          {participant.displayName}
                        </span>
                        <span class="text-xcord-text-muted text-xs">
                          {getParticipantRole(participant)}
                        </span>
                        <Show when={participant.isMuted}>
                          <span class="text-red-400 text-xs" aria-label="Muted">Muted</span>
                        </Show>
                        {/* Host controls: move to audience */}
                        <Show when={
                          stageData().myRole === 'host' &&
                          participant.userId !== stageData().myUserId &&
                          participant.role !== 'host'
                        }>
                          <button
                            class="mt-1 text-xs text-xcord-text-muted hover:text-xcord-text-primary transition-colors"
                            onClick={() => moveToAudience(participant.userId)}
                            aria-label={`Move ${participant.displayName} to audience`}
                          >
                            Move to audience
                          </button>
                        </Show>
                      </div>
                    )}
                  </For>
                </div>
              </div>

              {/* Audience section */}
              <div>
                <h3 class="text-xcord-text-muted text-xs font-semibold uppercase tracking-wide mb-2">
                  Audience — {stageData().audience.length}
                </h3>
                <Show when={stageData().audience.length === 0}>
                  <p class="text-xcord-text-muted text-sm">No audience members</p>
                </Show>
                <div class="space-y-1">
                  <For each={stageData().audience}>
                    {(participant) => (
                      <div class="flex items-center gap-3 p-2 rounded-lg hover:bg-xcord-bg-primary transition-colors">
                        <div class="w-8 h-8 rounded-full bg-xcord-bg-tertiary flex items-center justify-center text-xcord-text-muted font-semibold text-sm flex-shrink-0">
                          {participant.displayName.charAt(0).toUpperCase()}
                        </div>
                        <span class="flex-1 text-xcord-text-secondary text-sm">
                          {participant.displayName}
                        </span>
                        {/* Host controls: invite to speak */}
                        <Show when={stageData().myRole === 'host'}>
                          <button
                            class="text-xs text-xcord-brand hover:underline transition-colors flex-shrink-0"
                            onClick={() => inviteToSpeak(participant.userId)}
                            aria-label={`Invite ${participant.displayName} to speak`}
                          >
                            Invite to speak
                          </button>
                        </Show>
                      </div>
                    )}
                  </For>
                </div>
              </div>
            </div>

            {/* Bottom bar: audience speak request controls */}
            <Show when={stageData().myRole === 'audience'}>
              <div class="px-4 py-3 border-t border-xcord-bg-tertiary flex-shrink-0">
                <Show
                  when={!stageData().hasPendingSpeakRequest}
                  fallback={
                    <div class="flex items-center justify-between">
                      <span class="text-xcord-text-muted text-sm">Request sent...</span>
                      <button
                        class="text-xs text-xcord-text-muted hover:text-xcord-text-primary transition-colors"
                        onClick={cancelSpeakRequest}
                        aria-label="Cancel speak request"
                      >
                        Cancel
                      </button>
                    </div>
                  }
                >
                  <button
                    class="w-full px-4 py-2 bg-xcord-brand text-white rounded-lg hover:bg-xcord-brand/80 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                    onClick={requestToSpeak}
                    disabled={isRequestingToSpeak()}
                    aria-label="Request to speak"
                  >
                    {isRequestingToSpeak() ? 'Requesting...' : 'Request to Speak'}
                  </button>
                </Show>
              </div>
            </Show>
          </div>
        )}
      </Show>
    </div>
  );
}

