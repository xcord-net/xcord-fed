import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { OnboardingConfig, OnboardingGroup, OnboardingChannel } from '../components/ServerOnboarding';
import {
  getOnboardingSteps,
  stepIndex,
  nextStep,
  previousStep,
  stepLabel,
  progressPercent,
  toggleGroupSelection,
  toggleChannelSelection,
} from '../components/ServerOnboarding';

// ---- Test data ----

const makeGroup = (overrides: Partial<OnboardingGroup> = {}): OnboardingGroup => ({
  id: 'group-1',
  name: 'Gaming',
  description: 'For gamers',
  emoji: '\u{1F3AE}',
  ...overrides,
});

const makeChannel = (overrides: Partial<OnboardingChannel> = {}): OnboardingChannel => ({
  channelId: 'ch-1',
  channelName: 'general',
  description: 'General chat',
  ...overrides,
});

const makeConfig = (overrides: Partial<OnboardingConfig> = {}): OnboardingConfig => ({
  serverId: 'srv-1',
  promptMessage: 'Welcome! Let us get you set up.',
  rules: '1. Be respectful.\n2. No spam.\n3. Have fun!',
  groups: [
    makeGroup({ id: 'group-1', name: 'Gaming', emoji: '\u{1F3AE}' }),
    makeGroup({ id: 'group-2', name: 'Music', emoji: '\u{1F3B5}' }),
    makeGroup({ id: 'group-3', name: 'Art', emoji: '\u{1F3A8}' }),
  ],
  channels: [
    makeChannel({ channelId: 'ch-1', channelName: 'general' }),
    makeChannel({ channelId: 'ch-2', channelName: 'gaming' }),
  ],
  totalSteps: 4,
  ...overrides,
});

// ---- Tests ----

describe('ServerOnboarding', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- Step navigation ----

  describe('step navigation helpers', () => {
    it('getOnboardingSteps returns all 4 steps', () => {
      // Act
      const steps = getOnboardingSteps();

      // Assert
      expect(steps).toHaveLength(4);
      expect(steps).toEqual(['rules', 'groups', 'channels', 'complete']);
    });

    it('stepIndex returns 0 for rules (first step)', () => {
      expect(stepIndex('rules')).toBe(0);
    });

    it('stepIndex returns 3 for complete (last step)', () => {
      expect(stepIndex('complete')).toBe(3);
    });

    it('nextStep from rules returns groups', () => {
      expect(nextStep('rules')).toBe('groups');
    });

    it('nextStep from groups returns channels', () => {
      expect(nextStep('groups')).toBe('channels');
    });

    it('nextStep from channels returns complete', () => {
      expect(nextStep('channels')).toBe('complete');
    });

    it('nextStep from complete returns null (no further step)', () => {
      expect(nextStep('complete')).toBeNull();
    });

    it('previousStep from groups returns rules', () => {
      expect(previousStep('groups')).toBe('rules');
    });

    it('previousStep from rules returns null (first step)', () => {
      expect(previousStep('rules')).toBeNull();
    });

    it('previousStep from complete returns channels', () => {
      expect(previousStep('complete')).toBe('channels');
    });
  });

  // ---- Step labels ----

  describe('stepLabel', () => {
    it('returns "Rules" for rules step', () => {
      expect(stepLabel('rules')).toBe('Rules');
    });

    it('returns "Interests" for groups step', () => {
      expect(stepLabel('groups')).toBe('Interests');
    });

    it('returns "Channels" for channels step', () => {
      expect(stepLabel('channels')).toBe('Channels');
    });

    it('returns "Done" for complete step', () => {
      expect(stepLabel('complete')).toBe('Done');
    });
  });

  // ---- Progress percentage ----

  describe('progressPercent', () => {
    it('returns 0% for the first step', () => {
      expect(progressPercent('rules')).toBe(0);
    });

    it('returns 100% for the last step', () => {
      expect(progressPercent('complete')).toBe(100);
    });

    it('returns a value between 0 and 100 for intermediate steps', () => {
      // 4 steps: rules=0, groups=1, channels=2, complete=3
      // groups: Math.round((1 / (4 - 1)) * 100) = Math.round(33.33) = 33
      const pct = progressPercent('groups');
      expect(pct).toBe(33);
    });

    it('channels step has higher progress than groups step', () => {
      expect(progressPercent('channels')).toBeGreaterThan(progressPercent('groups'));
    });
  });

  // ---- Group selection ----

  describe('group selection', () => {
    it('adds a group when toggled and not previously selected', () => {
      // Arrange
      const selectedIds: string[] = [];

      // Act
      const result = toggleGroupSelection(selectedIds, 'group-1');

      // Assert
      expect(result).toContain('group-1');
    });

    it('removes a group when toggled and already selected', () => {
      // Arrange
      const selectedIds = ['group-1', 'group-2'];

      // Act
      const result = toggleGroupSelection(selectedIds, 'group-1');

      // Assert
      expect(result).not.toContain('group-1');
      expect(result).toContain('group-2');
    });

    it('multiple groups can be selected simultaneously', () => {
      // Arrange
      let selectedIds: string[] = [];

      // Act
      selectedIds = toggleGroupSelection(selectedIds, 'group-1');
      selectedIds = toggleGroupSelection(selectedIds, 'group-2');
      selectedIds = toggleGroupSelection(selectedIds, 'group-3');

      // Assert
      expect(selectedIds).toHaveLength(3);
    });

  });

  // ---- Channel selection ----

  describe('channel selection', () => {
    it('adds a channel when toggled and not previously selected', () => {
      // Arrange
      const selectedIds: string[] = [];

      // Act
      const result = toggleChannelSelection(selectedIds, 'ch-1');

      // Assert
      expect(result).toContain('ch-1');
    });

    it('removes a channel when toggled and already selected', () => {
      // Arrange
      const selectedIds = ['ch-1', 'ch-2'];

      // Act
      const result = toggleChannelSelection(selectedIds, 'ch-1');

      // Assert
      expect(result).not.toContain('ch-1');
    });

    it('multiple channels can be selected', () => {
      // Arrange
      let selectedIds: string[] = [];

      // Act
      selectedIds = toggleChannelSelection(selectedIds, 'ch-1');
      selectedIds = toggleChannelSelection(selectedIds, 'ch-2');

      // Assert
      expect(selectedIds).toHaveLength(2);
    });
  });

  // ---- API: load onboarding ----

  describe('load onboarding config API', () => {
    it('GET hits the correct URL', async () => {
      // Arrange
      const serverId = 'srv-api-get';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => makeConfig({ serverId }),
      });

      // Act
      const result = await api.get<OnboardingConfig>(
        `/api/v1/servers/${serverId}/onboarding`,
      );

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/onboarding`,
        expect.objectContaining({ method: 'GET' }),
      );
      expect(result.serverId).toBe(serverId);
    });
  });

  // ---- API: complete onboarding ----

  describe('complete onboarding API', () => {
    it('POST to complete hits the correct URL with selections', async () => {
      // Arrange
      const serverId = 'srv-complete-test';
      const payload = {
        rulesAccepted: true,
        selectedGroupIds: ['group-1', 'group-2'],
        selectedChannelIds: ['ch-1'],
      };

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/onboarding/complete`, payload);

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/servers/${serverId}/onboarding/complete`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify(payload),
        }),
      );
    });

    it('complete payload includes rulesAccepted flag', async () => {
      // Arrange
      const serverId = 'srv-rules-test';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/onboarding/complete`, {
        rulesAccepted: true,
        selectedGroupIds: [],
        selectedChannelIds: [],
      });

      // Assert - verify body includes rulesAccepted
      const callArgs = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
      const body = JSON.parse(callArgs[1].body);
      expect(body.rulesAccepted).toBe(true);
    });

    it('complete payload can have empty group and channel selections', async () => {
      // Arrange
      const serverId = 'srv-empty-sel';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/onboarding/complete`, {
        rulesAccepted: true,
        selectedGroupIds: [],
        selectedChannelIds: [],
      });

      // Assert
      const callArgs = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
      const body = JSON.parse(callArgs[1].body);
      expect(body.selectedGroupIds).toHaveLength(0);
      expect(body.selectedChannelIds).toHaveLength(0);
    });
  });
});
