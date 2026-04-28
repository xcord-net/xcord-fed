import { describe, it, expect, vi } from 'vitest';
import { createSignal } from 'solid-js';
import { render, fireEvent } from '@solidjs/testing-library';
import Menu from './Menu';

describe('Menu', () => {
  it('does not render menu when closed', () => {
    const { queryByRole } = render(() => (
      <Menu open={false} onClose={() => {}}>
        <button role="menuitem">Item 1</button>
      </Menu>
    ));
    expect(queryByRole('menu')).toBeNull();
  });

  it('renders menu when open', () => {
    const { getByRole, getByText } = render(() => (
      <Menu open={true} onClose={() => {}} position={{ x: 10, y: 10 }}>
        <button role="menuitem">Item 1</button>
      </Menu>
    ));
    expect(getByRole('menu')).toBeInTheDocument();
    expect(getByText('Item 1')).toBeInTheDocument();
  });

  it('calls onClose when overlay is clicked', () => {
    const onClose = vi.fn();
    const { container } = render(() => (
      <Menu open={true} onClose={onClose} position={{ x: 0, y: 0 }}>
        <button role="menuitem">Item</button>
      </Menu>
    ));
    // Overlay is the first sibling of the menu - find it by aria-hidden.
    const overlay = container.querySelector('[aria-hidden="true"]')!;
    fireEvent.click(overlay);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose when Escape key is pressed', () => {
    const onClose = vi.fn();
    render(() => (
      <Menu open={true} onClose={onClose} position={{ x: 0, y: 0 }}>
        <button role="menuitem">Item</button>
      </Menu>
    ));
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('reacts to open prop changes', () => {
    const [open, setOpen] = createSignal(false);
    const { queryByRole } = render(() => (
      <Menu open={open()} onClose={() => {}} position={{ x: 0, y: 0 }}>
        <button role="menuitem">Item</button>
      </Menu>
    ));
    expect(queryByRole('menu')).toBeNull();
    setOpen(true);
    expect(queryByRole('menu')).not.toBeNull();
  });

  it('activates focused menu item via Enter key', () => {
    const onClick = vi.fn();
    const { getByText } = render(() => (
      <Menu open={true} onClose={() => {}} position={{ x: 0, y: 0 }}>
        <button role="menuitem" onClick={onClick}>Choose Me</button>
      </Menu>
    ));
    const item = getByText('Choose Me');
    item.focus();
    fireEvent.keyDown(document, { key: 'Enter' });
    expect(onClick).toHaveBeenCalledOnce();
  });
});
