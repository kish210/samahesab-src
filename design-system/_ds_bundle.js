/* @ds-bundle: {"format":3,"namespace":"DesignSystem_53d3bd","components":[{"name":"Badge","sourcePath":"components/Badge.jsx"},{"name":"Button","sourcePath":"components/Button.jsx"},{"name":"Card","sourcePath":"components/Card.jsx"},{"name":"Input","sourcePath":"components/Input.jsx"},{"name":"Select","sourcePath":"components/Input.jsx"},{"name":"StatCard","sourcePath":"components/StatCard.jsx"}],"sourceHashes":{"components/Badge.jsx":"dd5faa908df7","components/Button.jsx":"d1910f9bcd95","components/Card.jsx":"066931ed4313","components/Input.jsx":"7e12df5d0a9d","components/StatCard.jsx":"bbf90373dfcd","screens/erp-shell.js":"16062dcb258e"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.DesignSystem_53d3bd = window.DesignSystem_53d3bd || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/Badge.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const TONE = {
  neutral: 'badge-gray',
  blue: 'badge-blue',
  green: 'badge-green',
  amber: 'badge-amber',
  red: 'badge-red',
  gold: 'badge-gold',
  solid: 'badge-solid'
};

/** Compact status pill. Use a dot for live/state semantics. */
function Badge({
  tone = 'neutral',
  dot = false,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("span", _extends({
    className: ['badge', TONE[tone] || TONE.neutral, className].filter(Boolean).join(' ')
  }, rest), dot && /*#__PURE__*/React.createElement("span", {
    className: "dot"
  }), children);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Badge.jsx", error: String((e && e.message) || e) }); }

// components/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * SamaHesab primary action button.
 * Variants map to the brand system; gold is reserved for rare premium emphasis.
 */
function Button({
  variant = 'primary',
  size = 'md',
  iconLeading,
  iconTrailing,
  iconOnly = false,
  disabled = false,
  className = '',
  children,
  ...rest
}) {
  const cls = ['btn', `btn-${variant}`, size === 'sm' ? 'btn-sm' : size === 'lg' ? 'btn-lg' : '', iconOnly ? 'btn-icon' : '', className].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", _extends({
    className: cls,
    disabled: disabled,
    "aria-disabled": disabled || undefined
  }, rest), iconLeading, !iconOnly && children, iconOnly && children, iconTrailing);
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Button.jsx", error: String((e && e.message) || e) }); }

// components/Card.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/** Surface container. Optional header with title + actions. */
function Card({
  title,
  actions,
  padded = false,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['card', className].filter(Boolean).join(' ')
  }, rest), (title || actions) && /*#__PURE__*/React.createElement("div", {
    className: "card-head"
  }, /*#__PURE__*/React.createElement("div", {
    className: "card-title"
  }, title), actions && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 'var(--space-2)'
    }
  }, actions)), padded ? /*#__PURE__*/React.createElement("div", {
    className: "card-pad"
  }, children) : children);
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Card.jsx", error: String((e && e.message) || e) }); }

// components/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/** Labelled text input with optional leading icon, hint and error state. */
function Input({
  label,
  hint,
  error,
  required = false,
  leadingIcon,
  size = 'md',
  className = '',
  id,
  ...rest
}) {
  const inputId = id || `inp-${Math.random().toString(36).slice(2, 8)}`;
  const input = /*#__PURE__*/React.createElement("input", _extends({
    id: inputId,
    className: ['input', size === 'sm' ? 'input-sm' : '', error ? 'is-error' : '', className].filter(Boolean).join(' '),
    "aria-invalid": error ? true : undefined
  }, rest));
  return /*#__PURE__*/React.createElement("div", {
    className: "field"
  }, label && /*#__PURE__*/React.createElement("label", {
    className: "label",
    htmlFor: inputId
  }, label, required && /*#__PURE__*/React.createElement("span", {
    className: "req"
  }, "*")), leadingIcon ? /*#__PURE__*/React.createElement("div", {
    className: "input-group"
  }, /*#__PURE__*/React.createElement("span", {
    className: "ig-icon"
  }, leadingIcon), input) : input, error ? /*#__PURE__*/React.createElement("span", {
    className: "hint",
    style: {
      color: 'var(--danger-500)'
    }
  }, error) : hint ? /*#__PURE__*/React.createElement("span", {
    className: "hint"
  }, hint) : null);
}

/** Native select styled to match the system. */
function Select({
  label,
  hint,
  required = false,
  size = 'md',
  className = '',
  id,
  children,
  ...rest
}) {
  const selId = id || `sel-${Math.random().toString(36).slice(2, 8)}`;
  return /*#__PURE__*/React.createElement("div", {
    className: "field"
  }, label && /*#__PURE__*/React.createElement("label", {
    className: "label",
    htmlFor: selId
  }, label, required && /*#__PURE__*/React.createElement("span", {
    className: "req"
  }, "*")), /*#__PURE__*/React.createElement("select", _extends({
    id: selId,
    className: ['select', size === 'sm' ? 'select-sm' : '', className].filter(Boolean).join(' ')
  }, rest), children), hint && /*#__PURE__*/React.createElement("span", {
    className: "hint"
  }, hint));
}
Object.assign(__ds_scope, { Input, Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Input.jsx", error: String((e && e.message) || e) }); }

// components/StatCard.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/** KPI tile for dashboards: label, large value, optional delta + sparkline slot. */
function StatCard({
  label,
  value,
  unit,
  delta,
  deltaTone = 'auto',
  icon,
  footer,
  accent = false,
  className = '',
  ...rest
}) {
  let tone = deltaTone;
  if (delta != null && deltaTone === 'auto') {
    const up = String(delta).trim().startsWith('+') || typeof delta === 'number' && delta >= 0;
    tone = up ? 'green' : 'red';
  }
  const deltaColor = tone === 'green' ? 'var(--success-500)' : tone === 'red' ? 'var(--danger-500)' : 'var(--text-muted)';
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['card', 'card-pad', className].filter(Boolean).join(' '),
    style: accent ? {
      borderTop: '3px solid var(--gold-500)'
    } : undefined
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-sm)',
      color: 'var(--text-muted)',
      fontWeight: 500
    }
  }, label), icon && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--blue-500)',
      display: 'inline-flex'
    }
  }, icon)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'baseline',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-3xl)',
      lineHeight: 1,
      fontWeight: 700,
      color: 'var(--text-strong)',
      fontVariantNumeric: 'tabular-nums'
    }
  }, value), unit && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-base)',
      color: 'var(--text-muted)',
      fontWeight: 500
    }
  }, unit)), (delta != null || footer) && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      marginTop: 10
    }
  }, delta != null && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-sm)',
      fontWeight: 600,
      color: deltaColor
    }
  }, delta), footer && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-sm)',
      color: 'var(--text-muted)'
    }
  }, footer)));
}
Object.assign(__ds_scope, { StatCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/StatCard.jsx", error: String((e && e.message) || e) }); }

// screens/erp-shell.js
try { (() => {
/* ============================================================
   SamaHesab — ErpShell
   تزریق پوسته‌ی یکسان ERP: تاپ‌بار + سایدبار + تب اسناد + نوار وضعیت
   استفاده:
   ErpShell.mount({
     active: 'accounting',          // ماژول فعال سایدبار
     menu: 'حسابداری',              // منوی فعال تاپ‌بار
     tabs: [['ثبت سند','voucher.html',true], ...],
     status: { form: 'ثبت سند', extra: 'سند ۱۴۰۵۸۳' }
   })
   ============================================================ */
(function () {
  const svg = p => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${p}</svg>`;
  const I = {
    home: '<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V21h14V9.5"/>',
    accounting: '<path d="M5 4h11l3 3v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z"/><path d="M8 9h7M8 13h7M8 17h4"/>',
    sales: '<circle cx="9" cy="20" r="1.4"/><circle cx="18" cy="20" r="1.4"/><path d="M2 3h3l2.4 12.3a1 1 0 0 0 1 .8h8.7a1 1 0 0 0 1-.78L21 7H6"/>',
    purchasing: '<path d="M3 6h11v9H3z"/><path d="M14 9h4l3 3v3h-7z"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/>',
    inventory: '<path d="M3.3 7.3 12 3l8.7 4.3v9.4L12 21l-8.7-4.3z"/><path d="M3.3 7.3 12 11.6l8.7-4.3M12 21v-9.4"/>',
    treasury: '<rect x="2.5" y="6" width="19" height="12" rx="2"/><circle cx="12" cy="12" r="2.6"/><path d="M6 9.5v5M18 9.5v5"/>',
    cheque: '<rect x="2.5" y="5" width="19" height="14" rx="2"/><path d="M6 9h6M6 12.5h4"/><path d="m14 13.5 2 2 4-4"/>',
    pos: '<rect x="3" y="3" width="18" height="13" rx="1.6"/><path d="M8 20h8M12 16v4"/>',
    restaurant: '<path d="M5 3v7a3 3 0 0 0 6 0V3M8 3v18M19 3c-1.5 1-2.5 3-2.5 6 0 2 1 3 2.5 3v9"/>',
    people: '<circle cx="9" cy="8" r="3.2"/><path d="M3.5 20a5.5 5.5 0 0 1 11 0"/><path d="M16 6.2a3 3 0 0 1 0 5.6M21 20a5 5 0 0 0-3.5-4.8"/>',
    reports: '<path d="M4 20V4"/><path d="M4 20h16"/><rect x="7" y="11" width="3" height="6"/><rect x="12" y="7" width="3" height="10"/><rect x="17" y="13" width="3" height="4"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M12 2.5v2.2M12 19.3v2.2M21.5 12h-2.2M4.7 12H2.5M18.4 5.6l-1.6 1.6M7.2 16.8l-1.6 1.6M18.4 18.4l-1.6-1.6M7.2 7.2 5.6 5.6"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>',
    bell: '<path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>',
    calc: '<rect x="5" y="2.5" width="14" height="19" rx="2"/><path d="M8.5 6h7"/><path d="M8.5 11h.01M12 11h.01M15.5 11h.01M8.5 14.5h.01M12 14.5h.01M15.5 14.5h.01M8.5 18h.01M12 18h.01M15.5 18h.01"/>',
    help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 4.5 1.5c0 1.5-2 2-2 3.5"/><path d="M12 17h.01"/>'
  };
  const SIDE = [['home', 'میز کار', 'workspace.html'], ['accounting', 'حسابداری', 'voucher.html'], ['sales', 'فروش', 'sales-invoice.html'], ['purchasing', 'خرید', '#'], ['inventory', 'انبار', 'inventory-transfer.html'], ['treasury', 'خزانه‌داری', 'cheques.html'], ['pos', 'صندوق فروش', 'pos.html'], ['restaurant', 'رستوران', 'restaurant.html'], ['people', 'اشخاص', 'customer-card.html'], ['reports', 'گزارش‌ها', '#'], 'sep', ['settings', 'تنظیمات', '#']];
  const MENUS = ['حسابداری', 'خزانه‌داری', 'خرید', 'فروش', 'انبار', 'اشخاص', 'گزارشات', 'سیستم'];
  function topbar(opts) {
    const menus = MENUS.map(m => `<button class="${m === opts.menu ? 'active' : ''}">${m}</button>`).join('');
    return `
      <div class="brand">
        <div class="mark">س</div>
        <div><div class="nm">سماع‌حساب</div><div class="ver">نسخه ۳٫۲ سازمانی</div></div>
      </div>
      <nav class="erp-menu">${menus}</nav>
      <div class="search">${svg(I.search)}<input placeholder="جستجو در همه‌جا…   Ctrl+K" readonly style="cursor:pointer" onclick="ErpShell.openSearch()" /></div>
      <button class="tb-ic" title="ماشین‌حساب">${svg(I.calc)}</button>
      <button class="tb-ic" title="راهنما">${svg(I.help)}</button>
      <button class="tb-ic" title="اعلان‌ها">${svg(I.bell)}<span class="ping"></span></button>
      <div class="user">
        <div class="av">ر.م</div>
        <div><div class="un">رضا مرادی</div><div class="ur">حسابدار ارشد · شعبه مرکزی</div></div>
      </div>`;
  }
  function sidebar(active) {
    return `<nav class="nav">` + SIDE.map(it => {
      if (it === 'sep') return '<div class="sep"></div>';
      const [k, lb, href] = it;
      return `<a class="ni ${k === active ? 'active' : ''}" href="${href}" title="${lb}">${svg(I[k])}<span class="lb">${lb}</span></a>`;
    }).join('') + `</nav>`;
  }
  function tabstrip(tabs) {
    return (tabs || []).map(([lb, href, on]) => `<a class="dt ${on ? 'on' : ''}" href="${href}" style="text-decoration:none">${lb}<span class="x">×</span></a>`).join('') + `<button class="add" title="سند جدید">＋</button>`;
  }
  function statusbar(st) {
    st = st || {};
    return `
      <div class="si"><span class="ok-dot"></span><span>متصل</span></div>
      <div class="si">کاربر: <b>رضا مرادی</b></div>
      <div class="si">دوره مالی: <b>۱۴۰۵</b></div>
      <div class="si">شعبه: <b>مرکزی</b></div>
      ${st.form ? `<div class="si">فرم: <b>${st.form}</b></div>` : ''}
      ${st.extra ? `<div class="si">${st.extra}</div>` : ''}
      <div class="grow"></div>
      <div class="si">۱۴۰۵/۰۳/۱۵ · ۱۰:۲۴</div>
      <div class="si">سرور: <b>SRV-01</b></div>`;
  }
  window.ErpShell = {
    mount(opts) {
      opts = opts || {};
      const top = document.getElementById('erp-top');
      const side = document.getElementById('erp-side');
      const tabs = document.getElementById('erp-tabs');
      const status = document.getElementById('erp-status');
      if (top) top.innerHTML = topbar(opts);
      if (side) side.innerHTML = sidebar(opts.active);
      if (tabs) tabs.innerHTML = tabstrip(opts.tabs);
      if (status) status.innerHTML = statusbar(opts.status);
      mountPalette();
    }
  };
  window.ErpShell.openSearch = openPalette;

  /* ============================================================
     پالت فرمان (Ctrl+K) + جستجوی سراسری (F3)
     ابتکار شماره‌ی ۱ رودمپ — روی همه‌ی صفحات فعال است.
     ============================================================ */
  const CMDS = [
  // [نوع, عنوان, زیرعنوان, آیکون, مقصد]
  ['دستور', 'ثبت سند جدید', 'حسابداری · F2', 'voucher', 'voucher.html'], ['دستور', 'فاکتور فروش جدید', 'فروش · Ctrl+2', 'sales', 'sales-invoice.html'], ['دستور', 'فاکتور خرید جدید', 'خرید · Ctrl+3', 'purchasing', 'purchase-invoice.html'], ['دستور', 'انتقال بین انبار', 'انبار · Ctrl+4', 'inventory', 'inventory-transfer.html'], ['دستور', 'صندوق فروشگاهی', 'فروش · F12', 'pos', 'pos.html'], ['دستور', 'مدیریت چک', 'خزانه‌داری · Ctrl+5', 'cheque', 'cheques.html'], ['صفحه', 'میز کار', 'داشبورد', 'home', 'workspace.html'], ['صفحه', 'تراز آزمایشی', 'گزارش‌ها', 'reports', 'trial-balance.html'], ['صفحه', 'دفتر کل / معین', 'گزارش‌ها', 'reports', 'general-ledger.html'], ['صفحه', 'اسناد حسابداری', 'حسابداری', 'accounting', 'accounting-docs.html'], ['صفحه', 'رستوران', 'فروش · لمسی', 'restaurant', 'restaurant.html'], ['مشتری', 'بازرگانی پارس خودرو', 'کد ۲۰۰۱۲ · مانده ۷۸٬۰۰۰٬۰۰۰ بد', 'people', 'customer-card.html'], ['مشتری', 'شرکت آلفا تجارت', 'کد ۲۰۰۱۵ · مانده ۴۵٬۲۰۰٬۰۰۰ بد', 'people', 'customer-card.html'], ['کالا', 'روغن موتور ۵ لیتری بهران', 'K1001 · موجودی ۱۲', 'inventory', 'product-card.html'], ['کالا', 'باتری ۶۰ آمپر سپاهان', 'K1004 · موجودی ۶', 'inventory', 'product-card.html'], ['کالا', 'شمع موتور NGK', 'K1005 · کم‌موجود', 'inventory', 'product-card.html'], ['سند', 'سند ۱۴۰۵۸۳', 'فروش نقدی · پیش‌نویس', 'accounting', 'voucher.html'], ['فاکتور', 'فاکتور F000052', 'پارس خودرو · ۴۸٬۲۰۰٬۰۰۰', 'sales', 'sales-invoice.html']];
  const TYPE_CLR = {
    'دستور': '#4B5F97',
    'صفحه': '#5A6FA2',
    'مشتری': '#B58A3C',
    'کالا': '#2E7D52',
    'سند': '#5A6478',
    'فاکتور': '#5A6478'
  };
  function paletteHTML() {
    return `
    <div class="cmdk-ov" id="cmdkOv">
      <div class="cmdk">
        <div class="cmdk-in">
          ${svg(I.search)}
          <input id="cmdkInput" placeholder="جستجوی دستور، مشتری، کالا، سند، فاکتور…" autocomplete="off" />
          <span class="cmdk-esc">Esc</span>
        </div>
        <div class="cmdk-list" id="cmdkList"></div>
        <div class="cmdk-foot">
          <span><b>↑↓</b> حرکت</span><span><b>Enter</b> انتخاب</span><span><b>Esc</b> بستن</span>
          <span style="margin-inline-start:auto">جستجوی سراسری سماع‌حساب</span>
        </div>
      </div>
    </div>`;
  }
  let cmdkSel = 0,
    cmdkFiltered = CMDS;
  function renderList(q) {
    q = (q || '').trim();
    cmdkFiltered = q ? CMDS.filter(c => (c[1] + ' ' + c[2] + ' ' + c[0]).toLowerCase().includes(q.toLowerCase())) : CMDS;
    if (cmdkSel >= cmdkFiltered.length) cmdkSel = 0;
    const list = document.getElementById('cmdkList');
    if (!cmdkFiltered.length) {
      list.innerHTML = `<div class="cmdk-empty">نتیجه‌ای یافت نشد</div>`;
      return;
    }
    list.innerHTML = cmdkFiltered.map((c, i) => `
      <a class="cmdk-it ${i === cmdkSel ? 'hl' : ''}" href="${c[4]}" data-i="${i}">
        <span class="cmdk-ic" style="background:${TYPE_CLR[c[0]]}1A;color:${TYPE_CLR[c[0]]}">${svg(I[c[3]] || I.search)}</span>
        <span class="cmdk-tx"><span class="t">${c[1]}</span><span class="s">${c[2]}</span></span>
        <span class="cmdk-type" style="color:${TYPE_CLR[c[0]]}">${c[0]}</span>
      </a>`).join('');
    const hl = list.querySelector('.cmdk-it.hl');
    if (hl) hl.scrollIntoView({
      block: 'nearest'
    });
  }
  function openPalette() {
    const ov = document.getElementById('cmdkOv');
    if (!ov) return;
    ov.classList.add('on');
    cmdkSel = 0;
    const inp = document.getElementById('cmdkInput');
    inp.value = '';
    renderList('');
    setTimeout(() => inp.focus(), 30);
  }
  function closePalette() {
    const ov = document.getElementById('cmdkOv');
    if (ov) ov.classList.remove('on');
  }
  let paletteMounted = false;
  function mountPalette() {
    if (paletteMounted) return;
    paletteMounted = true;
    const wrap = document.createElement('div');
    wrap.innerHTML = paletteHTML();
    document.body.appendChild(wrap.firstElementChild);
    if (!document.getElementById('cmdk-style')) {
      const st = document.createElement('style');
      st.id = 'cmdk-style';
      st.textContent = CMDK_CSS;
      document.head.appendChild(st);
    }
    const ov = document.getElementById('cmdkOv');
    const inp = document.getElementById('cmdkInput');
    const list = document.getElementById('cmdkList');
    inp.addEventListener('input', () => {
      cmdkSel = 0;
      renderList(inp.value);
    });
    list.addEventListener('mousemove', e => {
      const it = e.target.closest('.cmdk-it');
      if (it) {
        cmdkSel = +it.dataset.i;
        renderList(inp.value);
      }
    });
    ov.addEventListener('click', e => {
      if (e.target === ov) closePalette();
    });
    document.addEventListener('keydown', e => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        ov.classList.contains('on') ? closePalette() : openPalette();
        return;
      }
      if (e.key === 'F3') {
        e.preventDefault();
        openPalette();
        return;
      }
      if (!ov.classList.contains('on')) return;
      if (e.key === 'Escape') {
        e.preventDefault();
        closePalette();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        cmdkSel = Math.min(cmdkSel + 1, cmdkFiltered.length - 1);
        renderList(inp.value);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        cmdkSel = Math.max(cmdkSel - 1, 0);
        renderList(inp.value);
      } else if (e.key === 'Enter') {
        e.preventDefault();
        const c = cmdkFiltered[cmdkSel];
        if (c) window.location.href = c[4];
      }
    });
  }
  const CMDK_CSS = `
    .cmdk-ov { position: fixed; inset: 0; background: rgba(8,14,28,.5); backdrop-filter: blur(2px); display: none; align-items: flex-start; justify-content: center; z-index: 200; padding-top: 12vh; }
    .cmdk-ov.on { display: flex; }
    .cmdk { width: 560px; max-width: 92vw; background: #fff; border-radius: 14px; box-shadow: 0 24px 64px rgba(8,14,28,.4); overflow: hidden; font-family: var(--font-sans); }
    .cmdk-in { display: flex; align-items: center; gap: 11px; padding: 14px 16px; border-bottom: 1px solid var(--border); }
    .cmdk-in svg { width: 19px; height: 19px; color: var(--gray-400); flex: none; }
    .cmdk-in input { flex: 1; border: none; outline: none; font-family: var(--font-sans); font-size: 15px; color: var(--text-strong); background: transparent; }
    .cmdk-esc { font-size: 10.5px; font-weight: 600; color: var(--text-muted); background: var(--gray-100); border: 1px solid var(--border); border-radius: 5px; padding: 2px 7px; }
    .cmdk-list { max-height: 340px; overflow-y: auto; padding: 6px; }
    .cmdk-it { display: flex; align-items: center; gap: 11px; padding: 9px 11px; border-radius: 8px; cursor: pointer; text-decoration: none; }
    .cmdk-it.hl { background: var(--blue-50); }
    .cmdk-it:hover { text-decoration: none; }
    .cmdk-ic { width: 32px; height: 32px; border-radius: 8px; display: flex; align-items: center; justify-content: center; flex: none; }
    .cmdk-ic svg { width: 17px; height: 17px; }
    .cmdk-tx { display: flex; flex-direction: column; min-width: 0; }
    .cmdk-tx .t { font-size: 13.5px; font-weight: 600; color: var(--text-strong); }
    .cmdk-tx .s { font-size: 11.5px; color: var(--text-muted); }
    .cmdk-type { margin-inline-start: auto; font-size: 11px; font-weight: 700; flex: none; }
    .cmdk-empty { padding: 30px; text-align: center; color: var(--text-muted); font-size: 13px; }
    .cmdk-foot { display: flex; align-items: center; gap: 16px; padding: 9px 16px; border-top: 1px solid var(--border); background: var(--gray-50); font-size: 11px; color: var(--text-muted); }
    .cmdk-foot b { color: var(--text-body); font-weight: 700; }
  `;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "screens/erp-shell.js", error: String((e && e.message) || e) }); }

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.Button = __ds_scope.Button;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.StatCard = __ds_scope.StatCard;

})();
