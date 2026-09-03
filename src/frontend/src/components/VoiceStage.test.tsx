import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import { vi } from 'vitest';
import type { VoiceParticipant } from '../types/voice';
import type { Member } from '../types/member';

const voiceState = {
  currentChannelId: null as string | null,
  participants: new Map<string, VoiceParticipant>(),
  isMuted: false,
  isSpeaking: false,
};

// The stage fetches the roster it needs to name participants, so the mock has
// to offer that too. Recorded rather than ignored: an empty roster must lead to
// exactly one fetch, and a populated one to none.
const fetchMembers = vi.fn(() => Promise.resolve());
const memberState = {
  members: [] as Member[],
  fetchMembers,
};

const authState = {
  user: { id: 'u-local', username: 'craig', email: 'craig@example.com' } as
    | { id: string; username: string; email: string; avatarUrl?: string }
    | null,
};

vi.mock('../stores/voice.store', () => ({ useVoice: () => voiceState }));
vi.mock('../stores/member.store', () => ({ useMembers: () => memberState }));
vi.mock('../stores/auth.store', () => ({ useAuth: () => authState }));

import VoiceStage from './VoiceStage';
import { useChannels } from '../stores/channel.store';
import { Capability, type Channel } from '../types/channel';

function makeChannel(over: Partial<Channel> = {}): Channel {
  return {
    id: 'voice-1', serverId: 's-1', name: 'General Voice', type: 'Voice',
    capabilities: Capability.Voice, position: 0, isNsfw: false, slowModeSeconds: 0,
    createdAt: '2025-01-01T00:00:00Z', conversationId: 'conv-1', ...over,
  };
}

function makeMember(over: Partial<Member> = {}): Member {
  return {
    userId: 'u-remote', serverId: 's-1', username: 'dana', displayName: 'Dana',
    groups: [], joinedAt: '2025-01-01T00:00:00Z', ...over,
  };
}

function selectVoiceChannel() {
  const channels = useChannels();
  channels.addChannel(makeChannel());
  channels.selectChannel('voice-1');
}

describe('VoiceStage', () => {
  beforeEach(() => {
    useChannels().reset();
    voiceState.currentChannelId = null;
    voiceState.participants = new Map();
    voiceState.isMuted = false;
    voiceState.isSpeaking = false;
    memberState.members = [];
    fetchMembers.mockClear();
    authState.user = { id: 'u-local', username: 'craig', email: 'craig@example.com' };
  });

  it('fetches the roster it needs to name participants', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';

    render(() => <VoiceStage />);

    // Nothing else on a voice channel loads the roster, so without this the
    // tiles fall back to raw user ids.
    expect(fetchMembers).toHaveBeenCalledWith('s-1');
  });

  it('does not refetch the roster when it is already loaded', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';
    memberState.members = [
      { userId: 'u-local', username: 'craig', displayName: 'Craig' } as Member,
    ];

    render(() => <VoiceStage />);

    expect(fetchMembers).not.toHaveBeenCalled();
  });

  it('prompts to join when the user is not connected to this channel', () => {
    selectVoiceChannel();

    const { getByText, queryByTestId, getByTestId } = render(() => <VoiceStage />);

    expect(getByTestId('voice-stage-empty')).toBeTruthy();
    expect(queryByTestId('voice-tile-u-local')).toBeNull();
  });

  it('renders a tile for the local user once connected', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';

    const { getByTestId } = render(() => <VoiceStage />);

    const tile = getByTestId('voice-tile-u-local');
    expect(tile.textContent).toContain('craig');
    expect(tile.textContent).toContain('(you)');
  });

  it('renders remote participants using their member display name', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';
    memberState.members = [makeMember()];
    voiceState.participants = new Map([
      ['u-remote', { userId: 'u-remote', isMuted: false, isDeafened: false }],
    ]);

    const { getByTestId } = render(() => <VoiceStage />);

    expect(getByTestId('voice-tile-u-remote').textContent).toContain('Dana');
  });

  it('marks a speaking participant', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';
    memberState.members = [makeMember()];
    voiceState.participants = new Map([
      ['u-remote', { userId: 'u-remote', isMuted: false, isDeafened: false, isSpeaking: true }],
    ]);

    const { getByTestId } = render(() => <VoiceStage />);

    expect(getByTestId('voice-tile-u-remote').dataset.speaking).toBe('true');
    expect(getByTestId('voice-tile-u-local').dataset.speaking).toBe('false');
  });

  it('shows a muted indicator for a muted participant', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';
    memberState.members = [makeMember()];
    voiceState.participants = new Map([
      ['u-remote', { userId: 'u-remote', isMuted: true, isDeafened: false }],
    ]);

    const { getByTestId, getByLabelText } = render(() => <VoiceStage />);

    expect(getByTestId('voice-tile-u-remote').contains(getByLabelText('Muted'))).toBe(true);
  });

  it('does not duplicate the local user when they also appear in the participants map', () => {
    selectVoiceChannel();
    voiceState.currentChannelId = 'voice-1';
    voiceState.participants = new Map([
      ['u-local', { userId: 'u-local', isMuted: false, isDeafened: false }],
    ]);

    const { getAllByTestId } = render(() => <VoiceStage />);

    expect(getAllByTestId('voice-tile-u-local')).toHaveLength(1);
  });
});
