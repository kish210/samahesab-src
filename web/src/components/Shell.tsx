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
 * ترتیب/گروه‌بندیِ core هم‌راستا با ساختارِ منویِ حسابفا (@2026-07-22، طبقِ درخواستِ صریحِ
 * کاربر «ساختار مثلِ حسابفا باشه»، تحقیقِ زندهٔ راهنمایِ جامعِ hesabfa.com/help/topics):
 * داشبورد → اشخاص → کالاها و خدمات → انبار → خزانه (معادلِ «بانکداری»یِ حسابفا؛ طبقِ خواستِ
 * کاربر نامش عوض نشد ولی مثلِ حسابفا زیرمجموعهٔ حسابداری نیست، دستهٔ جداست) → خرید و فروش
 * (حسابفا این دو را یک دسته می‌بیند) → حسابداری (فقط سند/دفترِ حساب‌ها) → گزارش‌هایِ مالی.
 * ماژول‌هایِ اختیاری (POS/رستوران/مودیان/گردشگری) طبقِ تصمیمِ صریحِ کاربر هرکدام جداگانه و
 * صریح می‌مانند (نه یک «سایر بخش‌ها»یِ حسابفاواره) — چون فقط وقتی نصب/فعال‌اند دیده می‌شوند.
 * آیکون‌ها از design-system/screens/erp-shell.js (ErpIcon) — همان مجموعه‌یِ خطیِ رسمی.
 */
const NAV_GROUPS: NavGroup[] = [
  { items: [{ to: '/', label: 'داشبورد', icon: 'home', end: true }] },
  { title: 'اشخاص', items: [
    { to: '/customers', label: 'مشتریان', icon: 'people' },
    { to: '/suppliers', label: 'تأمین‌کنندگان', icon: 'people' },
  ] },
  { title: 'کالاها و خدمات', items: [
    { to: '/products', label: 'کالاها', icon: 'inventory' },
  ] },
  { title: 'انبار', items: [
    { to: '/warehouse', label: 'انبار', icon: 'inventory' },
  ] },
  { title: 'خزانه', items: [
    { to: '/treasury', label: 'دریافتنی/پرداختنی', icon: 'treasury' },
    { to: '/cheques', label: 'تابلویِ چک', icon: 'cheque' },
  ] },
  { title: 'خرید و فروش', items: [
    { to: '/sales', label: 'فاکتورهایِ فروش', icon: 'sales' },
    { to: '/purchase', label: 'فاکتورهایِ خرید', icon: 'purchasing' },
  ] },
  { title: 'حسابداری', items: [
    { to: '/vouchers', label: 'اسنادِ حسابداری', icon: 'accounting' },
    { to: '/accounts', label: 'دفترِ حساب‌ها', icon: 'accounting' },
  ] },
  { title: 'گزارش‌هایِ مالی', items: [
    { to: '/trial-balance', label: 'تراز آزمایشی', icon: 'reports' },
    { to: '/general-ledger', label: 'دفترِ کل/معین', icon: 'reports' },
    { to: '/balance-sheet', label: 'ترازنامه', icon: 'reports' },
    { to: '/income-statement', label: 'صورتِ سودوزیان', icon: 'reports' },
    { to: '/branch-summary', label: 'خلاصهٔ شعب', icon: 'reports' },
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
  { title: 'مودیان', items: [
    { to: '/tax-invoicing', label: 'صورتحسابِ الکترونیکی', icon: 'accounting', moduleKey: 'TaxInvoicing' },
  ] },
  { title: 'گردشگری', items: [
    { to: '/tourism', label: 'فروش و محصولات', icon: 'sales', moduleKey: 'Tourism' },
  ] },
  { title: 'حقوق و دستمزد', items: [
    { to: '/hr/employees', label: 'پرسنل', icon: 'people', moduleKey: 'HR' },
    { to: '/hr/payroll', label: 'حقوق و دستمزد', icon: 'accounting', moduleKey: 'HR' },
  ] },
  { title: 'هتل', items: [
    { to: '/hotel', label: 'هتل / اقامتگاه', icon: 'restaurant', moduleKey: 'Hotel' },
  ] },
  // «سیستم» قبلاً فقط مدیریتِ ماژول‌ها بود؛ به «تنظیمات» تغییرِ نام یافت و صفحهٔ نو
  // «دربارهٔ سیستم» (نسخه/مجوز/کاربرِ جاری) هم زیرش اضافه شد — طبقِ بازخوردِ کاربر
  // که وب برخلافِ دسکتاپ هیچ بخشِ «تنظیمات»ی نداشت.
  { title: 'تنظیمات', items: [
    { to: '/modules', label: 'مدیریتِ ماژول‌ها', icon: 'modules' },
    { to: '/settings', label: 'دربارهٔ سیستم', icon: 'settings' },
  ] },
];

const FLAT_ITEMS = NAV_GROUPS.flatMap((g) => g.items);

/** این ۸ عنوان دقیقاً هم‌ارزِ `MENUS` در design-system/screens/erp-shell.js‌اند — طبقِ خواستهٔ
 * کاربر این‌ها فقط در توپ‌بار (دکمه‌هایِ دسته) دیده می‌شوند، نه در سایدبار — تا هر بخش یک‌بار
 * دیده شود (سایدبار فقط بخش‌هایِ خارج از این دسته‌ها را نشان می‌دهد: داشبورد/POS/رستوران). */
const TOPBAR_GROUP_TITLES = new Set([
  'اشخاص', 'کالاها و خدمات', 'انبار', 'خزانه', 'خرید و فروش', 'حسابداری', 'گزارش‌هایِ مالی', 'تنظیمات',
]);

/** عنوانِ صفحهٔ جاری — هم‌الگو با `CurrentPageTitle` در MainShellWindowِ دسکتاپ (بایندِ توپ‌بار). */
function currentPageTitle(pathname: string): string {
  const exact = FLAT_ITEMS.find((i) => i.to === pathname);
  if (exact) return exact.label;
  const prefix = FLAT_ITEMS.filter((i) => i.to !== '/' && pathname.startsWith(i.to)).sort((a, b) => b.to.length - a.to.length)[0];
  return prefix?.label ?? 'سما حساب';
}

export function Shell() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const initial = (user?.fullName || 'S').trim().charAt(0);
  const [pendingCheques, setPendingCheques] = useState(0);
  const [loadedModules, setLoadedModules] = useState<string[] | null>(null);
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [licenseBannerDismissed, setLicenseBannerDismissed] = useState(false);

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

  const activeGroupObj = visibleNavGroups.find((g) => g.items.some((i) => location.pathname.startsWith(i.to) && i.to !== '/'));
  const activeGroup = activeGroupObj?.title;
  // تب‌هایِ زیرِ گروهِ فعال — فقط وقتی بیش از یک زیرصفحه دارد (وگرنه یک‌تب اضافه بی‌فایده است).
  const activeTabs = activeGroupObj && activeGroupObj.items.length > 1 ? activeGroupObj.items : null;

  // سایدبار فقط بخش‌هایِ خارج از توپ‌بار را نشان می‌دهد (داشبورد/POS/رستوران) — دسته‌هایِ
  // TOPBAR_GROUP_TITLES از سایدبار حذف شده‌اند چون همان‌ها در توپ‌بار هستند.
  const sidebarGroups = visibleNavGroups.filter((g) => !g.title || !TOPBAR_GROUP_TITLES.has(g.title));
  const topbarGroups = visibleNavGroups.filter((g) => g.title && TOPBAR_GROUP_TITLES.has(g.title));

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
          {topbarGroups.map((g) => (
            <button key={g.title} type="button" className={g.title === activeGroup ? 'active' : ''}
              onClick={() => navigate(g.items[0].to)}>
              {g.title}
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
        {/* ── سایدبار — یک ردیفِ مستقیم به‌ازایِ هر بخش (بدونِ تکرارِ زیرمنوهایی که همین حالا
            در توپ‌بار/تب‌استریپ هستند)، رَدیفِ آیکونِ جمع‌شده (۶۰px) که با هاور تا ۲۲۰px باز می‌شود ── */}
        <aside className="erp-side">
          <nav className="nav">
            {sidebarGroups.map((group, gi) => {
              const main = group.items[0];
              const isActive = group.title ? group.title === activeGroup : location.pathname === main.to;
              return (
                <div key={gi}>
                  <NavLink to={main.to} end={main.end} title={group.title ?? main.label}
                    className={() => `ni${isActive ? ' active' : ''}`}>
                    <ErpIcon name={main.icon} />
                    <span className="lb">{group.title ?? main.label}</span>
                  </NavLink>
                  {gi === 0 && <div className="sep" />}
                </div>
              );
            })}
          </nav>
        </aside>

        <div className="erp-main">
          {/* ── تب‌استریپِ زیرصفحه‌هایِ بخشِ فعال — سایدبار فقط به اولین صفحهٔ هر بخش می‌رود،
              سوییچ بینِ زیرصفحه‌ها (مثلاً اسنادِ حسابداری/دفترِ حساب‌ها) از همین‌جا انجام می‌شود. */}
          {activeTabs && (
            <div className="erp-tabs">
              {activeTabs.map((item) => (
                <NavLink key={item.to} to={item.to} end={item.end}
                  className={({ isActive }) => `dt${isActive ? ' on' : ''}`}>
                  {item.label}
                </NavLink>
              ))}
            </div>
          )}
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
