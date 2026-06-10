import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { request } from '../api/client';

interface DevTokenResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
}

interface AuthState {
  token: string | null;
  isAuthenticated: boolean;
  signInDev: () => Promise<void>;
  setToken: (token: string | null) => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

/**
 * Holds the bearer token in memory for the session. In development the token is obtained from
 * the API's dev-token endpoint; in production the app would integrate with Entra (MSAL).
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);

  const signInDev = useCallback(async () => {
    const result = await request<DevTokenResponse>('/api/dev/token', { method: 'POST' });
    setToken(result.accessToken);
  }, []);

  const value = useMemo<AuthState>(
    () => ({ token, isAuthenticated: token !== null, signInDev, setToken }),
    [token, signInDev],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }
  return ctx;
}
