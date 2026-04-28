import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import GroupList from './GroupList';

const sampleGroup = {
  id: 'g-1',
  serverId: 's-1',
  name: 'Admins',
  color: '#ed4245',
  roles: 0,
  position: 1,
  isHoisted: false,
  isMentionable: true,
};

describe('GroupList', () => {
  it('renders the sidebar container without crashing', () => {
    const { getByTestId } = render(() => (
      <GroupList groups={[]} isLoading={false} selectedGroupId={null} onSelect={vi.fn()} />
    ));
    expect(getByTestId('group-list-sidebar')).toBeInTheDocument();
  });

  it('shows loading state when isLoading is true', () => {
    const { container } = render(() => (
      <GroupList groups={[]} isLoading={true} selectedGroupId={null} onSelect={vi.fn()} />
    ));
    expect(container.textContent).toContain('Loading groups...');
  });

  it('shows empty state when no groups and not loading', () => {
    const { container } = render(() => (
      <GroupList groups={[]} isLoading={false} selectedGroupId={null} onSelect={vi.fn()} />
    ));
    expect(container.textContent).toContain('No groups yet.');
  });

  it('renders group items by name', () => {
    const { getByTestId } = render(() => (
      <GroupList groups={[sampleGroup]} isLoading={false} selectedGroupId={null} onSelect={vi.fn()} />
    ));
    expect(getByTestId('group-item-g-1')).toHaveTextContent('Admins');
  });

  it('invokes onSelect with the group when item is clicked', () => {
    const onSelect = vi.fn();
    const { getByTestId } = render(() => (
      <GroupList groups={[sampleGroup]} isLoading={false} selectedGroupId={null} onSelect={onSelect} />
    ));
    fireEvent.click(getByTestId('group-item-g-1'));
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(sampleGroup);
  });

  it('marks the selected group as pressed', () => {
    const { getByTestId } = render(() => (
      <GroupList groups={[sampleGroup]} isLoading={false} selectedGroupId="g-1" onSelect={vi.fn()} />
    ));
    expect(getByTestId('group-item-g-1')).toHaveAttribute('aria-pressed', 'true');
  });
});
