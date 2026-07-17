// در نسخهٔ نصب‌شده، خودِ API این کلاینت را زیرِ /web/ سرو می‌کند ⇒ هم‌مبدأ است و BASE_URL باید
// خالی بماند (آدرسِ نسبیِ /api/... ). `.env.production` همین را خالی می‌گذارد. در حالتِ dev
// (سرورِ Vite رویِ ۵۱۷۳، مبدأِ متفاوت) `.env` آدرسِ کاملِ ۵۰۸۰ را می‌دهد و CORSِ سرور اجازه می‌دهد.
const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

const TOKEN_KEY = 'sh_access_token';
const REFRESH_KEY = 'sh_refresh_token';

export function getAccessToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem(TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_KEY, refreshToken);
}

export function clearTokens() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY);
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

/** یک‌بار تلاش برای تمدیدِ توکن با ۴۰۱ — اگر شکست خورد، پیام‌رسانِ خروجِ اجباری (رویدادِ سراسری). */
async function tryRefresh(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;
  const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) return false;
  const data = await res.json();
  setTokens(data.accessToken, data.refreshToken);
  return true;
}

/** فراخوانِ خامِ API — JSON درخواست/پاسخ، هدرِ Authorization خودکار، یک بارِ retry بعدِ تمدیدِ توکن روی ۴۰۱. */
export async function apiFetch<T>(path: string, options: RequestInit = {}, _retried = false): Promise<T> {
  const token = getAccessToken();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  };

  const res = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (res.status === 401 && !_retried) {
    const refreshed = await tryRefresh();
    if (refreshed) return apiFetch<T>(path, options, true);
    clearTokens();
    window.dispatchEvent(new CustomEvent('sh:unauthorized'));
    throw new ApiError(401, 'نشستِ شما منقضی شده؛ دوباره وارد شوید.');
  }

  if (!res.ok) {
    let message = `خطا در ارتباط با سرور (${res.status})`;
    try {
      const body = await res.json();
      message = body?.message ?? message;
    } catch {
      /* بدنهٔ غیرِ JSON — پیامِ پیش‌فرض کافی است */
    }
    throw new ApiError(res.status, message);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export function apiGet<T>(path: string) {
  return apiFetch<T>(path, { method: 'GET' });
}

export function apiPost<T>(path: string, body?: unknown) {
  return apiFetch<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined });
}

export function apiDelete<T>(path: string) {
  return apiFetch<T>(path, { method: 'DELETE' });
}

/** آپلودِ فایل (multipart) — بدونِ Content-Type دستی تا مرورگر boundaryِ درست بگذارد.
 * هدرِ Authorization خودکار + مدیریتِ خطا مثلِ apiFetch (بدون retryِ تمدید برایِ سادگی). */
export async function apiUpload<T>(path: string, file: File, fieldName = 'file'): Promise<T> {
  const token = getAccessToken();
  const form = new FormData();
  form.append(fieldName, file);

  const res = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    body: form,
  });

  if (res.status === 401) {
    clearTokens();
    window.dispatchEvent(new CustomEvent('sh:unauthorized'));
    throw new ApiError(401, 'نشستِ شما منقضی شده؛ دوباره وارد شوید.');
  }
  if (!res.ok) {
    let message = `خطا در آپلود (${res.status})`;
    try {
      const body = await res.json();
      message = body?.message ?? message;
    } catch {
      /* بدنهٔ غیرِ JSON */
    }
    throw new ApiError(res.status, message);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
