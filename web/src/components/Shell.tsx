import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface NavItem {
  to: string;
  label: string;
  icon: string;
  end?: boolean;
}
interface NavGroup {
  title?: string;
  items: NavItem[];
}

/**
 * ترتیب/گروه‌بندی هم‌راستا با سایدبارِ دسکتاپ (MainShellWindow.xaml): اصلی → حسابداری →
 * خزانه → انبارداری → فروش → خرید → اشخاص → تنظیمات. «ماژول‌ها» یک گروهِ جدا و صریح است —
 * POS بخشِ هسته نیست (طبقِ CLAUDE.md: هسته=حسابداری/خزانه/انبار/فروش/خرید/اشخاص، POS ماژولِ
 * اختیاری است) پس در navbar کنارِ «مدیریتِ ماژول‌ها» جدا از هسته دیده می‌شود.
 */
const NAV_GROUPS: NavGroup[] = [
  { items: [{ to: '/', label: 'داشبورد', icon: '📊', end: true }] },
  { title: 'حسابداری', items: [
    { to: '/vouchers', label: 'اسنادِ حسابداری', icon: '📋' },
    { to: '/trial-balance', label: 'تراز آزمایشی', icon: '🏛' },
    { to: '/general-ledger', label: 'دفترِ کل/معین', icon: '🏛' },
  ] },
  { title: 'خزانه', items: [
    { to: '/treasury', label: 'دریافتنی/پرداختنی', icon: '🏦' },
    { to: '/cheques', label: 'تابلویِ چک', icon: '📝' },
  ] },
  { title: 'انبارداری', items: [
    { to: '/products', label: 'کالاها', icon: '📦' },
    { to: '/warehouse', label: 'انبار', icon: '🏭' },
  ] },
  { title: 'فروش', items: [
    { to: '/sales', label: 'فاکتورهایِ فروش', icon: '🧾' },
  ] },
  { title: 'خرید', items: [
    { to: '/purchase', label: 'فاکتورهایِ خرید', icon: '🛒' },
  ] },
  { title: 'اشخاص', items: [
    { to: '/customers', label: 'مشتریان', icon: '👥' },
    { to: '/suppliers', label: 'تأمین‌کنندگان', icon: '🏪' },
  ] },
  { title: 'ماژول‌ها', items: [
    { to: '/pos', label: 'صندوقِ فروش (POS)', icon: '🖨' },
    { to: '/modules', label: 'مدیریتِ ماژول‌ها', icon: '🧩' },
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
                  className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    padding: '9px 12px',
                    borderRadius: 'var(--radius-sm)',
                    color: 'var(--text-on-brand)',
                    fontSize: 'var(--text-sm)',
                  }}
                >
                  <span aria-hidden="true">{item.icon}</span>
                  <span>{item.label}</span>
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
