import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ErpIcon, type IconName } from './ErpIcons';
import { CommandPalette, type PaletteItem } from './CommandPalette';
import { apiGet } from '../api/client';
import { todayJalaliString } from '../lib/jalali';
import '../erp-shell.css';

interface ChequeBoardRow { dueState: 'Overdue' | 'DueToday' | 'Upcoming' }

interface NavItem {
  to: string;
  label: string;
  icon: IconName;
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
 * آیکون‌ها از design-system/screens/erp-shell.js (ErpIcon) — همان مجموعه‌یِ خطیِ رسمی.
 */
const NAV_GROUPS: NavGroup[] = [
  { items: [{ to: '/', label: 'داشبورد', icon: 'home', end: true }] },
  { title: 'حسابداری', items: [
    { to: '/vouchers', label: 'اسنادِ حسابداری', icon: 'accounting' },
    { to: '/trial-balance', label: 'تراز آزمایشی', icon: 'reports' },
    { to: '/general-ledger', label: 'دفترِ کل/معین', icon: 'reports' },
  ] },
  { title: 'خزانه', items: [
    { to: '/treasury', label: 'دریافتنی/پرداختنی', icon: 'treasury' },
    { to: '/cheques', label: 'تابلویِ چک', icon: 'cheque' },
  ] },
  { title: 'انبارداری', items: [
    { to: '/products', label: 'کالاها', icon: 'inventory' },
    { to: '/warehouse', label: 'انبار', icon: 'inventory' },
  ] },
  { title: 'فروش', items: [
    { to: '/sales', label: 'فاکتورهایِ فروش', icon: 'sales' },
  ] },
  { title: 'خرید', items: [
    { to: '/purchase', label: 'فاکتورهایِ خرید', icon: 'purchasing' },
  ] },
  { title: 'اشخاص', items: [
    { to: '/customers', label: 'مشتریان', icon: 'people' },
    { to: '/suppliers', label: 'تأمین‌کنندگان', icon: 'people' },
  ] },
  { title: 'ماژول‌ها', items: [
    { to: '/pos', label: 'صندوقِ فروش (POS)', icon: 'pos' },
    { to: '/modules', label: 'مدیریتِ ماژول‌ها', icon: 'modules' },
  ] },
];

const FLAT_ITEMS = NAV_GROUPS.flatMap((g) => g.items);
const PALETTE_ITEMS: PaletteItem[] = NAV_GROUPS.flatMap((g) =>
  g.items.map((i) => ({ label: i.label, sub: g.title ?? 'اصلی', icon: i.icon, to: i.to })),
);

/** عنوانِ صفحهٔ جاری — هم‌الگو با `CurrentPageTitle` در MainShellWindowِ دسکتاپ (بایندِ توپ‌بار). */
function currentPageTitle(pathname: string): string {
  const exact = FLAT_ITEMS.find((i) => i.to === pathname);
  if (exact) return exact.label;
  const prefix = FLAT_ITEMS.filter((i) => i.to !== '/' && pathname.startsWith(i.to)).sort((a, b) => b.to.length - a.to.length)[0];
  return prefix?.label ?? 'سما حساب';
}

const TOP_MENUS = ['حسابداری', 'خزانه', 'انبارداری', 'فروش', 'خرید', 'اشخاص', 'ماژول‌ها'];

export function Shell() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const initial = (user?.fullName || 'S').trim().charAt(0);
  const [pendingCheques, setPendingCheques] = useState(0);

  useEffect(() => {
    document.title = `${currentPageTitle(location.pathname)} — سما حساب`;
  }, [location.pathname]);

  useEffect(() => {
    apiGet<ChequeBoardRow[]>(`/api/cheques/board?today=${encodeURIComponent(todayJalaliString())}`)
      .then((rows) => setPendingCheques(rows.filter((r) => r.dueState !== 'Upcoming').length))
      .catch(() => {});
  }, []);

  const activeGroup = NAV_GROUPS.find((g) => g.items.some((i) => location.pathname.startsWith(i.to) && i.to !== '/'))?.title;

  return (
    <div className="erp" style={{ height: '100%' }}>
      {/* ── توپ‌بار — برند/منوی افقی/جست‌وجو/اعلان/کاربر، هم‌الگو با design-system/screens/erp.css ── */}
      <header className="erp-top">
        <div className="brand">
          <div className="mark">س</div>
          <div>
            <div className="nm">سما حساب</div>
            <div className="ver">نسخهٔ ۲٫۹</div>
          </div>
        </div>
        <nav className="erp-menu">
          {TOP_MENUS.map((m) => (
            <button key={m} type="button" className={m === activeGroup ? 'active' : ''}>{m}</button>
          ))}
        </nav>
        <div className="search">
          <ErpIcon name="search" />
          <input placeholder="جستجو در همه‌جا…   Ctrl+K" readOnly style={{ cursor: 'pointer' }}
            onClick={() => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }))} />
        </div>
        <button type="button" className="tb-ic" title="اعلان‌ها">
          <ErpIcon name="bell" />
          {pendingCheques > 0 && <span className="ping" />}
        </button>
        <button type="button" className="user" onClick={logout} title="خروج">
          <span className="av">{initial}</span>
          <div>
            <div className="un">{user?.fullName}</div>
            <div className="ur">خروج</div>
          </div>
        </button>
      </header>

      <div className="erp-body">
        {/* ── سایدبار — رَدیفِ آیکونِ جمع‌شده (۶۰px)، با هاور تا ۲۲۰px بازمی‌شود ── */}
        <aside className="erp-side">
          <nav className="nav">
            {NAV_GROUPS.map((group, gi) => (
              <div key={gi}>
                {group.title && <div className="grouplabel">{group.title}</div>}
                {group.items.map((item) => (
                  <NavLink key={item.to} to={item.to} end={item.end} title={item.label}
                    className={({ isActive }) => `ni${isActive ? ' active' : ''}`}>
                    <ErpIcon name={item.icon} />
                    <span className="lb">{item.label}</span>
                  </NavLink>
                ))}
                {gi === 0 && <div className="sep" />}
              </div>
            ))}
          </nav>
        </aside>

        <div className="erp-main">
          <main style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: 'var(--space-6)', background: 'var(--bg-app)' }}>
            <Outlet />
          </main>

          {/* ── نوارِ وضعیت — کاربر/سالِ مالی/تاریخ، هم‌الگو با erp-status ── */}
          <footer className="erp-status">
            <div className="si"><span className="ok-dot" /><span>متصل</span></div>
            <div className="si">کاربر: <b>{user?.fullName}</b></div>
            <div className="grow" />
            <div className="si">{new Date().toLocaleDateString('fa-IR')}</div>
          </footer>
        </div>
      </div>

      <CommandPalette items={PALETTE_ITEMS} />
    </div>
  );
}
