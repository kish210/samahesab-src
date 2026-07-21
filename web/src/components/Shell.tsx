import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ErpIcon, type IconName } from './ErpIcons';
import { CommandPalette, type PaletteItem } from './CommandPalette';
import { apiGet } from '../api/client';
import { todayJalaliString } from '../lib/jalali';
import '../erp-shell.css';

interface ChequeBoardRow { dueState: 'Overdue' | 'DueToday' | 'Upcoming' }
interface LicenseStatus { isExpired: boolean; daysRemaining: number | null; expiresUtc: string | null }

interface NavItem {
  to: string;
  label: string;
  icon: IconName;
  end?: boolean;
  /** کلیدِ ماژول (از `IModule.Key`) — اگر تعریف شود، این آیتم فقط وقتی نشان داده می‌شود که
   * ماژول واقعاً روی سرور بارگذاری‌شده باشد (نه هر ماژولِ اختیاری را کورکورانه). */
  moduleKey?: string;
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
    { to: '/accounts', label: 'دفترِ حساب‌ها', icon: 'accounting' },
  ] },
  { title: 'گزارش‌هایِ مالی', items: [
    { to: '/trial-balance', label: 'تراز آزمایشی', icon: 'reports' },
    { to: '/general-ledger', label: 'دفترِ کل/معین', icon: 'reports' },
    { to: '/balance-sheet', label: 'ترازنامه', icon: 'reports' },
    { to: '/income-statement', label: 'صورتِ سودوزیان', icon: 'reports' },
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
  // هر ماژولِ اختیاری زیرمنویِ جداگانهٔ خودش را دارد (نه یک «ماژول‌ها»یِ مخلوط) — هر کدام
  // فقط وقتی دیده می‌شود که moduleKeyاش واقعاً روی سرور بارگذاری‌شده باشد.
  { title: 'POS', items: [
    { to: '/pos', label: 'صندوقِ فروش', icon: 'pos', moduleKey: 'POS' },
  ] },
  { title: 'رستوران', items: [
    { to: '/restaurant', label: 'میزها و سالن‌ها', icon: 'restaurant', moduleKey: 'Restaurant' },
    { to: '/restaurant/kitchen', label: 'تابلویِ آشپزخانه', icon: 'restaurant', moduleKey: 'Restaurant' },
  ] },
  { title: 'سیستم', items: [
    { to: '/modules', label: 'مدیریتِ ماژول‌ها', icon: 'modules' },
  ] },
];

const FLAT_ITEMS = NAV_GROUPS.flatMap((g) => g.items);

/** منویِ افقیِ توپ‌بار — عمداً به تعدادِ ثابتِ کوچکی از دسته‌بندی محدود است (نه تکرارِ ۱به۱ِ هرزیرمنویِ
 * سایدبار) تا شلوغ نشود؛ هر دکمه به اولین گروهِ سایدبارِ مرتبط با آن ناوبری می‌کند. */
const TOP_MENU_MAP: [string, string[]][] = [
  ['حسابداری', ['حسابداری']],
  ['خزانه‌داری', ['خزانه']],
  ['خرید', ['خرید']],
  ['فروش', ['فروش', 'POS', 'رستوران']],
  ['انبار', ['انبارداری']],
  ['اشخاص', ['اشخاص']],
  ['گزارشات', ['گزارش‌هایِ مالی']],
  ['سیستم', ['سیستم']],
];

/** عنوانِ صفحهٔ جاری — هم‌الگو با `CurrentPageTitle` در MainShellWindowِ دسکتاپ (بایندِ توپ‌بار). */
function currentPageTitle(pathname: string): string {
  const exact = FLAT_ITEMS.find((i) => i.to === pathname);
  if (exact) return exact.label;
  const prefix = FLAT_ITEMS.filter((i) => i.to !== '/' && pathname.startsWith(i.to)).sort((a, b) => b.to.length - a.to.length)[0];
  return prefix?.label ?? 'سما حساب';
}

export function Shell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const initial = (user?.fullName || 'S').trim().charAt(0);
  const [pendingCheques, setPendingCheques] = useState(0);
  const [loadedModules, setLoadedModules] = useState<string[] | null>(null);
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [licenseBannerDismissed, setLicenseBannerDismissed] = useState(false);
  // آکاردئون — همیشه فقط گروهِ صفحهٔ جاری باز است تا سایدبار شلوغ نشود (نه هرچند گروهِ هم‌زمان‌بازِ قبلی).
  const [openGroup, setOpenGroup] = useState<string | undefined>();

  useEffect(() => {
    document.title = `${currentPageTitle(location.pathname)} — سما حساب`;
  }, [location.pathname]);

  useEffect(() => {
    apiGet<ChequeBoardRow[]>(`/api/cheques/board?today=${encodeURIComponent(todayJalaliString())}`)
      .then((rows) => setPendingCheques(rows.filter((r) => r.dueState !== 'Upcoming').length))
      .catch(() => {});
  }, []);

  // navbar فقط ماژول‌هایِ واقعاً بارگذاری‌شده روی این سرور را نشان بدهد (نه هر ماژولِ اختیاری را
  // کورکورانه) — طبقِ قاعدهٔ CLAUDE.md: هسته نباید لینکِ مرده به ماژولِ نصب‌نشده نشان بدهد.
  useEffect(() => {
    apiGet<string[]>('/api/module-capabilities').then(setLoadedModules).catch(() => setLoadedModules([]));
  }, []);

  // بنرِ اطلاع‌رسانیِ «یک‌سالِ رایگان» — فقط وقتی نزدیکِ اتمام یا تمام‌شده باشد نمایش می‌یابد؛
  // اطلاع‌رسانی است، نه قفلِ فنی (نگاه کن به ServerLicenseStatusProvider.cs).
  useEffect(() => {
    apiGet<LicenseStatus>('/api/license/status').then(setLicense).catch(() => {});
  }, []);
  const showLicenseBanner = !licenseBannerDismissed && license && (license.isExpired || (license.daysRemaining !== null && license.daysRemaining <= 30));

  const visibleNavGroups: NavGroup[] = NAV_GROUPS
    .map((g) => ({ ...g, items: g.items.filter((i) => !i.moduleKey || loadedModules === null || loadedModules.includes(i.moduleKey)) }))
    .filter((g) => g.items.length > 0);
  const palette: PaletteItem[] = visibleNavGroups.flatMap((g) =>
    g.items.map((i) => ({ label: i.label, sub: g.title ?? 'اصلی', icon: i.icon, to: i.to })),
  );

  const activeGroup = NAV_GROUPS.find((g) => g.items.some((i) => location.pathname.startsWith(i.to) && i.to !== '/'))?.title;
  const activeTopMenu = TOP_MENU_MAP.find(([, titles]) => titles.includes(activeGroup ?? ''))?.[0];

  // با هر تغییرِ مسیر، فقط گروهِ صفحهٔ جاری در سایدبار باز می‌ماند (رفتارِ آکاردئون).
  useEffect(() => {
    if (activeGroup) setOpenGroup(activeGroup);
  }, [activeGroup]);

  function toggleGroup(title: string) {
    setOpenGroup((prev) => (prev === title ? undefined : title));
  }

  function goToTopMenu(titles: string[]) {
    const group = visibleNavGroups.find((g) => g.title && titles.includes(g.title));
    if (!group) return;
    setOpenGroup(group.title);
    navigate(group.items[0].to);
  }

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
          {TOP_MENU_MAP.filter(([, titles]) => visibleNavGroups.some((g) => g.title && titles.includes(g.title))).map(([label, titles]) => (
            <button key={label} type="button" className={label === activeTopMenu ? 'active' : ''} onClick={() => goToTopMenu(titles)}>
              {label}
            </button>
          ))}
        </nav>
        <div className="search" style={{ cursor: 'pointer' }}
          onClick={() => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }))}>
          <ErpIcon name="search" />
          <input placeholder="جستجو در همه‌جا…   Ctrl+K" readOnly style={{ cursor: 'pointer' }} tabIndex={-1} />
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

      {showLicenseBanner && (
        <div style={{
          flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12,
          padding: '6px 16px', fontSize: 12.5,
          background: license!.isExpired ? 'var(--danger-50, #fef2f2)' : 'var(--warning-50, #fffbeb)',
          color: license!.isExpired ? 'var(--danger-700)' : 'var(--warning-700, #9a6700)',
          borderBottom: '1px solid var(--border)',
        }}>
          <span>
            {license!.isExpired
              ? 'دورهٔ یک‌سالهٔ رایگانِ این نصب به پایان رسیده — برایِ تمدید با پشتیبانی تماس بگیرید.'
              : `${license!.daysRemaining} روز به پایانِ دورهٔ یک‌سالهٔ رایگانِ این نصب مانده است.`}
          </span>
          <button type="button" onClick={() => setLicenseBannerDismissed(true)}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'inherit', fontSize: 12 }}>✕</button>
        </div>
      )}

      <div className="erp-body">
        {/* ── سایدبار — رَدیفِ آیکونِ جمع‌شده (۶۰px)، با هاور تا ۲۲۰px بازمی‌شود ── */}
        <aside className="erp-side">
          <nav className="nav">
            {visibleNavGroups.map((group, gi) => {
              const isCollapsed = group.title ? group.title !== openGroup : false;
              return (
                <div key={gi}>
                  {group.title && (
                    <button type="button" className="grouplabel" onClick={() => toggleGroup(group.title!)}
                      style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%', background: 'transparent', border: 'none', cursor: 'pointer' }}>
                      <span>{group.title}</span>
                      <span style={{ transform: isCollapsed ? 'rotate(-90deg)' : 'none', transition: 'transform 120ms' }}>▾</span>
                    </button>
                  )}
                  {!isCollapsed && group.items.map((item) => (
                    <NavLink key={item.to} to={item.to} end={item.end} title={item.label}
                      className={({ isActive }) => `ni${isActive ? ' active' : ''}`}>
                      <ErpIcon name={item.icon} />
                      <span className="lb">{item.label}</span>
                    </NavLink>
                  ))}
                  {gi === 0 && <div className="sep" />}
                </div>
              );
            })}
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

      <CommandPalette items={palette} />
    </div>
  );
}
