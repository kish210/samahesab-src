import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

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

        {localError && (
          <div style={{ color: 'var(--danger-700)', background: 'var(--danger-50)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: 'var(--text-sm)' }}>
            {localError}
          </div>
        )}

        <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-2)' }}>
          {submitting ? 'در حالِ ورود…' : 'ورود'}
        </button>
      </form>
    </div>
  );
}
