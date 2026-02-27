/** User record as returned by admin user list endpoint */
export interface AdminUser {
  id: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  isBot: boolean;
  isAdmin: boolean;
  isDisabled: boolean;
  emailConfirmed: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

/** Paginated user list response */
export interface AdminUsersResponse {
  users: AdminUser[];
  totalCount: number;
}
