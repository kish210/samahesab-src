// سما حساب — کلاینتِ موبایلِ PWA. کلاینتِ سرورِ SamaHesab.API (JWT).
'use strict';

const $ = (id) => document.getElementById(id);
const store = {
  get base() { return localStorage.getItem('sh_base') || ''; },          // خالی = هم‌مبدأ
  set base(v) { localStorage.setItem('sh_base', v || ''); },
  get token() { return localStorage.getItem('sh_token') || ''; },
  set token(v) { v ? localStorage.setItem('sh_token', v) : localStorage.removeItem('sh_token'); },
};

const fa = (n) => (typeof n === 'number' && isFinite(n)) ? Math.round(n).toLocaleString('en-US') : '۰';

// تاریخِ امروزِ شمسی به فرمتِ موردِ نیازِ API (مثل 1405/03/27، ارقامِ لاتین).
function todayJalali() {
  try {
    const p = new Intl.DateTimeFormat('en-US-u-ca-persian',
      { year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
    const g = (t) => p.find(x => x.type === t).value;
    return `${g('year')}/${g('month')}/${g('day')}`;
  } catch { return ''; }
}

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
    msg.textContent = /Failed to fetch|NetworkError/.test(e.message || '')
      ? 'اتصال به سرور برقرار نشد — آدرس/شبکه را بررسی کنید.' : 'ورود ناموفق: ' + e.message;
  } finally { btn.disabled = false; }
}

function logout() { store.token = ''; showLogin(); }

async function openDashboard() {
  showDash();
  try { const me = await api('/api/auth/me'); $('hello').textContent = 'سلام، ' + (me.fullName || me.username || 'کاربر'); }
  catch { $('hello').textContent = 'داشبورد'; }
  await Promise.all([loadKpis(), loadAlerts()]);
}

// تعریفِ کارت‌های KPI: [کلید, برچسب, رنگ, واحد]
const KPI_DEFS = [
  ['todaySales', 'فروشِ امروز', 'blue', 'ریال'],
  ['monthSales', 'فروشِ ماه', 'blue', 'ریال'],
  ['monthProfit', 'سودِ ماه', 'green', 'ریال'],
  ['monthMarginPercent', 'حاشیهٔ سود', 'green', '٪'],
  ['receivablesTotal', 'دریافتنی', 'gold', 'ریال'],
  ['payablesTotal', 'پرداختنی', 'gold', 'ریال'],
  ['chequesInProcess', 'چکِ در جریان', 'amber', 'فقره'],
];

async function loadKpis() {
  const box = $('kpis'); const cust = $('customers');
  box.innerHTML = skeleton(6); cust.innerHTML = '';
  try {
    const d = await api('/api/analytics/dashboard/manager?today=' + encodeURIComponent(todayJalali()));
    box.innerHTML = KPI_DEFS
      .filter(([k]) => typeof d[k] === 'number')
      .map(([k, l, c, u]) => `
        <div class="kpi ${c}">
          <div class="v">${fa(d[k])}<span class="u">${u}</span></div>
          <div class="l">${l}</div>
        </div>`).join('');
    // برترین مشتریان
    const tc = Array.isArray(d.topCustomers) ? d.topCustomers : [];
    if (tc.length) {
      cust.innerHTML = `<section class="card"><h2>🏆 برترین مشتریان</h2><div class="list">` +
        tc.slice(0, 5).map(c => `
          <div class="row tc">
            <span class="av">${escapeHtml((c.name || '?').slice(0, 1))}</span>
            <span class="grow">${escapeHtml(c.name || '—')}<small>${fa(c.invoiceCount)} فاکتور</small></span>
            <span class="amt">${fa(c.total)}</span>
          </div>`).join('') + `</div></section>`;
    }
  } catch (e) {
    box.innerHTML = `<div class="kpi"><div class="l">${escapeHtml(e.message || 'داشبورد در دسترس نیست')}</div></div>`;
  }
}

async function loadAlerts() {
  const box = $('alerts');
  try {
    const a = await api('/api/analytics/alerts?today=' + encodeURIComponent(todayJalali()));
    const list = Array.isArray(a) ? a : (a.items || a.alerts || []);
    if (!list.length) { box.innerHTML = '<div class="empty">✓ هشداری نیست</div>'; return; }
    box.innerHTML = list.slice(0, 30).map(x => {
      const text = x.message || x.title || x.text || x.description || JSON.stringify(x);
      const sev = (x.severity || x.level || '').toString().toLowerCase();
      const cls = /crit|danger|high|red/.test(sev) ? 'r' : /warn|med|amber/.test(sev) ? 'w' : '';
      return `<div class="row"><span class="dot ${cls}"></span><span>${escapeHtml(text)}</span></div>`;
    }).join('');
  } catch { box.innerHTML = '<div class="empty">هشدارها در دسترس نیست</div>'; }
}

function skeleton(n) { let s = ''; for (let i = 0; i < n; i++) s += '<div class="kpi sk"></div>'; return s; }
function escapeHtml(s) { return String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }

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
