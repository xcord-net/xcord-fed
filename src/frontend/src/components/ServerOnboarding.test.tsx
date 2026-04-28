import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ServerOnboarding, {
  getOnboardingSteps,
  stepIndex,
  nextStep,
  previousStep,
  stepLabel,
  progressPercent,
  toggleGroupSelection,
  toggleChannelSelection,
} from './ServerOnboarding';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleConfig = {
  serverId: 's-1',
  promptMessage: 'Welcome to the server!',
  rules: 'Be excellent to each other.',
  groups: [
    { id: 'g-1', name: 'Gaming', description: 'Talk about games', emoji: '🎮' },
    { id: 'g-2', name: 'Music', description: 'Music channels' },
  ],
  channels: [
    { channelId: 'c-1', channelName: 'general', description: 'Main chat' },
  ],
  totalSteps: 4,
};

describe('ServerOnboarding pure helpers', () => {
  it('getOnboardingSteps returns all four steps in order', () => {
    expect(getOnboardingSteps()).toEqual(['rules', 'groups', 'channels', 'complete']);
  });

  it('stepIndex returns the position', () => {
    expect(stepIndex('rules')).toBe(0);
    expect(stepIndex('complete')).toBe(3);
  });

  it('nextStep advances by one and returns null at end', () => {
    expect(nextStep('rules')).toBe('groups');
    expect(nextStep('complete')).toBeNull();
  });

  it('previousStep goes back by one and returns null at start', () => {
    expect(previousStep('groups')).toBe('rules');
    expect(previousStep('rules')).toBeNull();
  });

  it('stepLabel returns a friendly label for each step', () => {
    expect(stepLabel('rules')).toBe('Rules');
    expect(stepLabel('groups')).toBe('Interests');
  });

  it('progressPercent maps steps to 0..100', () => {
    expect(progressPercent('rules')).toBe(0);
    expect(progressPercent('complete')).toBe(100);
  });

  it('toggleGroupSelection adds and removes ids', () => {
    expect(toggleGroupSelection([], 'g-1')).toEqual(['g-1']);
    expect(toggleGroupSelection(['g-1'], 'g-1')).toEqual([]);
  });

  it('toggleChannelSelection adds and removes ids', () => {
    expect(toggleChannelSelection([], 'c-1')).toEqual(['c-1']);
    expect(toggleChannelSelection(['c-1'], 'c-1')).toEqual([]);
  });
});

describe('ServerOnboarding', () => {
  it('renders the prompt message from the loaded config', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/onboarding': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText } = render(() => <ServerOnboarding serverId="s-1" />);
    expect(await findByText('Welcome to the server!')).toBeInTheDocument();
  });

  it('shows the rules text on the first step', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/onboarding': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText } = render(() => <ServerOnboarding serverId="s-1" />);
    expect(await findByText('Be excellent to each other.')).toBeInTheDocument();
    expect(await findByText('Server Rules')).toBeInTheDocument();
  });

  it('disables Next on rules until the agreement checkbox is checked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/onboarding': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText, container } = render(() => <ServerOnboarding serverId="s-1" />);
    const nextBtn = (await findByText('Next')) as HTMLButtonElement;
    expect(nextBtn.disabled).toBe(true);
    const checkbox = container.querySelector('input[type="checkbox"]') as HTMLInputElement;
    fireEvent.click(checkbox);
    await waitFor(() => expect(nextBtn.disabled).toBe(false));
  });

  it('advances to the groups step when Next is clicked after accepting rules', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/onboarding': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText, container } = render(() => <ServerOnboarding serverId="s-1" />);
    // waitFor retries on thrown errors, not on null returns — so assert and throw.
    const checkbox: HTMLInputElement = await waitFor(() => {
      const el = container.querySelector('input[type="checkbox"]');
      if (!el) throw new Error('checkbox not yet rendered');
      return el as HTMLInputElement;
    });
    fireEvent.click(checkbox);
    fireEvent.click(await findByText('Next'));
    expect(await findByText('Choose Your Interests')).toBeInTheDocument();
  });

  it('shows "No onboarding configured" empty state when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/onboarding': () => ({ status: 500, body: { message: 'fail' } }),
    });
    const { findByText } = render(() => <ServerOnboarding serverId="s-1" />);
    expect(await findByText(/No onboarding configured/i)).toBeInTheDocument();
  });
});
