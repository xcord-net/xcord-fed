import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import CreateServerModal from './CreateServerModal';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('CreateServerModal', () => {
  beforeEach(() => {
    useServers().reset();
    useChannels().reset();
  });

  it('renders dialog with name input and submit button', () => {
    const { getByTestId } = renderWithRouter(() => <CreateServerModal onClose={() => {}} />);
    expect(getByTestId('create-server-dialog')).toBeInTheDocument();
    expect(getByTestId('create-server-name-input')).toBeInTheDocument();
    expect(getByTestId('create-server-submit-button')).toHaveTextContent('Create');
  });

  it('disables submit when name is empty/whitespace', () => {
    const { getByTestId } = renderWithRouter(() => <CreateServerModal onClose={() => {}} />);
    const submit = getByTestId('create-server-submit-button') as HTMLButtonElement;
    expect(submit).toBeDisabled();
    fireEvent.input(getByTestId('create-server-name-input'), { target: { value: '   ' } });
    expect(submit).toBeDisabled();
  });

  it('enables submit once name is entered', () => {
    const { getByTestId } = renderWithRouter(() => <CreateServerModal onClose={() => {}} />);
    fireEvent.input(getByTestId('create-server-name-input'), { target: { value: 'My Server' } });
    expect(getByTestId('create-server-submit-button')).not.toBeDisabled();
  });

  it('calls onClose when Cancel is clicked', () => {
    const onClose = vi.fn();
    const { getByText } = renderWithRouter(() => <CreateServerModal onClose={onClose} />);
    fireEvent.click(getByText('Cancel'));
    expect(onClose).toHaveBeenCalled();
  });

  it('submits and closes on success', async () => {
    const onClose = vi.fn();
    mockFetch({
      'POST /api/v1/servers': () => ({ status: 200, body: { id: 's-1', name: 'My Server', ownerId: 'u-1' } }),
      'GET /api/v1/servers/s-1/channels': () => ({ status: 200, body: { channels: [{ id: 'c-1', name: 'general', capabilities: 'Chat' }], categories: [] } }),
    });
    const { getByTestId } = renderWithRouter(() => <CreateServerModal onClose={onClose} />);
    fireEvent.input(getByTestId('create-server-name-input'), { target: { value: 'My Server' } });
    fireEvent.click(getByTestId('create-server-submit-button'));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('shows error alert when create fails', async () => {
    mockFetch({
      'POST /api/v1/servers': () => ({ status: 400, body: { message: 'Server name taken' } }),
    });
    const { getByTestId, findByRole } = renderWithRouter(() => <CreateServerModal onClose={() => {}} />);
    fireEvent.input(getByTestId('create-server-name-input'), { target: { value: 'Dup' } });
    fireEvent.click(getByTestId('create-server-submit-button'));
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Server name taken|Failed to create server/);
  });
});
