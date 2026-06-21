import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import Toaster from './Toaster';
import { useToasts } from '../../stores/toast.store';

describe('Toaster', () => {
  beforeEach(() => useToasts().reset());

  it('renders a pushed toast with its message and kind-specific role', () => {
    const { getByText, getByTestId } = render(() => <Toaster />);
    useToasts().error('Upload failed');
    expect(getByText('Upload failed')).toBeInTheDocument();
    // Errors are announced assertively via role=alert.
    expect(getByTestId('toast-error').getAttribute('role')).toBe('alert');
  });

  it('uses role=status for non-error toasts', () => {
    const { getByTestId } = render(() => <Toaster />);
    useToasts().success('Saved');
    expect(getByTestId('toast-success').getAttribute('role')).toBe('status');
  });

  it('removes a toast when its dismiss button is clicked', () => {
    const { getByLabelText, queryByText } = render(() => <Toaster />);
    useToasts().info('Dismiss me');
    expect(queryByText('Dismiss me')).toBeInTheDocument();
    fireEvent.click(getByLabelText('Dismiss notification'));
    expect(queryByText('Dismiss me')).toBeNull();
  });
});
