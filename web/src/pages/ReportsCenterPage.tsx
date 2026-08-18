import { useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface ReportItem { code: string; name: string }
interface ReportCategory { name: string; items: ReportItem[] }
interface ReportRunRow { code: string; name: string; debit: number; credit: number; balance: number }
interface ReportRunResult { rows: ReportRunRow[] | null; redirectMessage: string | null }

/** خروجیِ اکسلِ (CSV) سمتِ کلاینت — با BOM تا اکسلِ فارسی درست باز کند (هم‌الگو با خروجیِ
 *  اکسلِ حسابفا). مستقیماً از همان ردیف‌هایِ نمایش‌داده‌شده ساخته می‌شود، بدونِ نیاز به اندپوینتِ
 *  جدا؛ نامِ فایل از کدِ گزارش ساخته می‌شود. */
function downloadReportCsv(filename: string, header: string[], rows: ReportRunRow[]) {
  const esc = (v: string | number) => {
    const s = String(v ?? '');
    return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [header.map(esc).join(',')];
  for (const r of rows) {
    lines.push([r.code, r.name, r.debit, r.credit, r.balance].map(esc).join(','));
  }
  const blob = new Blob(['\uFEFF' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${filename}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

const CATEGORIES: ReportCategory[] = [
  { name: 'حسابداری', items: [
    { code: 'TrialBalance', name: 'تراز آزمایشی' },
    { code: 'GeneralLedger', name: 'دفتر کل' },
    { code: 'BalanceSheet', name: 'ترازنامه' },
    { code: 'ProfitLoss', name: 'سود و زیان' },
    { code: 'CashFlow', name: 'جریان وجوه نقد' },
  ] },
  { name: 'فروش', items: [
    { code: 'SalesSummary', name: 'خلاصه فروش' },
    { code: 'SalesByCustomer', name: 'فروش به تفکیک مشتری' },
    { code: 'SalesByProduct', name: 'فروش به تفکیک کالا' },
    { code: 'CustomerBalance', name: 'مانده مشتریان' },
  ] },
  { name: 'خرید', items: [
    { code: 'PurchaseSummary', name: 'خلاصه خرید' },
    { code: 'SupplierBalance', name: 'مانده تأمین‌کنندگان' },
  ] },
  { name: 'انبار', items: [
    { code: 'StockStatus', name: 'وضعیت موجودی' },
    { code: 'LowStock', name: 'کمبود موجودی' },
    { code: 'StockValuation', name: 'ارزیابی موجودی' },
  ] },
  { name: 'چک', items: [
    { code: 'ChequesInProcess', name: 'چک‌های در جریان' },
    { code: 'ChequesDue', name: 'چک‌های سررسید' },
    { code: 'ChequesReturned', name: 'چک‌های برگشتی' },
  ] },
  { name: 'منابع انسانی', items: [
    { code: 'EmployeeList', name: 'لیست کارکنان' },
  ] },
];

/** U-WEB-REPORTS-CENTER — RunReportQuery از قبل ۱۸ گزارشِ هسته را پیاده‌سازی کرده بود
 * (پورت‌شده برایِ دسکتاپ) ولی هیچ اندپوینت/صفحهٔ وبی نداشت. SalaryReport/AttendanceSummary
 * عمداً حذف شدند — ماژولِ HR که وب‌آمادگی‌اش هنوز بررسی نشده. «دفترِ معین»/«گردشِ کالا»
 * هم نیستند — نیازِ انتخابِ حساب/کالایِ مشخص دارند (صفحاتِ اختصاصیِ خودشان). */
export function ReportsCenterPage() {
  const [category, setCategory] = useState(CATEGORIES[0]);
  const [report, setReport] = useState<ReportItem>(CATEGORIES[0].items[0]);
  const [fromDate, setFromDate] = useState(todayJalaliString().slice(0, 8) + '01');
  const [toDate, setToDate] = useState(todayJalaliString());
  const [result, setResult] = useState<ReportRunResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function selectCategory(c: ReportCategory) {
    setCategory(c);
    setReport(c.items[0]);
    setResult(null);
  }

  async function run() {
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const data = await apiGet<ReportRunResult>(
        `/api/reports/run?code=${encodeURIComponent(report.code)}&from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}`,
      );
      setResult(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در اجرایِ گزارش.');
    } finally {
      setLoading(false);
    }
  }

  const columns: Column<ReportRunRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => r.name },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.debit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.credit) },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => money(r.balance) },
  ];

  const totalDebit = result?.rows?.reduce((s, r) => s + r.debit, 0) ?? 0;
  const totalCredit = result?.rows?.reduce((s, r) => s + r.credit, 0) ?? 0;

  return (
    <div>
      <PageHeader title="مرکزِ گزارشات" />
      <div style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap' }}>
        <div className="gbox" style={{ padding: 'var(--space-3)', minWidth: 180 }}>
          <div className="gh">دسته‌بندی</div>
          {CATEGORIES.map((c) => (
            <div key={c.name} className="minitabs">
              <button
                type="button"
                className={`btn btn-sm ${c.name === category.name ? 'btn-primary' : 'btn-ghost'}`}
                style={{ width: '100%', justifyContent: 'flex-start', marginBottom: 4 }}
                onClick={() => selectCategory(c)}
              >
                {c.name}
              </button>
            </div>
          ))}
        </div>

        <div style={{ flex: 1, minWidth: 320 }}>
          <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-4)' }}>
            <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', flexWrap: 'wrap' }}>
              <div className="field">
                <label className="label">گزارش</label>
                <select className="select" value={report.code} onChange={(e) => setReport(category.items.find((i) => i.code === e.target.value) ?? category.items[0])}>
                  {category.items.map((i) => <option key={i.code} value={i.code}>{i.name}</option>)}
                </select>
              </div>
              <JalaliDateInput value={fromDate} onChange={setFromDate} label="از تاریخ" />
              <JalaliDateInput value={toDate} onChange={setToDate} label="تا تاریخ" />
              <button type="button" className="btn btn-primary btn-sm" onClick={run} disabled={loading}>
                {loading ? 'در حالِ اجرا…' : 'اجرایِ گزارش'}
              </button>
              <button
                type="button"
                className="btn btn-sm"
                title="دانلودِ خروجیِ اکسل (CSV) از همین ردیف‌ها"
                disabled={!result?.rows?.length}
                onClick={() => downloadReportCsv(`report-${report.code}`, ['کد', 'نام', 'بدهکار', 'بستانکار', 'مانده'], result!.rows!)}
              >
                خروجیِ اکسل (CSV)
              </button>
              <button
                type="button"
                className="btn btn-sm"
                title="چاپِ گزارش — برای PDF «ذخیره به‌عنوان PDF» را در دیالوگِ چاپ انتخاب کنید"
                disabled={!result?.rows?.length}
                onClick={() => window.print()}
              >
                🖨 چاپ
              </button>
            </div>
          </div>

          {error && <StatusMessage kind="error">{error}</StatusMessage>}
          {result?.redirectMessage && <StatusMessage kind="muted">{result.redirectMessage}</StatusMessage>}

          {result?.rows && (
            <div className="print-area">
              {/* سربرگی که فقط رویِ کاغذ/PDF دیده می‌شود — هم‌الگو با TrialBalancePage/دفترِ کل */}
              <div className="print-only" style={{ display: 'none', marginBottom: 'var(--space-3)' }}>
                <h2>{report.name}</h2>
                <div>از {fromDate} تا {toDate}</div>
              </div>
              <DataTable columns={columns} rows={result.rows} rowKey={(r, i) => `${r.code}-${i}`} emptyText="داده‌ای برایِ این بازه نیست." />
              <div className="sumbar" style={{ marginTop: 'var(--space-3)' }}>
                <span>جمعِ بدهکار: {money(totalDebit)}</span>
                <span>جمعِ بستانکار: {money(totalCredit)}</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
