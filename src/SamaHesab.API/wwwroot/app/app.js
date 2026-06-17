// سما حساب — کلاینتِ موبایلِ PWA. کلاینتِ سرورِ SamaHesab.API (JWT).
'use strict';

const $ = (id) => document.getElementById(id);
const store = {
  get base() { return localStorage.getItem('sh_base') || ''; },          // خالی = هم‌مبدأ
  set base(v) { localStorage.setItem('sh_base', v || ''); },
  get token() { return localStorage.getItem('sh_token') || ''; },
  set token(v) { v ? localStorage.setItem('sh_token', v) : localStorage.removeItem('sh_token'); },
};

const fa = (n) => (typeof n === 'number' && isFinite(n))
  ? n.toLocaleString('fa-IR') : (n ?? '—');

// نگاشتِ کلیدهای پرکاربردِ داشبورد به برچسبِ فارسی (هرچه نبود، خودِ کلید نشان داده می‌شود).
const LBL = {
  todaySales: 'فروشِ امروز', salesToday: 'فروشِ امروز', monthSales: 'فروشِ ماه',
  receivables: 'دریافتنی', payables: 'پرداختنی', cashBalance: 'موجودیِ نقد',
  dueCheques: 'چکِ سررسید', lowStock: 'کالای کم‌موجود', invoiceCount: 'تعدادِ فاکتور',
  netProfit: 'سودِ خالص', totalSales: 'جمعِ فروش', openShifts: 'شیفتِ باز',
};

async function api(path, opts = {}) {
  const headers = Object.assign({ 'Accept': 'application/json' }, opts.headers || {});
  if (store.token) headers['Authorization'] = 'Bearer ' + store.token;
  if (opts.body && !headers['Content-Type']) headers['Content-Type'] = 'application/json';
  const res = await fetch(store.base + path, Object.assign({}, opts, { headers }));
  if (res.status === 401) { logout(); throw new Error('نشست منقضی شد — دوباره وارد شوید.'); }
  if (!res.ok) throw new Error('خطای سرور (' + res.status + ')');
  const ct = res.headers.get('content-type') || '';
  return ct.includes('json') ? res.json() : res.text();
}

function showLogin() { $('topbar').classList.add('hidden'); $('dashView').classList.add('hidden'); $('loginView').classList.remove('hidden'); }
function showDash() { $('loginView').classList.add('hidden'); $('topbar').classList.remove('hidden'); $('dashView').classList.remove('hidden'); }

async function login() {
  const btn = $('btnLogin'); const msg = $('loginMsg');
  const base = $('server').value.trim().replace(/\/+$/, '');
  const username = $('username').value.trim();
  const password = $('password').value;
  if (!username || !password) { msg.className = 'msg err'; msg.textContent = 'نام کاربری و رمز را وارد کنید.'; return; }
  btn.disabled = true; msg.className = 'msg'; msg.textContent = 'در حال ورود…';
  store.base = base;
  try {
    const r = await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password, companyId: 1, branchId: 1 }) });
    if (!r || !r.accessToken) throw new Error('پاسخِ نامعتبر از سرور.');
    store.token = r.accessToken;
    msg.className = 'msg ok'; msg.textContent = 'موفق ✓';
    await openDashboard();
  } catch (e) {
    store.token = '';
    msg.className = 'msg err';
    msg.textContent = (e.message && /Failed to fetch|NetworkError/.test(e.message))
      ? 'اتصال به سرور برقرار نشد — آدرس/شبکه را بررسی کنید.' : 'ورود ناموفق: ' + e.message;
  } finally { btn.disabled = false; }
}

function logout() { store.token = ''; showLogin(); }

async function openDashboard() {
  showDash();
  // سلام به کاربر
  try { const me = await api('/api/auth/me'); $('hello').textContent = 'سلام، ' + (me.fullName || me.username || 'کاربر') + ' 👋'; }
  catch { $('hello').textContent = 'داشبورد'; }
  await Promise.all([loadKpis(), loadAlerts()]);
}

async function loadKpis() {
  const box = $('kpis'); box.innerHTML = '';
  try {
    const d = await api('/api/analytics/dashboard/manager');
    const entries = Object.entries(d || {}).filter(([, v]) => typeof v === 'number').slice(0, 6);
    if (!entries.length) { box.innerHTML = '<div class="kpi"><div class="l">داده‌ای برای نمایش نیست</div></div>'; return; }
    box.innerHTML = entries.map(([k, v]) =>
      `<div class="kpi"><div class="v">${fa(v)}</div><div class="l">${LBL[k] || k}</div></div>`).join('');
  } catch (e) {
    box.innerHTML = `<div class="kpi"><div class="l">داشبورد در دسترس نیست</div></div>`;
  }
}

async function loadAlerts() {
  const box = $('alerts');
  try {
    const a = await api('/api/analytics/alerts');
    const list = Array.isArray(a) ? a : (a.items || a.alerts || []);
    if (!list.length) { box.innerHTML = '<div class="muted pad">هشداری نیست ✓</div>'; return; }
    box.innerHTML = list.slice(0, 30).map(x => {
      const text = x.message || x.title || x.text || x.description || JSON.stringify(x);
      const sev = (x.severity || x.level || '').toString().toLowerCase();
      const cls = /crit|danger|high|red/.test(sev) ? 'r' : /warn|med|amber/.test(sev) ? 'w' : '';
      return `<div class="row"><span class="dot ${cls}"></span><span>${escapeHtml(text)}</span></div>`;
    }).join('');
  } catch { box.innerHTML = '<div class="muted pad">هشدارها در دسترس نیست</div>'; }
}

function escapeHtml(s) { return String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }

// ── راه‌اندازی ──
window.addEventListener('DOMContentLoaded', () => {
  $('server').value = store.base;
  $('verLine').textContent = 'سما حساب موبایل · نسخهٔ بتا';
  $('btnLogin').addEventListener('click', login);
  $('password').addEventListener('keydown', e => { if (e.key === 'Enter') login(); });
  $('btnLogout').addEventListener('click', logout);
  $('btnRefresh').addEventListener('click', () => { loadKpis(); loadAlerts(); });
  if (store.token) openDashboard(); else showLogin();

  if ('serviceWorker' in navigator) navigator.serviceWorker.register('sw.js').catch(() => {});
});
