import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import { useChannels } from '../stores/channel.store';
import ChannelPermissions from './ChannelPermissions';
import Modal from './ui/Modal';

interface ChannelSettingsProps {
  serverId: string;
  channelId: string;
  onClose: () => void;
}

const SLOWMODE_OPTIONS: { label: string; value: number }[] = [
  { label: 'Off', value: 0 },
  { label: '5 seconds', value: 5 },
  { label: '10 seconds', value: 10 },
  { label: '15 seconds', value: 15 },
  { label: '30 seconds', value: 30 },
  { label: '1 minute', value: 60 },
  { label: '2 minutes', value: 120 },
  { label: '5 minutes', value: 300 },
  { label: '10 minutes', value: 600 },
  { label: '15 minutes', value: 900 },
  { label: '30 minutes', value: 1800 },
  { label: '1 hour', value: 3600 },
  { label: '2 hours', value: 7200 },
  { label: '6 hours', value: 21600 },
];

export default function ChannelSettings(props: ChannelSettingsProps) {
  const channelStore = useChannels();

  const currentChannel = () => channelStore.channels.find((c) => c.id === props.channelId);

  const [channelName, setChannelName] = createSignal('');
  const [topic, setTopic] = createSignal('');
  const [slowMode, setSlowMode] = createSignal(0);
  const [isNsfw, setIsNsfw] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [successMsg, setSuccessMsg] = createSignal('');
  const [errorMsg, setErrorMsg] = createSignal('');
  const [activeTab, setActiveTab] = createSignal<'overview' | 'permissions'>('overview');
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);

  onMount(() => {
    const channel = currentChannel();
    if (channel) {
      setChannelName(channel.name);
      setTopic(channel.topic ?? '');
      setSlowMode(channel.slowModeSeconds);
      setIsNsfw(channel.isNsfw);
    }
  });

  const handleSave = async (e: Event) => {
    e.preventDefault();
    setIsSaving(true);
    setSuccessMsg('');
    setErrorMsg('');

    try {
      await api.patch(`/api/v1/channels/${props.channelId}`, {
        name: channelName().trim(),
        topic: topic().trim() || null,
        slowModeSeconds: slowMode(),
        isNsfw: isNsfw(),
      });
      channelStore.updateChannel(props.channelId, {
        name: channelName().trim(),
        topic: topic().trim() || undefined,
        slowModeSeconds: slowMode(),
        isNsfw: isNsfw(),
      });
      setSuccessMsg('Channel settings saved successfully.');
    } catch (err: unknown) {
      const e = err as { detail?: string; error?: string; message?: string };
      setErrorMsg(e?.detail ?? e?.error ?? e?.message ?? 'Failed to save channel settings.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      <Modal open={true} onClose={props.onClose} aria-label="Channel Settings" size="lg">
        {/* Header */}
        <div class="flex items-center justify-between px-6 py-4 border-b border-xcord-border">
          <div>
            <h2 class="text-xl font-bold text-white">Channel Settings</h2>
            <p class="text-xcord-text-muted text-sm mt-0.5">
              #{currentChannel()?.name ?? props.channelId}
            </p>
          </div>
          <button
            type="button"
            aria-label="Close channel settings"
            onClick={props.onClose}
            class="text-xcord-text-muted hover:text-white transition-colors rounded focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
          >
            &#10005;
          </button>
        </div>

        {/* Tab navigation */}
        <div class="flex border-b border-xcord-border px-6">
          <button
            type="button"
            class={`px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-none ${
              activeTab() === 'overview'
                ? 'text-white border-b-2 border-xcord-brand'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => setActiveTab('overview')}
          >
            Overview
          </button>
          <button
            type="button"
            aria-label="Channel Permissions tab"
            class={`px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-none ${
              activeTab() === 'permissions'
                ? 'text-white border-b-2 border-xcord-brand'
                : 'text-xcord-text-muted hover:text-white'
            }`}
            onClick={() => setActiveTab('permissions')}
          >
            Permissions
          </button>
        </div>

        {/* Body */}
        <Show when={activeTab() === 'permissions'}>
          <div class="h-[500px]">
            <ChannelPermissions serverId={props.serverId} channelId={props.channelId} />
          </div>
        </Show>
        <Show when={activeTab() === 'overview'}>
          <form onSubmit={handleSave}>
            {/* Overview section */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-4">Overview</h3>

              {/* Channel name */}
              <div class="mb-4">
                <label for="channel-name" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Channel Name <span class="text-red-400">*</span>
                </label>
                <div class="relative">
                  <span class="absolute left-3 top-1/2 -translate-y-1/2 text-xcord-text-muted text-sm" aria-hidden="true">#</span>
                  <input
                    id="channel-name"
                    type="text"
                    required
                    maxlength="100"
                    value={channelName()}
                    onInput={(e) => setChannelName(e.currentTarget.value)}
                    class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded pl-7 pr-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                    placeholder="channel-name"
                  />
                </div>
              </div>

              {/* Topic */}
              <div>
                <label for="channel-topic" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Channel Topic
                </label>
                <textarea
                  id="channel-topic"
                  rows="3"
                  maxlength="1024"
                  value={topic()}
                  onInput={(e) => setTopic(e.currentTarget.value)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand resize-none"
                  placeholder="Let everyone know the purpose of this channel..."
                />
              </div>
            </section>

            {/* Permissions section */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-4">Permissions</h3>

              {/* Slowmode */}
              <div class="mb-5">
                <label for="slowmode" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Slowmode
                </label>
                <select
                  id="slowmode"
                  value={String(slowMode())}
                  onChange={(e) => setSlowMode(parseInt(e.currentTarget.value, 10))}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                >
                  <For each={SLOWMODE_OPTIONS}>
                    {(opt) => (
                      <option value={String(opt.value)}>{opt.label}</option>
                    )}
                  </For>
                </select>
                <p class="text-xcord-text-muted text-xs mt-1">
                  Users must wait between sending messages.
                </p>
              </div>

              {/* NSFW toggle */}
              <div class="flex items-center justify-between">
                <div>
                  <label for="nsfw-toggle" class="text-sm font-medium text-xcord-text-primary cursor-pointer">
                    Age-Restricted Channel (NSFW)
                  </label>
                  <p class="text-xcord-text-muted text-xs mt-0.5">
                    Users must confirm they are 18+ to view this channel.
                  </p>
                </div>
                <button
                  type="button"
                  id="nsfw-toggle"
                  role="switch"
                  aria-checked={isNsfw()}
                  onClick={() => setIsNsfw(!isNsfw())}
                  class={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none flex-shrink-0 ${
                    isNsfw() ? 'bg-xcord-brand' : 'bg-xcord-bg-primary'
                  }`}
                >
                  <span
                    class={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                      isNsfw() ? 'translate-x-6' : 'translate-x-1'
                    }`}
                  />
                </button>
              </div>
            </section>

            {/* Danger Zone */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-red-400 uppercase tracking-wide mb-4">Danger Zone</h3>
              <button
                type="button"
                class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
                onClick={() => setShowDeleteConfirm(true)}
              >
                Delete Channel
              </button>
            </section>

            {/* Status messages */}
            <Show when={successMsg()}>
              <div role="status" class="mx-6 mb-4 px-4 py-2 bg-green-500/20 border border-green-500/30 rounded text-green-400 text-sm">
                {successMsg()}
              </div>
            </Show>
            <Show when={errorMsg()}>
              <div role="alert" class="mx-6 mb-4 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">
                {errorMsg()}
              </div>
            </Show>

            {/* Footer actions */}
            <div class="px-6 pb-5 flex justify-end gap-3">
              <button
                type="button"
                onClick={props.onClose}
                class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSaving()}
                class="px-5 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
              >
                {isSaving() ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>
        </Show>
      </Modal>

      {/* Delete channel confirmation */}
      <Modal open={showDeleteConfirm()} onClose={() => setShowDeleteConfirm(false)} title="Delete Channel" size="sm" role="alertdialog">
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-6">Are you sure you want to delete this channel? This cannot be undone.</p>
          <div class="flex justify-end gap-3">
            <button
              type="button"
              onClick={() => setShowDeleteConfirm(false)}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => { channelStore.deleteChannel(props.channelId).then(() => props.onClose()); }}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
            >
              Delete Channel
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
