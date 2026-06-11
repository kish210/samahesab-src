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
    help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 4.5 1.5c0 1.5-2 2-2 3.5"/><path d="M12 17h.01"/>',
  };

  const SIDE = [
    ['home', 'میز کار', 'workspace.html'],
    ['accounting', 'حسابداری', 'voucher.html'],
    ['sales', 'فروش', 'sales-invoice.html'],
    ['purchasing', 'خرید', '#'],
    ['inventory', 'انبار', 'inventory-transfer.html'],
    ['treasury', 'خزانه‌داری', 'cheques.html'],
    ['pos', 'صندوق فروش', 'pos.html'],
    ['restaurant', 'رستوران', 'restaurant.html'],
    ['people', 'اشخاص', 'customer-card.html'],
    ['reports', 'گزارش‌ها', '#'],
    'sep',
    ['settings', 'تنظیمات', '#'],
  ];

  const MENUS = ['حسابداری', 'خزانه‌داری', 'خرید', 'فروش', 'انبار', 'اشخاص', 'گزارشات', 'سیستم'];

  function topbar(opts) {
    const menus = MENUS.map(m => `<button class="${m === opts.menu ? 'active' : ''}">${m}</button>`).join('');
    return `
      <div class="brand">
        <div class="mark">س</div>
        <div><div class="nm">سماع‌حساب</div><div class="ver">نسخه ۳٫۲ سازمانی</div></div>
      </div>
      <nav class="erp-menu">${menus}</nav>
      <div class="search">${svg(I.search)}<input placeholder="جستجو در همه‌جا…   Ctrl+K" /></div>
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
    return (tabs || []).map(([lb, href, on]) =>
      `<a class="dt ${on ? 'on' : ''}" href="${href}" style="text-decoration:none">${lb}<span class="x">×</span></a>`
    ).join('') + `<button class="add" title="سند جدید">＋</button>`;
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
    }
  };
})();
