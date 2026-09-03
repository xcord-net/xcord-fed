import { For, Show, createEffect } from 'solid-js';
import { useVoice } from '../stores/voice.store';
import { useMembers } from '../stores/member.store';
import { useAuth } from '../stores/auth.store';
import { useChannels } from '../stores/channel.store';
import { MutedIcon } from './ui/icons';
import styles from './VoiceStage.module.css';
import EmptyState from './ui/EmptyState';

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

  /**
   * The roster is what turns a voice participant into a person.
   *
   * Nothing else on a voice channel loads it - the member list is opened on
   * demand from the compose bar, and the Deck fetches it only for a community
   * view - so anyone who went straight into a room saw raw user IDs on the
   * tiles instead of names. Fetching it here makes the stage responsible for
   * the data it renders.
   */
  createEffect(() => {
    const channelId = voice.currentChannelId;
    if (!channelId) return;
    const serverId = channels.channels.find((c) => c.id === channelId)?.serverId;
    if (!serverId) return;
    if (members.members.length > 0) return;
    void members.fetchMembers(serverId).catch(() => undefined);
  });

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
          <EmptyState
            title="Join to see who is here"
            body="Connect to this room and the people in it appear on the stage."
            dense
            data-testid="voice-stage-empty"
          />
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
                    <MutedIcon class={styles.mutedIcon} label="Muted" />
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
