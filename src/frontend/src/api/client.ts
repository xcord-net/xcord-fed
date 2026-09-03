class ApiClient {
  private baseUrl = '';
  private authenticated = false;
  private refreshPromise: Promise<boolean> | null = null;
  private onSessionExpired: () => void = () => { window.location.href = '/login'; };

  setBaseUrl(url: string) {
    this.baseUrl = url;
  }

  getBaseUrl(): string {
    return this.baseUrl;
  }

  setAuthenticated(value: boolean) {
    this.authenticated = value;
  }

  isAuthenticated(): boolean {
    return this.authenticated;
  }

  setOnSessionExpired(handler: () => void) {
    this.onSessionExpired = handler;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    // CSRF defense: custom header browsers will not send on cross-origin
    // form submissions. Required by the backend for cookie-authenticated
    // state-changing requests (POST/PUT/PATCH/DELETE).
    if (method !== 'GET' && method !== 'HEAD') {
      headers['X-Xcord-Request'] = '1';
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
      credentials: 'include',
      cache: 'no-store',
    });

    // Handle 401 - try refresh
    if (response.status === 401 && this.authenticated) {
      const refreshed = await this.tryRefresh();
      if (refreshed) {
        // Retry original request with new cookie
        const retryResponse = await fetch(`${this.baseUrl}${path}`, {
          method,
          headers,
          body: body ? JSON.stringify(body) : undefined,
          credentials: 'include',
        });

        if (retryResponse.status === 401) {
          // Double 401 - redirect to login
          this.authenticated = false;
          this.onSessionExpired();
          throw new Error('Session expired');
        }

        if (!retryResponse.ok) {
          const error = await retryResponse.json().catch(() => ({ error: 'Something went wrong on our end. Try again.' }));
          throw error;
        }

        return retryResponse.json() as Promise<T>;
      } else {
        // Refresh failed - redirect to login
        this.authenticated = false;
        window.location.href = '/login';
        throw new Error('Session expired');
      }
    }

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Something went wrong on our end. Try again.' }));
      throw error;
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return undefined as T;
    }

    return response.json() as Promise<T>;
  }

  private async tryRefresh(): Promise<boolean> {
    // If a refresh is already in progress, wait for it
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshPromise = (async () => {
      try {
        const response = await fetch(`${this.baseUrl}/api/v1/auth/refresh`, {
          method: 'POST',
          headers: { 'X-Xcord-Request': '1' },
          credentials: 'include',
        });

        if (!response.ok) return false;

        return true;
      } catch {
        return false;
      } finally {
        this.refreshPromise = null;
      }
    })();

    return this.refreshPromise;
  }

  async get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  async put<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  async patch<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PATCH', path, body);
  }

  async delete<T>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }
}

export const api = new ApiClient();
