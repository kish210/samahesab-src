// چاپِ حرارتیِ رسید (POS / رستوران / KOT) — بدونِ وابستگیِ سخت‌افزاری:
// پنجره‌ای جدا با `@page { size: 80mm auto }` باز می‌شود تا دیالوگِ چاپِ مرورگر رویِ
// کاغذِ حرارتیِ ۸۰mm (نه A4) تنظیم شود. کاربر پرینترِ حرارتیِ نصب‌شده را همان‌جا انتخاب می‌کند.

export interface ThermalItem {
  name: string;
  /** تعدادِ فرمت‌شده — مثلاً «۲ × ۱٬۲۵۰٬۰۰۰». */
  qty: string;
  /** مبلغِ ردیفِ فرمت‌شده (ریال). */
  amount: string;
  /** یادداشتِ آشپزخانه (مثل «بدون پیاز») — در KOT نمایش داده می‌شود. */
  note?: string;
}

export interface ThermalLine {
  label: string;
  value: string;
  bold?: boolean;
}

export interface ThermalReceipt {
  title: string;
  header: ThermalLine[];
  items: ThermalItem[];
  totals?: ThermalLine[];
  /** «مبلغ به حروف» — اگر داده شود زیرِ جمع نمایش داده می‌شود. */
  amountInWords?: string;
  footer?: string[];
}

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** بازکردنِ پنجرهٔ چاپِ حرارتی با همان رسید و ارسالِ مستقیم به دیالوگِ چاپ. */
export function printThermal(r: ThermalReceipt): void {
  const w = window.open('', '_blank', 'width=340,height=640');
  if (!w) {
    alert('مرورگر پنجرهٔ چاپ را مسدود کرد؛ لطفاً اجازهٔ popup را بدهید.');
    return;
  }

  const header = r.header
    .map((l) => `<div class="row"><span>${esc(l.label)}</span><span class="amt">${esc(l.value)}</span></div>`)
    .join('');

  const items = r.items
    .map((i) => `
      <div class="item">
        <div class="row"><span class="n">${esc(i.name)}</span><span class="amt">${esc(i.amount)}</span></div>
        <div class="q">${esc(i.qty)}${i.note ? `<span class="note"> · ${esc(i.note)}</span>` : ''}</div>
      </div>`)
    .join('');

  const totals = (r.totals ?? [])
    .map((t) => `<div class="row${t.bold ? ' bold' : ''}"><span>${esc(t.label)}</span><span class="amt">${esc(t.value)}</span></div>`)
    .join('');

  const footer = (r.footer ?? [])
    .map((f) => `<div class="foot">${esc(f)}</div>`)
    .join('');

  const html = `<!doctype html>
<html dir="rtl" lang="fa">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(r.title)}</title>
<style>
  @page { size: 80mm auto; margin: 0; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    width: 80mm; padding: 3mm 3mm; color: #000; background: #fff;
    font-family: 'Vazirmatn', Tahoma, 'Segoe UI', sans-serif;
    font-size: 12px; line-height: 1.5;
  }
  .title { text-align: center; font-weight: 800; font-size: 15px; margin: 0 0 2px; }
  .sub { text-align: center; font-size: 10.5px; color: #333; margin-bottom: 2px; }
  .dash { border-top: 1px dashed #000; margin: 5px 0; }
  .row { display: flex; justify-content: space-between; gap: 8px; padding: 1px 0; }
  .row.bold { font-weight: 800; font-size: 13px; }
  .row.total { font-weight: 800; font-size: 15px; }
  .amt { font-variant-numeric: tabular-nums; white-space: nowrap; text-align: left; }
  .item { padding: 2px 0; }
  .item .n { font-weight: 600; }
  .item .q { font-size: 10.5px; color: #333; }
  .item .note { font-weight: 600; }
  .words { font-size: 10.5px; color: #333; margin: 2px 0; }
  .foot { text-align: center; font-size: 10.5px; margin-top: 3px; }
</style>
</head>
<body>
  <div class="title">${esc(r.title)}</div>
  <div class="sub">${esc(r.header.map((h) => `${h.label}: ${h.value}`).join(' · '))}</div>
  <div class="dash"></div>
  ${header}
  <div class="dash"></div>
  ${items}
  ${totals ? `<div class="dash"></div>${totals}` : ''}
  ${r.amountInWords ? `<div class="words">${esc(r.amountInWords)}</div>` : ''}
  ${footer ? `<div class="dash"></div>${footer}` : ''}
  <script>window.onload = function(){ setTimeout(function(){ window.print(); setTimeout(function(){ window.close(); }, 500); }, 120); };</script>
</body>
</html>`;

  w.document.write(html);
  w.document.close();
  w.focus();
}
