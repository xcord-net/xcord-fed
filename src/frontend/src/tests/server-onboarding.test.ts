import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type { OnboardingConfig, OnboardingRole, OnboardingChannel } from '../components/ServerOnboarding';
import {
  getOnboardingSteps,
  stepIndex,
  nextStep,
  previousStep,
  stepLabel,
  progressPercent,
  toggleRoleSelection,
  toggleChannelSelection,
} from '../components/ServerOnboarding';

// ---- Test data ----

const makeRole = (overrides: Partial<OnboardingRole> = {}): OnboardingRole => ({
  id: 'role-1',
  name: 'Gaming',
  description: 'For gamers',
  emoji: '🎮',
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
  roles: [
    makeRole({ id: 'role-1', name: 'Gaming', emoji: '🎮' }),
    makeRole({ id: 'role-2', name: 'Music', emoji: '🎵' }),
    makeRole({ id: 'role-3', name: 'Art', emoji: '🎨' }),
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
      expect(steps).toEqual(['rules', 'roles', 'channels', 'complete']);
    });

    it('stepIndex returns 0 for rules (first step)', () => {
      expect(stepIndex('rules')).toBe(0);
    });

    it('stepIndex returns 3 for complete (last step)', () => {
      expect(stepIndex('complete')).toBe(3);
    });

    it('nextStep from rules returns roles', () => {
      expect(nextStep('rules')).toBe('roles');
    });

    it('nextStep from roles returns channels', () => {
      expect(nextStep('roles')).toBe('channels');
    });

    it('nextStep from channels returns complete', () => {
      expect(nextStep('channels')).toBe('complete');
    });

    it('nextStep from complete returns null (no further step)', () => {
      expect(nextStep('complete')).toBeNull();
    });

    it('previousStep from roles returns rules', () => {
      expect(previousStep('roles')).toBe('rules');
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

    it('returns "Interests" for roles step', () => {
      expect(stepLabel('roles')).toBe('Interests');
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
      const pct = progressPercent('roles');
      expect(pct).toBeGreaterThan(0);
      expect(pct).toBeLessThan(100);
    });

    it('channels step has higher progress than roles step', () => {
      expect(progressPercent('channels')).toBeGreaterThan(progressPercent('roles'));
    });
  });

  // ---- Role selection ----

  describe('role selection', () => {
    it('adds a role when toggled and not previously selected', () => {
      // Arrange
      const selectedIds: string[] = [];

      // Act
      const result = toggleRoleSelection(selectedIds, 'role-1');

      // Assert
      expect(result).toContain('role-1');
    });

    it('removes a role when toggled and already selected', () => {
      // Arrange
      const selectedIds = ['role-1', 'role-2'];

      // Act
      const result = toggleRoleSelection(selectedIds, 'role-1');

      // Assert
      expect(result).not.toContain('role-1');
      expect(result).toContain('role-2');
    });

    it('multiple roles can be selected simultaneously', () => {
      // Arrange
      let selectedIds: string[] = [];

      // Act
      selectedIds = toggleRoleSelection(selectedIds, 'role-1');
      selectedIds = toggleRoleSelection(selectedIds, 'role-2');
      selectedIds = toggleRoleSelection(selectedIds, 'role-3');

      // Assert
      expect(selectedIds).toHaveLength(3);
    });

    it('toggle does not mutate the original array', () => {
      // Arrange
      const original = ['role-1'];

      // Act
      toggleRoleSelection(original, 'role-2');

      // Assert — original unchanged
      expect(original).toHaveLength(1);
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

  // ---- OnboardingConfig shape ----

  describe('OnboardingConfig shape', () => {
    it('config has serverId, rules, roles, and channels', () => {
      // Arrange
      const config = makeConfig();

      // Assert
      expect(config.serverId).toBe('srv-1');
      expect(config.rules).toContain('Be respectful');
      expect(config.roles).toHaveLength(3);
      expect(config.channels).toHaveLength(2);
    });

    it('role has id, name, optional description and emoji', () => {
      // Arrange
      const role = makeRole({ id: 'r-1', name: 'Gamer', emoji: '🎮' });

      // Assert
      expect(role.id).toBe('r-1');
      expect(role.name).toBe('Gamer');
      expect(role.emoji).toBe('🎮');
    });

    it('channel description is optional', () => {
      // Arrange
      const channel = makeChannel({ description: undefined });

      // Assert
      expect(channel.description).toBeUndefined();
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
        selectedRoleIds: ['role-1', 'role-2'],
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
        selectedRoleIds: [],
        selectedChannelIds: [],
      });

      // Assert — verify body includes rulesAccepted
      const callArgs = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
      const body = JSON.parse(callArgs[1].body);
      expect(body.rulesAccepted).toBe(true);
    });

    it('complete payload can have empty role and channel selections', async () => {
      // Arrange
      const serverId = 'srv-empty-sel';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/servers/${serverId}/onboarding/complete`, {
        rulesAccepted: true,
        selectedRoleIds: [],
        selectedChannelIds: [],
      });

      // Assert
      const callArgs = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
      const body = JSON.parse(callArgs[1].body);
      expect(body.selectedRoleIds).toHaveLength(0);
      expect(body.selectedChannelIds).toHaveLength(0);
    });
  });
});
