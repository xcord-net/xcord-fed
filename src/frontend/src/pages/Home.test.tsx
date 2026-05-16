import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import Home from './Home';

describe('Home', () => {
  it('renders the placeholder text', () => {
    const { getByTestId } = render(() => <Home />);
    expect(getByTestId('home-placeholder')).toHaveTextContent('Xcord client - channels coming soon');
  });

  it('renders without crashing', () => {
    const { getByTestId } = render(() => <Home />);
    expect(getByTestId('home-page')).toBeInTheDocument();
  });

  it('renders the placeholder paragraph', () => {
    const { getByTestId } = render(() => <Home />);
    expect(getByTestId('home-placeholder')).toBeInTheDocument();
  });
});
