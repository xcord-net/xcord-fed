import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { Inbox } from 'lucide-solid';
import EmptyState from './EmptyState';

describe('EmptyState', () => {
  it('states what is not there', () => {
    const { getByTestId } = render(() => <EmptyState title="No posts yet" />);
    expect(getByTestId('empty-state-title').textContent).toBe('No posts yet');
  });

  it('omits the second line when there is nothing more to say', () => {
    const { queryByTestId } = render(() => <EmptyState title="No posts yet" />);
    expect(queryByTestId('empty-state-body')).toBeNull();
  });

  it('explains when given a body', () => {
    const { getByTestId } = render(() => (
      <EmptyState title="No posts yet" body="Start a discussion and it shows up here." />
    ));
    expect(getByTestId('empty-state-body').textContent).toBe(
      'Start a discussion and it shows up here.',
    );
  });

  it('offers the way out', () => {
    const onClick = vi.fn();
    const { getByTestId } = render(() => (
      <EmptyState title="No posts yet" action={{ label: 'Write the first', onClick }} />
    ));
    const button = getByTestId('empty-state-action');
    expect(button.textContent).toBe('Write the first');
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('has no action to press when the surface offers none', () => {
    const { queryByTestId } = render(() => <EmptyState title="No posts yet" />);
    expect(queryByTestId('empty-state-action')).toBeNull();
  });

  // The glyph repeats the sentence beside it, so it stays out of the a11y tree.
  it('keeps the glyph decorative', () => {
    const { container } = render(() => <EmptyState icon={Inbox} title="No posts yet" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
    expect(svg?.getAttribute('stroke-width')).toBe('1.5');
  });

  it('draws a smaller glyph when dense', () => {
    const full = render(() => <EmptyState icon={Inbox} title="x" />);
    expect(full.container.querySelector('svg')?.getAttribute('width')).toBe('28');
    full.unmount();

    const dense = render(() => <EmptyState icon={Inbox} title="x" dense />);
    expect(dense.container.querySelector('svg')?.getAttribute('width')).toBe('20');
  });

  it('carries a test id through so callers can target their own surface', () => {
    const { getByTestId } = render(() => (
      <EmptyState title="No posts yet" data-testid="forum-empty" />
    ));
    expect(getByTestId('forum-empty')).toBeInTheDocument();
  });
});
