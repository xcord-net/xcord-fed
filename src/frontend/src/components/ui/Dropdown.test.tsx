import { describe, it, expect, vi, afterEach } from 'vitest';
import { createSignal } from 'solid-js';
import { render, fireEvent } from '@solidjs/testing-library';
import Dropdown from './Dropdown';

const triggers: HTMLElement[] = [];
function setupTrigger() {
  const trigger = document.createElement('button');
  trigger.textContent = 'trigger';
  document.body.appendChild(trigger);
  triggers.push(trigger);
  return trigger;
}

afterEach(() => {
  while (triggers.length > 0) triggers.pop()!.remove();
});

describe('Dropdown', () => {
  it('does not render content when open is false', () => {
    const trigger = setupTrigger();
    const { queryByText } = render(() => (
      <Dropdown open={false} onClose={() => {}} trigger={trigger}>
        <div>panel content</div>
      </Dropdown>
    ));
    expect(queryByText('panel content')).toBeNull();
  });

  it('renders content when open is true', () => {
    const trigger = setupTrigger();
    const { getByText } = render(() => (
      <Dropdown open={true} onClose={() => {}} trigger={trigger}>
        <div>panel content</div>
      </Dropdown>
    ));
    expect(getByText('panel content')).toBeInTheDocument();
  });

  it('calls onClose when overlay is clicked', () => {
    const trigger = setupTrigger();
    const onClose = vi.fn();
    const { container } = render(() => (
      <Dropdown open={true} onClose={onClose} trigger={trigger}>
        <div>panel content</div>
      </Dropdown>
    ));
    const overlay = container.querySelector('[aria-hidden="true"]')!;
    fireEvent.click(overlay);
    expect(onClose).toHaveBeenCalled();
  });

  it('calls onClose when Escape is pressed', () => {
    const trigger = setupTrigger();
    const onClose = vi.fn();
    render(() => (
      <Dropdown open={true} onClose={onClose} trigger={trigger}>
        <div>panel content</div>
      </Dropdown>
    ));
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalled();
  });

  it('reacts to open prop changes', () => {
    const trigger = setupTrigger();
    const [open, setOpen] = createSignal(false);
    const { queryByText } = render(() => (
      <Dropdown open={open()} onClose={() => {}} trigger={trigger}>
        <div>panel content</div>
      </Dropdown>
    ));
    expect(queryByText('panel content')).toBeNull();
    setOpen(true);
    expect(queryByText('panel content')).not.toBeNull();
  });
});
