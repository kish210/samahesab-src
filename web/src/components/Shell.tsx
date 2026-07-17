import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface NavItem {
  to: string;
  label: string;
  end?: boolean;
}
interface NavGroup {
  title?: string;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  { items: [{ to: '/', label: 'داشبورد', end: true }] },
  { title: 'اشخاص', items: [
    { to: '/customers', label: 'مشتریان' },
    { to: '/suppliers', label: 'تأمین‌کنندگان' },
  ] },
  { title: 'کالا/انبار', items: [
    { to: '/products', label: 'کالاها' },
    { to: '/warehouse', label: 'انبار' },
  ] },
  { title: 'فروش/خرید', items: [
    { to: '/sales', label: 'فاکتورهایِ فروش' },
    { to: '/purchase', label: 'فاکتورهایِ خرید' },
  ] },
  { title: 'خزانه', items: [
    { to: '/treasury', label: 'دریافتنی/پرداختنی' },
    { to: '/cheques', label: 'تابلویِ چک' },
  ] },
  { title: 'حسابداری', items: [
    { to: '/vouchers', label: 'اسنادِ حسابداری' },
    { to: '/trial-balance', label: 'تراز آزمایشی' },
    { to: '/general-ledger', label: 'دفترِ کل/معین' },
  ] },
  { title: 'مدیریت', items: [
    { to: '/modules', label: 'ماژول‌ها' },
  ] },
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
            overflowY: 'auto',
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-3)',
          }}
        >
          {NAV_GROUPS.map((group, gi) => (
            <div key={gi} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {group.title && (
                <div style={{ padding: '4px 12px', fontSize: 10, color: 'rgba(255,255,255,0.5)', fontWeight: 600 }}>{group.title}</div>
              )}
              {group.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  style={({ isActive }) => ({
                    display: 'block',
                    padding: '9px 12px',
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
            </div>
          ))}
        </aside>

        <main style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: 'var(--space-6)', background: 'var(--bg-app)' }}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
