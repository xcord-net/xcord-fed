export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  displayName: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  authenticated: boolean;
  userId: string;
  username?: string;
  emailConfirmed?: boolean;
  confirmationCode?: string;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string;
}

export interface UserInfo {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  emailConfirmed: boolean;
  twoFactorEnabled: boolean;
  isAdmin: boolean;
  isBot: boolean;
}

export interface User {
  id: string;
  username: string;
  email: string;
  avatarUrl?: string;
  isAdmin?: boolean;
}
