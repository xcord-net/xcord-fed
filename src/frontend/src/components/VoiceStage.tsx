import { For, Show } from 'solid-js';
import { useVoice } from '../stores/voice.store';
import { useMembers } from '../stores/member.store';
import { useAuth } from '../stores/auth.store';
import { useChannels } from '../stores/channel.store';
import styles from './VoiceStage.module.css';

/** Resting heights for the level meter, as a percentage of its track. The
 *  shape is fixed rather than driven by audio: LiveKit reports who is
 *  speaking, not how loudly, so animating to a real level would be invented
 *  data. The bars say "this person holds the floor" and nothing more. */
const METER_BARS = [34, 62, 45, 88, 56, 100, 48, 74, 40, 66, 52, 82, 38];

/** One rendered tile. Joins voice state to the member record for the name
 *  and avatar, since the voice store only carries user IDs. */
interface Tile {
  userId: string;
  name: string;
  avatarUrl?: string;
  isMuted: boolean;
  isSpeaking: boolean;
  isLocal: boolean;
}

/**
 * The participant grid for a voice channel. Shows everyone in the room, who
 * is muted, and who is speaking. Text chat renders below it, so the room and
 * its conversation stay on one screen.
 */
export default function VoiceStage() {
  const voice = useVoice();
  const members = useMembers();
  const auth = useAuth();
  const channels = useChannels();

  const nameFor = (userId: string): { name: string; avatarUrl?: string } => {
    const member = members.members.find((m) => m.userId === userId);
    if (member) {
      return {
        name: member.nickname ?? member.displayName ?? member.username,
        avatarUrl: member.avatarUrl,
      };
    }
    return { name: userId };
  };

  const tiles = (): Tile[] => {
    const result: Tile[] = [];

    const localUser = auth.user;
    if (localUser) {
      const resolved = nameFor(localUser.id);
      result.push({
        userId: localUser.id,
        name: resolved.name === localUser.id ? localUser.username : resolved.name,
        avatarUrl: resolved.avatarUrl ?? localUser.avatarUrl,
        isMuted: voice.isMuted,
        isSpeaking: voice.isSpeaking,
        isLocal: true,
      });
    }

    for (const participant of voice.participants.values()) {
      if (localUser && participant.userId === localUser.id) continue;
      const resolved = nameFor(participant.userId);
      result.push({
        userId: participant.userId,
        name: resolved.name,
        avatarUrl: resolved.avatarUrl,
        isMuted: participant.isMuted,
        isSpeaking: participant.isSpeaking ?? false,
        isLocal: false,
      });
    }

    return result;
  };

  const isConnectedHere = () =>
    voice.currentChannelId !== null &&
    voice.currentChannelId === channels.selectedChannelId;

  return (
    <div class={styles.stage} data-testid="voice-stage">
      <Show
        when={isConnectedHere()}
        fallback={
          <p class={styles.empty}>Join this channel to see who is here.</p>
        }
      >
        <div class={styles.grid}>
          <For each={tiles()}>
            {(tile) => (
              <div
                data-testid={`voice-tile-${tile.userId}`}
                data-participant={tile.name}
                data-speaking={String(tile.isSpeaking)}
                data-muted={String(tile.isMuted)}
                classList={{
                  [styles.tile]: true,
                  [styles.tileSpeaking]: tile.isSpeaking,
                }}
              >
                <div class={styles.avatar}>
                  <Show
                    when={tile.avatarUrl}
                    fallback={tile.name.charAt(0).toUpperCase()}
                  >
                    <img src={tile.avatarUrl} alt="" class={styles.avatarImg} />
                  </Show>
                </div>

                <div class={styles.nameRow}>
                  <span class={styles.name}>
                    {tile.name}
                    <Show when={tile.isLocal}>
                      <span class={styles.you}> (you)</span>
                    </Show>
                  </span>
                  <Show when={tile.isMuted}>
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      class={styles.mutedIcon}
                      role="img"
                      aria-label="Muted"
                    >
                      <line x1="2" y1="2" x2="22" y2="22" />
                      <path d="M18.89 13.23A7.12 7.12 0 0 0 19 12v-2" />
                      <path d="M5 10v2a7 7 0 0 0 12 5" />
                      <path d="M15 9.34V5a3 3 0 0 0-5.68-1.33" />
                      <path d="M9 9v3a3 3 0 0 0 5.12 2.12" />
                      <line x1="12" y1="19" x2="12" y2="22" />
                    </svg>
                  </Show>
                </div>

                {/* Level meter. The bars mark who holds the floor; they are
                    decorative, so screen readers skip them. */}
                <div class={styles.meter} aria-hidden="true">
                  <For each={METER_BARS}>
                    {(height, index) => (
                      <span
                        class={styles.bar}
                        style={{
                          height: `${height}%`,
                          "animation-delay": `-${index() * 130}ms`,
                        }}
                      />
                    )}
                  </For>
                </div>
              </div>
            )}
          </For>
        </div>
      </Show>
    </div>
  );
}
