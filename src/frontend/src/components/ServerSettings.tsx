import { For, Show, createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { api } from '../api/client';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useAuth } from '../stores/auth.store';
import type { Channel } from '../types/channel';
import AutomodManager from './AutomodManager';
import BanManager from './BanManager';
import AuditLogViewer from './AuditLogViewer';
import EmojiManager from './EmojiManager';
import StickerPicker from './StickerPicker';
import VanityInvite from './VanityInvite';
import ServerTemplates from './ServerTemplates';
import ServerBoost from './ServerBoost';
import ServerInsights from './ServerInsights';
import InviteManager from './InviteManager';
import AppDirectory from './AppDirectory';
import OwnershipTransfer from './OwnershipTransfer';
import WelcomeScreen from './WelcomeScreen';
import Modal from './ui/Modal';
import { getErrorMessage } from '../utils/errors';

interface ServerSettingsProps {
  serverId: string;
  onClose: () => void;
}

type NotificationLevel = 'AllMessages' | 'OnlyMentions' | 'Nothing';
type SettingsTab = 'overview' | 'automod' | 'bans' | 'audit-log' | 'emoji' | 'stickers' | 'vanity-url' | 'templates' | 'boost' | 'insights' | 'invites' | 'app-directory' | 'welcome-screen';

const NOTIFICATION_OPTIONS: { label: string; value: NotificationLevel }[] = [
  { label: 'All Messages', value: 'AllMessages' },
  { label: 'Only @Mentions', value: 'OnlyMentions' },
  { label: 'Nothing', value: 'Nothing' },
];

const TABS: { id: SettingsTab; label: string; ownerOnly?: boolean }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'automod', label: 'Automod', ownerOnly: true },
  { id: 'bans', label: 'Bans', ownerOnly: true },
  { id: 'audit-log', label: 'Audit Log', ownerOnly: true },
  { id: 'emoji', label: 'Emoji' },
  { id: 'stickers', label: 'Stickers' },
  { id: 'vanity-url', label: 'Vanity URL', ownerOnly: true },
  { id: 'templates', label: 'Templates', ownerOnly: true },
  { id: 'boost', label: 'Boost' },
  { id: 'insights', label: 'Insights', ownerOnly: true },
  { id: 'invites', label: 'Invites', ownerOnly: true },
  { id: 'app-directory', label: 'App Directory' },
  { id: 'welcome-screen', label: 'Welcome Screen' },
];

export default function ServerSettings(props: ServerSettingsProps) {
  const navigate = useNavigate();
  const serverStore = useServers();
  const channelStore = useChannels();
  const auth = useAuth();

  const currentServer = () => serverStore.servers.find((s) => s.id === props.serverId);
  const textChannels = () => channelStore.channels.filter((c) => c.type === 'Text');
  const isOwner = () => {
    const server = currentServer();
    const userId = auth.user?.id;
    return !!server && !!userId && server.ownerId === userId;
  };
  const visibleTabs = () => TABS.filter((tab) => !tab.ownerOnly || isOwner());

  const [activeTab, setActiveTab] = createSignal<SettingsTab>('overview');
  const [name, setName] = createSignal('');
  const [description, setDescription] = createSignal('');
  const [systemChannelId, setSystemChannelId] = createSignal<string>('');
  const [defaultNotifications, setDefaultNotifications] = createSignal<NotificationLevel>('AllMessages');
  const [isSaving, setIsSaving] = createSignal(false);
  const [successMsg, setSuccessMsg] = createSignal('');
  const [errorMsg, setErrorMsg] = createSignal('');
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);

  onMount(async () => {
    // Ensure servers are loaded so isOwner() resolves correctly before
    // rendering owner-only tabs (Invites, Vanity URL, Templates, etc.).
    if (serverStore.servers.length === 0) {
      await serverStore.fetchServers();
    }
    const server = currentServer();
    if (server) {
      setName(server.name);
      setDescription(server.description ?? '');
    }
  });

  const handleSave = async (e: Event) => {
    e.preventDefault();
    setIsSaving(true);
    setSuccessMsg('');
    setErrorMsg('');

    try {
      await api.patch(`/api/v1/servers/${props.serverId}`, {
        name: name().trim(),
        description: description().trim() || null,
        systemChannelId: systemChannelId() || null,
        defaultNotificationLevel: defaultNotifications(),
      });
      serverStore.updateServer(props.serverId, {
        name: name().trim(),
        description: description().trim() || undefined,
      });
      setSuccessMsg('Server settings saved successfully.');
    } catch (err: unknown) {
      setErrorMsg(getErrorMessage(err, 'Failed to save settings.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      <Modal open={true} onClose={props.onClose} aria-label="Server Settings" size="xl">
        {/* Header */}
        <div class="flex items-center justify-between px-6 py-4 border-b border-xcord-border">
          <h2 class="text-xl font-bold text-xcord-text-primary">Server Settings</h2>
          <button
            type="button"
            aria-label="Close settings"
            onClick={props.onClose}
            class="text-xcord-text-muted hover:text-white transition-colors rounded focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
          >
            &#10005;
          </button>
        </div>

        {/* Tab navigation */}
        <div class="flex flex-wrap border-b border-xcord-border px-6">
          <For each={visibleTabs()}>
            {(tab) => (
              <button
                type="button"
                class={`px-4 py-2 text-sm font-medium transition-colors ${
                  activeTab() === tab.id
                    ? 'text-white border-b-2 border-xcord-brand'
                    : 'text-xcord-text-muted hover:text-white'
                }`}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            )}
          </For>
        </div>

        {/* Body */}
        {/* Overview tab */}
        <Show when={activeTab() === 'overview'}>
          <form onSubmit={handleSave}>
            {/* Overview section */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-4">Overview</h3>

              {/* Server icon placeholder */}
              <div class="flex items-center gap-4 mb-5">
                <div
                  class="w-20 h-20 rounded-full bg-xcord-brand flex items-center justify-center text-white text-2xl font-bold select-none flex-shrink-0"
                  aria-label="Server icon"
                >
                  {name() ? name().charAt(0).toUpperCase() : '?'}
                </div>
                <div>
                  <p class="text-white font-medium">{name() || currentServer()?.name}</p>
                  <p class="text-xcord-text-muted text-sm mt-0.5">Icon upload coming soon</p>
                </div>
              </div>

              {/* Server name */}
              <div class="mb-4">
                <label for="server-name" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Server Name <span class="text-red-400">*</span>
                </label>
                <input
                  id="server-name"
                  type="text"
                  required
                  maxlength="100"
                  value={name()}
                  onInput={(e) => setName(e.currentTarget.value)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                  placeholder="My Awesome Server"
                />
              </div>

              {/* Description */}
              <div>
                <label for="server-description" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Description
                </label>
                <textarea
                  id="server-description"
                  rows="3"
                  maxlength="1000"
                  value={description()}
                  onInput={(e) => setDescription(e.currentTarget.value)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand resize-none"
                  placeholder="Tell people what your server is about..."
                />
              </div>
            </section>

            {/* System Messages section */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-4">System Messages</h3>

              <div>
                <label for="system-channel" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  System Messages Channel
                </label>
                <select
                  id="system-channel"
                  value={systemChannelId()}
                  onChange={(e) => setSystemChannelId(e.currentTarget.value)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                >
                  <option value="">None</option>
                  <For each={textChannels()}>
                    {(channel: Channel) => (
                      <option value={channel.id}>#{channel.name}</option>
                    )}
                  </For>
                </select>
                <p class="text-xcord-text-muted text-xs mt-1">
                  Channel where join/leave and server boost messages are sent.
                </p>
              </div>
            </section>

            {/* Default Notifications section */}
            <section class="px-6 py-5 border-b border-xcord-border">
              <h3 class="text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-4">Default Notification Settings</h3>

              <div>
                <label for="default-notifications" class="block text-xs font-semibold text-xcord-text-muted uppercase tracking-wide mb-1.5">
                  Default Notification Level
                </label>
                <select
                  id="default-notifications"
                  value={defaultNotifications()}
                  onChange={(e) => setDefaultNotifications(e.currentTarget.value as NotificationLevel)}
                  class="w-full bg-xcord-bg-tertiary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                >
                  <For each={NOTIFICATION_OPTIONS}>
                    {(opt) => (
                      <option value={opt.value}>{opt.label}</option>
                    )}
                  </For>
                </select>
                <p class="text-xcord-text-muted text-xs mt-1">
                  Controls what notifications members receive by default.
                </p>
              </div>
            </section>

            {/* Status messages */}
            <Show when={successMsg()}>
              <div role="status" class="mx-6 mb-4 mt-4 px-4 py-2 bg-green-500/20 border border-green-500/30 rounded text-green-400 text-sm">
                {successMsg()}
              </div>
            </Show>
            <Show when={errorMsg()}>
              <div role="alert" class="mx-6 mb-4 mt-4 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">
                {errorMsg()}
              </div>
            </Show>

            {/* Footer actions */}
            <div class="px-6 py-5 flex justify-end gap-3">
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

          {/* Danger Zone */}
          <Show when={isOwner()}>
            <section class="px-6 py-5 border-t border-xcord-border">
              <h3 class="text-xs font-semibold text-red-400 uppercase tracking-wide mb-4">Danger Zone</h3>
              <Show when={auth.user?.id && currentServer()?.ownerId}>
                <OwnershipTransfer
                  serverId={props.serverId}
                  serverName={currentServer()?.name ?? ''}
                  currentUserId={auth.user!.id}
                  ownerId={currentServer()!.ownerId}
                  onTransferred={(newOwnerId) => {
                    serverStore.updateServer(props.serverId, { ownerId: newOwnerId });
                  }}
                />
              </Show>
              <button
                type="button"
                class="mt-4 px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
                onClick={() => setShowDeleteConfirm(true)}
              >
                Delete Server
              </button>
            </section>
          </Show>
        </Show>

        {/* Automod tab */}
        <Show when={activeTab() === 'automod'}>
          <AutomodManager serverId={props.serverId} />
        </Show>

        {/* Bans tab */}
        <Show when={activeTab() === 'bans'}>
          <BanManager serverId={props.serverId} />
        </Show>

        {/* Audit Log tab */}
        <Show when={activeTab() === 'audit-log'}>
          <AuditLogViewer serverId={props.serverId} />
        </Show>

        {/* Emoji tab */}
        <Show when={activeTab() === 'emoji'}>
          <EmojiManager serverId={props.serverId} />
        </Show>

        {/* Stickers tab */}
        <Show when={activeTab() === 'stickers'}>
          <StickerPicker serverId={props.serverId} canManage={true} />
        </Show>

        {/* Vanity URL tab */}
        <Show when={activeTab() === 'vanity-url'}>
          <div class="px-6 py-5">
            <VanityInvite serverId={props.serverId} isOwner={true} />
          </div>
        </Show>

        {/* Templates tab */}
        <Show when={activeTab() === 'templates'}>
          <ServerTemplates serverId={props.serverId} isOwner={true} />
        </Show>

        {/* Boost tab */}
        <Show when={activeTab() === 'boost'}>
          <ServerBoost serverId={props.serverId} />
        </Show>

        {/* Insights tab */}
        <Show when={activeTab() === 'insights'}>
          <ServerInsights serverId={props.serverId} />
        </Show>

        {/* App Directory tab */}
        <Show when={activeTab() === 'app-directory'}>
          <AppDirectory
            availableServerIds={[props.serverId]}
            serverNames={{ [props.serverId]: currentServer()?.name ?? props.serverId }}
          />
        </Show>

        {/* Invites tab */}
        <Show when={activeTab() === 'invites'}>
          <InviteManager serverId={props.serverId} />
        </Show>

        {/* Welcome Screen tab */}
        <Show when={activeTab() === 'welcome-screen'}>
          <WelcomeScreen serverId={props.serverId} isOwner={isOwner()} />
        </Show>
      </Modal>

      {/* Delete server confirmation */}
      <Modal open={showDeleteConfirm()} onClose={() => setShowDeleteConfirm(false)} title="Delete Server" size="sm" role="alertdialog">
        <div class="p-6">
          <p class="text-xcord-text-secondary text-sm mb-6">Are you sure you want to delete this server? This cannot be undone.</p>
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
              onClick={() => {
                serverStore.deleteServer(props.serverId).then(() => {
                  props.onClose();
                  navigate('/channels/me');
                });
              }}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:outline-none"
            >
              Delete Server
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
