import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';

let mockAuth = { isLoading: false, isAuthenticated: true };

vi.mock('../stores/auth.store', () => ({
  useAuth: () => mockAuth,
}));

import AuthGuard from './AuthGuard';

describe('AuthGuard', () => {
  beforeEach(() => {
    mockAuth = { isLoading: false, isAuthenticated: true };
  });

  it('renders children when authenticated and not loading', () => {
    const { getByText } = renderWithRouter(() => (
      <AuthGuard><span>protected</span></AuthGuard>
    ), { path: '/' });
    expect(getByText('protected')).toBeInTheDocument();
  });

  it('does not render children while auth is still loading', () => {
    mockAuth = { isLoading: true, isAuthenticated: false };
    const { queryByText } = renderWithRouter(() => (
      <AuthGuard><span>protected</span></AuthGuard>
    ), { path: '/' });
    expect(queryByText('protected')).toBeNull();
  });

  it('does not render children when unauthenticated', () => {
    mockAuth = { isLoading: false, isAuthenticated: false };
    const { queryByText } = renderWithRouter(() => (
      <AuthGuard><span>protected</span></AuthGuard>
    ), { path: '/' });
    expect(queryByText('protected')).toBeNull();
  });

  it('does not render children when both unauthenticated and loading', () => {
    mockAuth = { isLoading: true, isAuthenticated: true };
    const { queryByText } = renderWithRouter(() => (
      <AuthGuard><span>protected</span></AuthGuard>
    ), { path: '/' });
    expect(queryByText('protected')).toBeNull();
  });
});
