import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import PresenceDot from './PresenceDot';
import { usePresence } from '../stores/presence.store';

describe('PresenceDot', () => {
  beforeEach(() => {
    usePresence().reset();
  });

  it('defaults to offline status when user has no presence', () => {
    const { getByLabelText } = render(() => <PresenceDot userId="user-1" />);
    expect(getByLabelText('Status: offline')).toBeInTheDocument();
  });

  it('reflects presence status from the store', () => {
    usePresence().updatePresence('user-1', 'online');
    const { getByLabelText } = render(() => <PresenceDot userId="user-1" />);
    expect(getByLabelText('Status: online')).toBeInTheDocument();
  });

  it('updates label when presence changes', () => {
    const { getByLabelText } = render(() => <PresenceDot userId="user-1" />);
    expect(getByLabelText('Status: offline')).toBeInTheDocument();
    usePresence().updatePresence('user-1', 'dnd');
    expect(getByLabelText('Status: dnd')).toBeInTheDocument();
  });

  it('isolates presence per userId', () => {
    usePresence().updatePresence('user-1', 'online');
    usePresence().updatePresence('user-2', 'idle');
    const { getByLabelText } = render(() => (
      <>
        <PresenceDot userId="user-1" />
        <PresenceDot userId="user-2" />
      </>
    ));
    expect(getByLabelText('Status: online')).toBeInTheDocument();
    expect(getByLabelText('Status: idle')).toBeInTheDocument();
  });
});
