import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { apiGet, apiPost, clearTokens, getAccessToken, setTokens } from '../api/client';

export interface AuthUser {
  userId: number;
  companyId: number;
  branchId: number;
  username: string;
  fullName: string;
  roles: string[];
}

interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isReady: boolean;
  login: (username: string, password: string, companyId?: number, branchId?: number) => Promise<void>;
  logout: () => void;
  error: string | null;
}

const USER_KEY = 'sh_user';

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  });
  const [isReady, setIsReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // اگر توکن نداریم ولی کاربر در حافظه مانده (ناسازگاری) پاک شود.
    if (!getAccessToken() && user) {
      setUser(null);
      localStorage.removeItem(USER_KEY);
    }
    setIsReady(true);
  }, []);

  useEffect(() => {
    const onUnauthorized = () => {
      setUser(null);
      localStorage.removeItem(USER_KEY);
    };
    window.addEventListener('sh:unauthorized', onUnauthorized);
    return () => window.removeEventListener('sh:unauthorized', onUnauthorized);
  }, []);

  const login = useCallback(async (username: string, password: string, companyId = 1, branchId = 1) => {
    setError(null);
    try {
      const pair = await apiPost<TokenPair>('/api/auth/login', { username, password, companyId, branchId });
      setTokens(pair.accessToken, pair.refreshToken);
      const me = await apiGet<{
        userId: number;
        companyId: number;
        branchId: number;
        username: string;
        fullName: string;
        roles: string[];
      }>('/api/auth/me');
      const authUser: AuthUser = {
        userId: me.userId,
        companyId: me.companyId,
        branchId: me.branchId,
        username: me.username,
        fullName: me.fullName,
        roles: me.roles,
      };
      setUser(authUser);
      localStorage.setItem(USER_KEY, JSON.stringify(authUser));
    } catch (e) {
      const message = e instanceof Error ? e.message : 'ورود ناموفق بود.';
      setError(message);
      throw e;
    }
  }, []);

  const logout = useCallback(() => {
    clearTokens();
    localStorage.removeItem(USER_KEY);
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: !!user, isReady, login, logout, error }),
    [user, isReady, login, logout, error],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth باید درونِ AuthProvider استفاده شود.');
  return ctx;
}
