import { createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import Modal from './ui/Modal';

interface CreateServerModalProps {
  onClose: () => void;
}

export default function CreateServerModal(props: CreateServerModalProps) {
  const [name, setName] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const serverStore = useServers();
  const channelStore = useChannels();
  const navigate = useNavigate();

  const handleCreate = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const server = await serverStore.createServer(name());
      await channelStore.fetchChannels(server.id);
      const generalChannel = channelStore.channels.find(c => c.name === 'general') ?? channelStore.channels[0];
      navigate(`/channels/${server.id}/${generalChannel?.id ?? ''}`);
      props.onClose();
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setError(e?.detail || e?.message || 'Failed to create server');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open={true} onClose={props.onClose} title="Create a Server" size="md">
      <div class="p-6">
        <form onSubmit={handleCreate}>
          <div class="mb-4">
            <label for="server-name" class="block text-xcord-text-secondary text-sm font-medium mb-2">
              Server Name
            </label>
            <input
              id="server-name"
              type="text"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand focus:outline-none"
              autofocus
            />
          </div>
          {error() && (
            <div role="alert" class="mb-4 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
          )}
          <div class="flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => props.onClose()}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={!name().trim() || loading()}
              class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium rounded disabled:opacity-50 transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
            >
              {loading() ? 'Creating...' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </Modal>
  );
}
