import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import Home from './Home';

describe('Home', () => {
  it('renders the placeholder text', () => {
    const { getByText } = render(() => <Home />);
    expect(getByText('Xcord client - channels coming soon')).toBeInTheDocument();
  });

  it('renders without crashing', () => {
    const { container } = render(() => <Home />);
    expect(container.firstChild).not.toBeNull();
  });

  it('renders a paragraph element', () => {
    const { container } = render(() => <Home />);
    expect(container.querySelector('p')).not.toBeNull();
  });
});
