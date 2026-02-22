import { For, Show, Switch, Match, createSignal, createEffect, createMemo } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface OnboardingRole {
  id: string;
  name: string;
  description?: string;
  emoji?: string;
}

export interface OnboardingChannel {
  channelId: string;
  channelName: string;
  description?: string;
}

export interface OnboardingConfig {
  serverId: string;
  promptMessage: string;
  rules: string;
  roles: OnboardingRole[];
  channels: OnboardingChannel[];
  totalSteps: number;
}

export type OnboardingStep = 'rules' | 'roles' | 'channels' | 'complete';

interface ServerOnboardingProps {
  serverId: string;
  onComplete?: () => void;
}

// ---- Pure helpers ----

export function getOnboardingSteps(): OnboardingStep[] {
  return ['rules', 'roles', 'channels', 'complete'];
}

export function stepIndex(step: OnboardingStep): number {
  return getOnboardingSteps().indexOf(step);
}

export function nextStep(current: OnboardingStep): OnboardingStep | null {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(current);
  if (idx < 0 || idx >= steps.length - 1) return null;
  return steps[idx + 1];
}

export function previousStep(current: OnboardingStep): OnboardingStep | null {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(current);
  if (idx <= 0) return null;
  return steps[idx - 1];
}

export function stepLabel(step: OnboardingStep): string {
  switch (step) {
    case 'rules':
      return 'Rules';
    case 'roles':
      return 'Interests';
    case 'channels':
      return 'Channels';
    case 'complete':
      return 'Done';
  }
}

export function progressPercent(step: OnboardingStep): number {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(step);
  return Math.round((idx / (steps.length - 1)) * 100);
}

export function toggleRoleSelection(selectedIds: string[], roleId: string): string[] {
  return selectedIds.includes(roleId)
    ? selectedIds.filter((id) => id !== roleId)
    : [...selectedIds, roleId];
}

export function toggleChannelSelection(selectedIds: string[], channelId: string): string[] {
  return selectedIds.includes(channelId)
    ? selectedIds.filter((id) => id !== channelId)
    : [...selectedIds, channelId];
}

// ---- Component ----

export default function ServerOnboarding(props: ServerOnboardingProps) {
  const [config, setConfig] = createSignal<OnboardingConfig | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [currentStep, setCurrentStep] = createSignal<OnboardingStep>('rules');
  const [rulesAccepted, setRulesAccepted] = createSignal(false);
  const [selectedRoleIds, setSelectedRoleIds] = createSignal<string[]>([]);
  const [selectedChannelIds, setSelectedChannelIds] = createSignal<string[]>([]);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);

  const loadConfig = async (serverId: string) => {
    setIsLoading(true);
    try {
      const data = await api.get<OnboardingConfig>(
        `/api/v1/servers/${serverId}/onboarding`,
      );
      setConfig(data);
    } catch {
      setConfig(null);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadConfig(serverId);
    }
  });

  const canProceedFromRules = createMemo(() => rulesAccepted());

  const handleToggleRole = (roleId: string) => {
    setSelectedRoleIds((prev) =>
      prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId],
    );
  };

  const handleToggleChannel = (channelId: string) => {
    setSelectedChannelIds((prev) =>
      prev.includes(channelId)
        ? prev.filter((id) => id !== channelId)
        : [...prev, channelId],
    );
  };

  const handleNext = () => {
    const next = nextStep(currentStep());
    if (next) {
      setCurrentStep(next);
      setSubmitError(null);
    }
  };

  const handleBack = () => {
    const prev = previousStep(currentStep());
    if (prev) {
      setCurrentStep(prev);
      setSubmitError(null);
    }
  };

  const handleComplete = async () => {
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      await api.post(`/api/v1/servers/${props.serverId}/onboarding/complete`, {
        rulesAccepted: rulesAccepted(),
        selectedRoleIds: selectedRoleIds(),
        selectedChannelIds: selectedChannelIds(),
      });
      setCurrentStep('complete');
      props.onComplete?.();
    } catch {
      setSubmitError('Failed to complete onboarding. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const steps = getOnboardingSteps();
  const totalSteps = steps.length;

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <Show when={isLoading()}>
        <div class="flex items-center justify-center flex-1">
          <div class="w-6 h-6 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      <Show when={!isLoading() && config()}>
        <div class="flex flex-col h-full">
          {/* Progress header */}
          <div class="px-6 pt-6 pb-4 flex-shrink-0">
            <div class="flex items-center justify-between mb-2">
              <h2 class="text-xcord-text-primary font-bold text-lg">
                {config()!.promptMessage}
              </h2>
              <span class="text-xcord-text-muted text-sm">
                Step {stepIndex(currentStep()) + 1} of {totalSteps}
              </span>
            </div>

            {/* Progress bar */}
            <div class="w-full bg-xcord-bg-tertiary rounded-full h-1.5 overflow-hidden">
              <div
                class="bg-xcord-brand h-1.5 rounded-full transition-all duration-300"
                style={{ width: `${progressPercent(currentStep())}%` }}
                role="progressbar"
                aria-valuenow={progressPercent(currentStep())}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label={`Onboarding progress: ${progressPercent(currentStep())}%`}
              />
            </div>

            {/* Step indicators */}
            <div class="flex justify-between mt-2">
              <For each={steps}>
                {(step) => (
                  <span
                    class={`text-xs transition-colors ${
                      step === currentStep()
                        ? 'text-xcord-brand font-semibold'
                        : stepIndex(step) < stepIndex(currentStep())
                          ? 'text-xcord-text-primary'
                          : 'text-xcord-text-muted'
                    }`}
                  >
                    {stepLabel(step)}
                  </span>
                )}
              </For>
            </div>
          </div>

          {/* Step content */}
          <div class="flex-1 overflow-y-auto px-6 pb-4">
            <Switch>
              {/* Step 1: Rules */}
              <Match when={currentStep() === 'rules'}>
                <div class="space-y-4">
                  <h3 class="text-xcord-text-primary font-semibold">Server Rules</h3>
                  <div class="bg-xcord-bg-primary rounded-lg p-4 max-h-64 overflow-y-auto">
                    <p class="text-xcord-text-primary text-sm whitespace-pre-wrap">
                      {config()!.rules}
                    </p>
                  </div>
                  <label class="flex items-center gap-3 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      class="w-4 h-4 rounded accent-xcord-brand cursor-pointer"
                      checked={rulesAccepted()}
                      onChange={(e) => setRulesAccepted(e.currentTarget.checked)}
                      aria-label="I agree to the server rules"
                    />
                    <span class="text-xcord-text-primary text-sm">
                      I have read and agree to the server rules
                    </span>
                  </label>
                </div>
              </Match>

              {/* Step 2: Roles / Interests */}
              <Match when={currentStep() === 'roles'}>
                <div class="space-y-4">
                  <h3 class="text-xcord-text-primary font-semibold">Choose Your Interests</h3>
                  <p class="text-xcord-text-muted text-sm">
                    Select roles that match your interests. You can change these later.
                  </p>
                  <div class="grid grid-cols-2 gap-2">
                    <For each={config()!.roles}>
                      {(role) => {
                        const isSelected = () => selectedRoleIds().includes(role.id);
                        return (
                          <button
                            class={`flex items-center gap-2 p-3 rounded-lg border text-left transition-colors ${
                              isSelected()
                                ? 'border-xcord-brand bg-xcord-brand/10 text-xcord-text-primary'
                                : 'border-xcord-bg-tertiary bg-xcord-bg-primary text-xcord-text-muted hover:border-xcord-brand/50 hover:text-xcord-text-primary'
                            }`}
                            onClick={() => handleToggleRole(role.id)}
                            aria-pressed={isSelected()}
                            aria-label={`Select interest: ${role.name}`}
                          >
                            <Show when={role.emoji}>
                              <span class="text-lg flex-shrink-0">{role.emoji}</span>
                            </Show>
                            <div class="flex-1 min-w-0">
                              <p class="text-sm font-medium truncate">{role.name}</p>
                              <Show when={role.description}>
                                <p class="text-xs text-xcord-text-muted truncate">
                                  {role.description}
                                </p>
                              </Show>
                            </div>
                          </button>
                        );
                      }}
                    </For>
                  </div>
                  <Show when={config()!.roles.length === 0}>
                    <p class="text-xcord-text-muted text-sm">
                      No interest roles configured for this server.
                    </p>
                  </Show>
                </div>
              </Match>

              {/* Step 3: Channels */}
              <Match when={currentStep() === 'channels'}>
                <div class="space-y-4">
                  <h3 class="text-xcord-text-primary font-semibold">Browse Channels</h3>
                  <p class="text-xcord-text-muted text-sm">
                    Select channels you want to follow. You can always explore more later.
                  </p>
                  <div class="space-y-2">
                    <For each={config()!.channels}>
                      {(channel) => {
                        const isSelected = () => selectedChannelIds().includes(channel.channelId);
                        return (
                          <button
                            class={`w-full flex items-center gap-3 p-3 rounded-lg border text-left transition-colors ${
                              isSelected()
                                ? 'border-xcord-brand bg-xcord-brand/10'
                                : 'border-xcord-bg-tertiary bg-xcord-bg-primary hover:border-xcord-brand/50'
                            }`}
                            onClick={() => handleToggleChannel(channel.channelId)}
                            aria-pressed={isSelected()}
                            aria-label={`Select channel: ${channel.channelName}`}
                          >
                            <span class="text-xcord-text-muted text-sm">#</span>
                            <div class="flex-1 min-w-0">
                              <p
                                class={`text-sm font-medium truncate ${
                                  isSelected()
                                    ? 'text-xcord-text-primary'
                                    : 'text-xcord-text-muted'
                                }`}
                              >
                                {channel.channelName}
                              </p>
                              <Show when={channel.description}>
                                <p class="text-xs text-xcord-text-muted truncate">
                                  {channel.description}
                                </p>
                              </Show>
                            </div>
                            <Show when={isSelected()}>
                              <span class="text-xcord-brand text-sm flex-shrink-0">✓</span>
                            </Show>
                          </button>
                        );
                      }}
                    </For>
                  </div>
                  <Show when={config()!.channels.length === 0}>
                    <p class="text-xcord-text-muted text-sm">
                      No channels configured for this step.
                    </p>
                  </Show>
                </div>
              </Match>

              {/* Step 4: Complete */}
              <Match when={currentStep() === 'complete'}>
                <div class="flex flex-col items-center justify-center py-12 space-y-4 text-center">
                  <div class="w-16 h-16 rounded-full bg-green-500/20 flex items-center justify-center">
                    <span class="text-3xl">✓</span>
                  </div>
                  <h3 class="text-xcord-text-primary font-bold text-xl">
                    You're all set!
                  </h3>
                  <p class="text-xcord-text-muted text-sm max-w-xs">
                    Welcome to the server! You can explore channels and customize your preferences
                    anytime.
                  </p>
                </div>
              </Match>
            </Switch>
          </div>

          {/* Navigation footer */}
          <Show when={currentStep() !== 'complete'}>
            <div class="px-6 py-4 border-t border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
              <button
                class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                onClick={handleBack}
                disabled={stepIndex(currentStep()) === 0}
                aria-label="Go back"
              >
                Back
              </button>

              <div class="flex items-center gap-3">
                <Show when={submitError()}>
                  <p class="text-red-400 text-xs" role="alert">
                    {submitError()}
                  </p>
                </Show>

                <Show when={currentStep() !== 'channels'}>
                  <button
                    class="px-5 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    onClick={handleNext}
                    disabled={currentStep() === 'rules' && !canProceedFromRules()}
                    aria-label="Next step"
                  >
                    Next
                  </button>
                </Show>

                <Show when={currentStep() === 'channels'}>
                  <button
                    class="px-5 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    onClick={handleComplete}
                    disabled={isSubmitting()}
                    aria-label="Complete onboarding"
                  >
                    {isSubmitting() ? 'Completing...' : 'Finish'}
                  </button>
                </Show>
              </div>
            </div>
          </Show>
        </div>
      </Show>

      <Show when={!isLoading() && !config()}>
        <div class="flex items-center justify-center flex-1">
          <p class="text-xcord-text-muted text-sm">
            No onboarding configured for this server.
          </p>
        </div>
      </Show>
    </div>
  );
}

export { ServerOnboarding };
