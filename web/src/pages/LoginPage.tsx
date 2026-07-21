import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { apiPost, ApiError } from '../api/client';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'recovery'>('login');

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [recoverySuccess, setRecoverySuccess] = useState<string | null>(null);

  const [recCode, setRecCode] = useState('');
  const [recNewPassword, setRecNewPassword] = useState('');
  const [recConfirm, setRecConfirm] = useState('');
  const [recError, setRecError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLocalError(null);
    setSubmitting(true);
    try {
      await login(username, password);
      navigate('/', { replace: true });
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : 'ورود ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  function openRecovery() {
    setMode('recovery');
    setLocalError(null);
    setRecoverySuccess(null);
    setRecError(null);
    setRecCode('');
    setRecNewPassword('');
    setRecConfirm('');
  }

  async function onRecoverySubmit(e: FormEvent) {
    e.preventDefault();
    setRecError(null);
    if (!username.trim()) { setRecError('نامِ کاربری را وارد کنید.'); return; }
    if (recNewPassword.length < 6) { setRecError('رمزِ نو دستِ‌کم ۶ نویسه باشد.'); return; }
    if (recNewPassword !== recConfirm) { setRecError('رمزِ نو و تکرارش یکسان نیستند.'); return; }
    setSubmitting(true);
    try {
      await apiPost('/api/auth/reset-password-recovery', {
        username: username.trim(),
        recoveryCode: recCode.replace(/-/g, '').trim(),
        newPassword: recNewPassword,
      });
      setMode('login');
      setPassword('');
      setRecoverySuccess('رمزِ عبور با موفقیت عوض شد — با رمزِ نو وارد شوید.');
    } catch (err) {
      setRecError(err instanceof ApiError ? err.message : 'بازنشانیِ رمز ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      style={{
        minHeight: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'var(--bg-app)',
        flex: 1,
      }}
    >
      {mode === 'login' ? (
        <form
          onSubmit={onSubmit}
          style={{
            width: 360,
            maxWidth: '92vw',
            background: 'var(--bg-surface)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-lg)',
            boxShadow: 'var(--shadow-md)',
            padding: 'var(--space-8)',
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-4)',
          }}
        >
          <div style={{ textAlign: 'center', marginBottom: 'var(--space-2)' }}>
            <h1 style={{ fontSize: 'var(--text-2xl)', color: 'var(--blue-700)' }}>سما حساب</h1>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>ورود به کلاینتِ وب</div>
          </div>

          <div className="field">
            <label className="label" htmlFor="username">
              نامِ کاربری
            </label>
            <input
              id="username"
              className="input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
              autoComplete="username"
            />
          </div>

          <div className="field">
            <label className="label" htmlFor="password">
              رمزِ عبور
            </label>
            <input
              id="password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
          </div>

          {recoverySuccess && (
            <div style={{ color: 'var(--success-700)', background: 'var(--success-50)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: 'var(--text-sm)' }}>
              {recoverySuccess}
            </div>
          )}
          {localError && (
            <div style={{ color: 'var(--danger-700)', background: 'var(--danger-50)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: 'var(--text-sm)' }}>
              {localError}
            </div>
          )}

          <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-2)' }}>
            {submitting ? 'در حالِ ورود…' : 'ورود'}
          </button>

          <button type="button" onClick={openRecovery}
            style={{ background: 'transparent', border: 'none', color: 'var(--text-link)', fontSize: 'var(--text-sm)', cursor: 'pointer', padding: 0 }}>
            رمزِ عبور را فراموش کرده‌اید؟
          </button>
        </form>
      ) : (
        <form
          onSubmit={onRecoverySubmit}
          style={{
            width: 380,
            maxWidth: '92vw',
            background: 'var(--bg-surface)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-lg)',
            boxShadow: 'var(--shadow-md)',
            padding: 'var(--space-8)',
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-4)',
          }}
        >
          <div style={{ textAlign: 'center', marginBottom: 'var(--space-2)' }}>
            <h1 style={{ fontSize: 'var(--text-xl)', color: 'var(--blue-700)' }}>بازیابیِ رمزِ عبور</h1>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
              کدِ بازیابی‌ای که از «تنظیمات ← دربارهٔ سیستم» ساخته و یادداشت کرده بودید را وارد کنید.
            </div>
          </div>

          <div className="field">
            <label className="label" htmlFor="rec-username">نامِ کاربری</label>
            <input id="rec-username" className="input" value={username} onChange={(e) => setUsername(e.target.value)} autoFocus />
          </div>

          <div className="field">
            <label className="label" htmlFor="rec-code">کدِ بازیابی</label>
            <input id="rec-code" className="input" style={{ direction: 'ltr' }} value={recCode} onChange={(e) => setRecCode(e.target.value)} />
          </div>

          <div className="field">
            <label className="label" htmlFor="rec-newpw">رمزِ نو</label>
            <input id="rec-newpw" type="password" className="input" value={recNewPassword} onChange={(e) => setRecNewPassword(e.target.value)} />
          </div>

          <div className="field">
            <label className="label" htmlFor="rec-confirm">تکرارِ رمزِ نو</label>
            <input id="rec-confirm" type="password" className="input" value={recConfirm} onChange={(e) => setRecConfirm(e.target.value)} />
          </div>

          {recError && (
            <div style={{ color: 'var(--danger-700)', background: 'var(--danger-50)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: 'var(--text-sm)' }}>
              {recError}
            </div>
          )}

          <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-2)' }}>
            {submitting ? 'در حالِ بازنشانی…' : 'تغییرِ رمزِ عبور'}
          </button>

          <button type="button" onClick={() => setMode('login')}
            style={{ background: 'transparent', border: 'none', color: 'var(--text-link)', fontSize: 'var(--text-sm)', cursor: 'pointer', padding: 0 }}>
            بازگشت به ورود
          </button>
        </form>
      )}
    </div>
  );
}
