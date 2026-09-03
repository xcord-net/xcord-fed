import { createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import Modal from './ui/Modal';
import Flexbox from './ui/Flexbox';
import { getErrorMessage } from '../utils/errors';
import styles from './CreateServerModal.module.css';

interface CreateServerModalProps {
  onClose: () => void;
}

export default function CreateServerModal(props: CreateServerModalProps) {
  const [name, setName] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const serverStore = useServers();
  const navigate = useNavigate();

  const handleCreate = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const server = await serverStore.createServer(name());

      // Straight in on the channel the server was created with. This used to
      // fetch the whole channel list first and search it for one named
      // "general", which put a second round trip between the click and any
      // visible change - for an id the create response already knew. Changing
      // route loads the new server's channels on its own (useLayoutWiring), so
      // fetching here as well would only put a second answer in flight racing
      // the first.
      navigate(`/channels/${server.id}/${server.systemChannelId ?? ''}`);
      props.onClose();
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to create server'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal data-testid="create-server-dialog" open={true} onClose={props.onClose} title="Create a Server" size="md">
      <div class={styles.modalBody}>
        <form onSubmit={handleCreate}>
          <div class={styles.fieldGroup}>
            <label for="server-name" class={styles.fieldLabel}>
              Server Name
            </label>
            <input
              id="server-name"
              data-testid="create-server-name-input"
              type="text"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              class={styles.textInput}
              autofocus
            />
          </div>
          {error() && (
            <div role="alert" class={styles.errorAlert}>{error()}</div>
          )}
          <Flexbox justify="end" gap={0.75}>
            <button
              type="button"
              onClick={() => props.onClose()}
              class={styles.cancelBtn}
            >
              Cancel
            </button>
            <button
              data-testid="create-server-submit-button"
              type="submit"
              disabled={!name().trim() || loading()}
              class={styles.submitBtn}
            >
              {loading() ? 'Creating...' : 'Create'}
            </button>
          </Flexbox>
        </form>
      </div>
    </Modal>
  );
}
