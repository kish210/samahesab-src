import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { apiGet, apiPost, clearTokens, getAccessToken, setTokens, tokenStorage } from '../api/client';

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

/** وضعیتِ نشست نسبت به «خروجِ خودکار پس از بی‌تحرکی» (Idle-timeout) — هم‌الگو با کنترلِ امنیتیِ
 * حسابفا: نشستِ بی‌کار در مرورگر نباید بی‌نهایت باز بماند. 'active' = فعال · 'warning' = هشدارِ
 * پیش از انقضا (کاربر می‌تواند با «ادامه‌ی کار» تمدید کند) · 'expired' = منقضی و خارج‌شده. */
export type IdleState = 'active' | 'warning' | 'expired';

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isReady: boolean;
  login: (username: string, password: string, companyId?: number, branchId?: number) => Promise<void>;
  logout: () => void;
  error: string | null;
  /** وضعیتِ فعلیِ بی‌تحرکیِ نشست (برای نمایشِ هشدار در Shell). */
  idleState: IdleState;
  /** تمدیدِ نشست — با کلیک روی «ادامه‌ی کار» در هشدارِ انقضا. */
  extendIdle: () => void;
}

const USER_KEY = 'sh_user';

// ── خروجِ خودکار پس از بی‌تحرکی — مقادیرِ پیش‌فرض (مثلِ حسابفا، نشستِ بی‌کار بسته می‌شود).
// ۳۰ دقیقه بی‌تحرکی = خروج؛ ۶۰ ثانیهٔ آخر هشدار نمایش داده می‌شود تا کاربر فرصتِ «ادامه» داشته
// باشد. مقدارِ زمان را می‌توان با کلیدِ localStorageِ `sh_idle_minutes` (دقیقه) override کرد.
const IDLE_TIMEOUT_MS = 30 * 60 * 1000;
const IDLE_WARN_MS = 60 * 1000;
const IDLE_TICK_MS = 5 * 1000;
const IDLE_MINUTES_KEY = 'sh_idle_minutes';

function idleTimeoutMs(): number {
  const raw = Number(localStorage.getItem(IDLE_MINUTES_KEY));
  if (Number.isFinite(raw) && raw >= 1) return raw * 60 * 1000;
  return IDLE_TIMEOUT_MS;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const raw = tokenStorage().getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  });
  const [isReady, setIsReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [idleState, setIdleState] = useState<IdleState>('active');
  const lastActivityRef = useRef(Date.now());

  useEffect(() => {
    // اگر توکن نداریم ولی کاربر در حافظه مانده (ناسازگاری) پاک شود.
    if (!getAccessToken() && user) {
      setUser(null);
      tokenStorage().removeItem(USER_KEY);
    }
    setIsReady(true);
  }, []);

  useEffect(() => {
    const onUnauthorized = () => {
      setUser(null);
      tokenStorage().removeItem(USER_KEY);
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
      tokenStorage().setItem(USER_KEY, JSON.stringify(authUser));
    } catch (e) {
      const message = e instanceof Error ? e.message : 'ورود ناموفق بود.';
      setError(message);
      throw e;
    }
  }, []);

  const logout = useCallback(() => {
    clearTokens();
    tokenStorage().removeItem(USER_KEY);
    setUser(null);
  }, []);

  // ── خروجِ خودکار نشستِ بی‌کار — رویدادهایِ فعالیت، زمانِ آخرینِ فعالیت را تازه می‌کنند؛
  // یک تایمرِ دوره‌ای ماندنِ نشست را می‌سنجد و در آستانهٔ هشدار/انقضا، وضعیت را عوض می‌کند.
  // فقط وقتی کاربر وارد است سنجیده می‌شود (نه در صفحهٔ ورود).
  const extendIdle = useCallback(() => {
    lastActivityRef.current = Date.now();
    setIdleState('active');
  }, []);

  useEffect(() => {
    if (!user) {
      setIdleState('active');
      return;
    }
    const bump = () => {
      lastActivityRef.current = Date.now();
      // هر فعالیتِ کاربر هشدارِ انقضا را می‌بندد (وضعیت به «فعال» برمی‌گردد).
      setIdleState('active');
    };
    // mousemove/scroll passive تا با اسکرولِ بلند نشستِ کاربر را به هم نزند.
    const events: Array<keyof WindowEventMap> = [
      'keydown', 'mousedown', 'mousemove', 'touchstart', 'click', 'scroll', 'wheel',
    ];
    events.forEach((e) => window.addEventListener(e, bump, { passive: true }));
    const timer = window.setInterval(() => {
      const elapsed = Date.now() - lastActivityRef.current;
      if (elapsed >= idleTimeoutMs()) {
        setIdleState('expired');
        logout();
      } else if (elapsed >= idleTimeoutMs() - IDLE_WARN_MS) {
        setIdleState('warning');
      } else {
        setIdleState((s) => (s === 'warning' ? 'active' : s));
      }
    }, IDLE_TICK_MS);
    return () => {
      events.forEach((e) => window.removeEventListener(e, bump));
      window.clearInterval(timer);
    };
  }, [user, logout]);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: !!user, isReady, login, logout, error, idleState, extendIdle }),
    [user, isReady, login, logout, error, idleState, extendIdle],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth باید درونِ AuthProvider استفاده شود.');
  return ctx;
}
