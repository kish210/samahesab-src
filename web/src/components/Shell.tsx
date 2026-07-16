import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

const NAV_ITEMS = [
  { to: '/', label: 'داشبورد', end: true },
  { to: '/customers', label: 'مشتریان' },
];

export function Shell() {
  const { user, logout } = useAuth();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <header
        style={{
          height: 'var(--topbar-h)',
          background: 'var(--blue-700)',
          color: 'var(--text-on-brand)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 var(--space-5)',
          flex: 'none',
        }}
      >
        <div style={{ fontWeight: 700 }}>سما حساب</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)', fontSize: 'var(--text-sm)' }}>
          <span>{user?.fullName}</span>
          <button className="btn btn-ghost btn-sm" style={{ color: 'var(--text-on-brand)' }} onClick={logout}>
            خروج
          </button>
        </div>
      </header>

      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        <aside
          style={{
            width: 'var(--sidebar-w)',
            flex: 'none',
            background: 'var(--bg-sidebar)',
            padding: 'var(--space-4) var(--space-3)',
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-1)',
          }}
        >
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              style={({ isActive }) => ({
                display: 'block',
                padding: '10px 12px',
                borderRadius: 'var(--radius-sm)',
                color: 'var(--text-on-brand)',
                fontSize: 'var(--text-sm)',
                fontWeight: isActive ? 600 : 400,
                background: isActive ? 'var(--bg-sidebar-hover)' : 'transparent',
              })}
            >
              {item.label}
            </NavLink>
          ))}
        </aside>

        <main style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: 'var(--space-6)', background: 'var(--bg-app)' }}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
