import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import AutoUpdateToggle from './AutoUpdateToggle';

describe('AutoUpdateToggle', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => (
      <AutoUpdateToggle
        enabled={false}
        hubConnected={true}
        isToggling={false}
        toggleError={null}
        onToggle={vi.fn()}
      />
    ));
    expect(getByTestId('updates-auto-toggle-section')).toBeInTheDocument();
  });

  it('renders the Automatic Updates label and toggle button', () => {
    const { getByTestId, container } = render(() => (
      <AutoUpdateToggle
        enabled={true}
        hubConnected={true}
        isToggling={false}
        toggleError={null}
        onToggle={vi.fn()}
      />
    ));
    expect(container.textContent).toContain('Automatic Updates');
    const btn = getByTestId('updates-auto-toggle-button');
    expect(btn).toHaveAttribute('aria-checked', 'true');
  });

  it('reflects aria-checked=false when disabled', () => {
    const { getByTestId } = render(() => (
      <AutoUpdateToggle
        enabled={false}
        hubConnected={true}
        isToggling={false}
        toggleError={null}
        onToggle={vi.fn()}
      />
    ));
    expect(getByTestId('updates-auto-toggle-button')).toHaveAttribute('aria-checked', 'false');
  });

  it('invokes onToggle when the button is clicked', () => {
    const onToggle = vi.fn();
    const { getByTestId } = render(() => (
      <AutoUpdateToggle
        enabled={false}
        hubConnected={true}
        isToggling={false}
        toggleError={null}
        onToggle={onToggle}
      />
    ));
    fireEvent.click(getByTestId('updates-auto-toggle-button'));
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('disables the button when hub is disconnected or toggling', () => {
    const { getByTestId } = render(() => (
      <AutoUpdateToggle
        enabled={false}
        hubConnected={false}
        isToggling={false}
        toggleError={null}
        onToggle={vi.fn()}
      />
    ));
    expect(getByTestId('updates-auto-toggle-button')).toBeDisabled();
  });

  it('renders an error banner when toggleError is provided', () => {
    const { getByTestId } = render(() => (
      <AutoUpdateToggle
        enabled={false}
        hubConnected={true}
        isToggling={false}
        toggleError="Toggle failed"
        onToggle={vi.fn()}
      />
    ));
    const err = getByTestId('updates-toggle-error');
    expect(err).toHaveTextContent('Toggle failed');
  });
});
