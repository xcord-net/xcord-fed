import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

const openCreateServer = vi.fn();
vi.mock('../../stores/modal.store', () => ({
  useModals: () => ({ openCreateServer }),
}));

import WelcomePanel from './WelcomePanel';

describe('WelcomePanel', () => {
  it('opens the create-server modal when the CTA is clicked', () => {
    const { getByTestId } = render(() => <WelcomePanel />);
    expect(getByTestId('home-welcome')).toBeInTheDocument();
    fireEvent.click(getByTestId('welcome-create-server'));
    expect(openCreateServer).toHaveBeenCalledTimes(1);
  });
});
