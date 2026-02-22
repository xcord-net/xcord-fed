import { createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { createFocusTrap } from '../hooks/createFocusTrap';

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

  let dialogRef!: HTMLDivElement;

  createFocusTrap(() => dialogRef, { onEscape: () => props.onClose() });

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
    <div
      class="fixed inset-0 bg-black/70 flex items-center justify-center z-50"
      onClick={(e) => { if (e.target === e.currentTarget) props.onClose(); }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label="Create a Server"
        class="bg-xcord-bg-secondary rounded-lg shadow-xl w-full max-w-md p-6"
      >
        <h2 class="text-xl font-bold text-xcord-text-primary mb-4">Create a Server</h2>
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
            <p role="alert" class="text-red-400 text-sm mb-4">{error()}</p>
          )}
          <div class="flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => props.onClose()}
              class="px-4 py-2 text-xcord-text-secondary hover:text-xcord-text-primary transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none rounded"
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
    </div>
  );
}
