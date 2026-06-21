import { describe, it, expect } from 'vitest';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import NotFound from './NotFound';

describe('NotFound', () => {
  it('shows a 404 message and a way back home and to login', () => {
    const { getByText, getByRole } = renderWithRouter(() => <NotFound />, { path: '/totally-unknown' });
    expect(getByText('404')).toBeInTheDocument();
    expect(getByText('Page not found')).toBeInTheDocument();
    expect(getByRole('link', { name: 'Go home' }).getAttribute('href')).toBe('/');
    expect(getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
  });
});
