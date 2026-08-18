import { useEffect, useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface TrendRow { period: string; total: number; count: number }
interface InventoryTrendRow { period: string; inQty: number; outQty: number; net: number }
interface BranchPerfRow { branchId: number; name: string; total: number; invoiceCount: number }
interface SupplierRow { supplierId: number; name: string; total: number; invoiceCount: number }
interface AlertRow { kind: string; severity: number | string; title: string; refId: number | null; amount: number }
interface ReorderRow { productId: number; name: string; onHand: number; threshold: number; suggestedQty: number }
interface TopProductRow { productId: number; name: string; total: number; lineCount: number; profit: number }
interface CustomerAnalyticsDto {
  customerId: number; name: string; balance: number; totalSales: number; invoiceCount: number;
  averagePerInvoice: number; firstInvoiceDate: string | null; lastInvoiceDate: string | null;
  monthlyTrend: TrendRow[]; topProducts: TopProductRow[];
}
interface CustomerPick { id: number; code: string; name: string }

type Tab = 'sales-trend' | 'inventory-trend' | 'branch-performance' | 'top-suppliers' | 'alerts' | 'reorder' | 'customer360';

const severityBadge: Record<string, string> = { '0': 'badge-gray', Info: 'badge-gray', '1': 'badge-yellow', Warning: 'badge-yellow', '2': 'badge-yellow', Critical: 'badge-yellow' };

/** U-WEB-ANALYTICS — AnalyticsController (BI/داشبوردهایِ نقشی) از قبل کامل بود؛ تنها
 * DashboardPage.tsx (اندپوینتِ جداگانهٔ /api/dashboard/full) در وب وجود داشت. این صفحه
 * گزارش‌هایِ BIِ مانده (روندِ فروش/موجودی، عملکردِ شعب، تأمین‌کنندگانِ برتر، هشدارها،
 * پیشنهادِ سفارش، مشتریِ ۳۶۰) را پوشش می‌دهد. ⚠️ عمداً ۶ داشبوردِ نقشیِ جداگانه
 * (مدیر/حسابدار/انباردار/صندوقدار/رستوران/مالک) پورت نشدند — DashboardPage.tsxِ
 * موجود همان نقشِ خلاصهٔ کاریِ روزانه را برایِ همهٔ کاربران پوشش می‌دهد و افزودنِ ۶
 * صفحهٔ نزدیک‌به‌تکراری فراتر از این گپ بود. */
export function AnalyticsPage() {
  const [tab, setTab] = useState<Tab>('sales-trend');
  const [fromDate, setFromDate] = useState(todayJalaliString().slice(0, 8) + '01');
  const [toDate, setToDate] = useState(todayJalaliString());
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [salesTrend, setSalesTrend] = useState<TrendRow[] | null>(null);
  const [invTrend, setInvTrend] = useState<InventoryTrendRow[] | null>(null);
  const [branchPerf, setBranchPerf] = useState<BranchPerfRow[] | null>(null);
  const [suppliers, setSuppliers] = useState<SupplierRow[] | null>(null);
  const [alerts, setAlerts] = useState<AlertRow[] | null>(null);
  const [reorder, setReorder] = useState<ReorderRow[] | null>(null);

  async function run() {
    setLoading(true);
    setError(null);
    try {
      if (tab === 'sales-trend') setSalesTrend(await apiGet(`/api/analytics/sales-trend?from=${fromDate}&to=${toDate}`));
      else if (tab === 'inventory-trend') setInvTrend(await apiGet(`/api/analytics/inventory-trend?from=${fromDate}&to=${toDate}`));
      else if (tab === 'branch-performance') setBranchPerf(await apiGet(`/api/analytics/branch-performance?from=${fromDate}&to=${toDate}`));
      else if (tab === 'top-suppliers') setSuppliers(await apiGet(`/api/analytics/top-suppliers?from=${fromDate}&to=${toDate}&take=20`));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ گزارش.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (tab === 'alerts') {
      apiGet<AlertRow[]>(`/api/analytics/alerts?today=${todayJalaliString()}`).then(setAlerts)
        .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ هشدارها.'));
    } else if (tab === 'reorder') {
      apiGet<ReorderRow[]>('/api/analytics/reorder-suggestions').then(setReorder)
        .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ پیشنهادِ سفارش.'));
    }
  }, [tab]);

  // ── مشتریِ ۳۶۰ ──
  const [custSearch, setCustSearch] = useState('');
  const [custResults, setCustResults] = useState<CustomerPick[]>([]);
  const [custAnalytics, setCustAnalytics] = useState<CustomerAnalyticsDto | null>(null);

  async function searchCustomers() {
    if (!custSearch.trim()) return;
    try {
      const rows = await apiGet<CustomerPick[]>(`/api/customers?search=${encodeURIComponent(custSearch)}`);
      setCustResults(rows);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'جست‌وجوی مشتری ناموفق بود.');
    }
  }

  async function loadCustomer360(id: number) {
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<CustomerAnalyticsDto>(`/api/analytics/customer/${id}?from=${fromDate}&to=${toDate}`);
      setCustAnalytics(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بارگیریِ تحلیلِ مشتری ناموفق بود.');
    } finally {
      setLoading(false);
    }
  }

  const trendColumns: Column<TrendRow>[] = [
    { key: 'period', header: 'دوره', render: (r) => r.period },
    { key: 'total', header: 'مبلغ', numeric: true, render: (r) => money(r.total) },
    { key: 'count', header: 'تعداد', numeric: true, render: (r) => r.count },
  ];
  const invColumns: Column<InventoryTrendRow>[] = [
    { key: 'period', header: 'دوره', render: (r) => r.period },
    { key: 'in', header: 'ورود', numeric: true, render: (r) => money(r.inQty) },
    { key: 'out', header: 'خروج', numeric: true, render: (r) => money(r.outQty) },
    { key: 'net', header: 'خالص', numeric: true, render: (r) => money(r.net) },
  ];
  const branchColumns: Column<BranchPerfRow>[] = [
    { key: 'name', header: 'شعبه', render: (r) => r.name },
    { key: 'total', header: 'فروش', numeric: true, render: (r) => money(r.total) },
    { key: 'count', header: 'تعدادِ فاکتور', numeric: true, render: (r) => r.invoiceCount },
  ];
  const supplierColumns: Column<SupplierRow>[] = [
    { key: 'name', header: 'تأمین‌کننده', render: (r) => r.name },
    { key: 'total', header: 'مبلغِ خرید', numeric: true, render: (r) => money(r.total) },
    { key: 'count', header: 'تعدادِ فاکتور', numeric: true, render: (r) => r.invoiceCount },
  ];
  const alertColumns: Column<AlertRow>[] = [
    { key: 'severity', header: 'شدت', render: (r) => <span className={`badge ${severityBadge[String(r.severity)] ?? 'badge-gray'}`}>{String(r.severity)}</span> },
    { key: 'title', header: 'عنوان', render: (r) => r.title },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => (r.amount ? money(r.amount) : '—') },
  ];
  const reorderColumns: Column<ReorderRow>[] = [
    { key: 'name', header: 'کالا', render: (r) => r.name },
    { key: 'onHand', header: 'موجودی', numeric: true, render: (r) => money(r.onHand) },
    { key: 'threshold', header: 'نقطهٔ سفارش', numeric: true, render: (r) => money(r.threshold) },
    { key: 'suggested', header: 'پیشنهادِ سفارش', numeric: true, render: (r) => money(r.suggestedQty) },
  ];
  const topProductColumns: Column<TopProductRow>[] = [
    { key: 'name', header: 'کالا', render: (r) => r.name },
    { key: 'total', header: 'فروش', numeric: true, render: (r) => money(r.total) },
    { key: 'lines', header: 'تعداد', numeric: true, render: (r) => r.lineCount },
    { key: 'profit', header: 'سود', numeric: true, render: (r) => money(r.profit) },
  ];

  const needsDateRange = tab === 'sales-trend' || tab === 'inventory-trend' || tab === 'branch-performance' || tab === 'top-suppliers' || tab === 'customer360';

  return (
    <div>
      <PageHeader title="تحلیل و هوشِ تجاری" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'sales-trend' ? 'on' : ''} onClick={() => setTab('sales-trend')}>روندِ فروش</button>
        <button type="button" className={tab === 'inventory-trend' ? 'on' : ''} onClick={() => setTab('inventory-trend')}>روندِ موجودی</button>
        <button type="button" className={tab === 'branch-performance' ? 'on' : ''} onClick={() => setTab('branch-performance')}>عملکردِ شعب</button>
        <button type="button" className={tab === 'top-suppliers' ? 'on' : ''} onClick={() => setTab('top-suppliers')}>تأمین‌کنندگانِ برتر</button>
        <button type="button" className={tab === 'alerts' ? 'on' : ''} onClick={() => setTab('alerts')}>هشدارها</button>
        <button type="button" className={tab === 'reorder' ? 'on' : ''} onClick={() => setTab('reorder')}>پیشنهادِ سفارش</button>
        <button type="button" className={tab === 'customer360' ? 'on' : ''} onClick={() => setTab('customer360')}>مشتریِ ۳۶۰</button>
      </div>

      {needsDateRange && (
        <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)' }}>
          <JalaliDateInput value={fromDate} onChange={setFromDate} label="از تاریخ" />
          <JalaliDateInput value={toDate} onChange={setToDate} label="تا تاریخ" />
          {tab !== 'customer360' && (
            <button type="button" className="btn btn-primary btn-sm" onClick={run} disabled={loading}>
              {loading ? 'در حالِ اجرا…' : 'اجرا'}
            </button>
          )}
        </div>
      )}

      {tab === 'sales-trend' && salesTrend && <DataTable columns={trendColumns} rows={salesTrend} rowKey={(r) => r.period} emptyText="داده‌ای نیست." />}
      {tab === 'inventory-trend' && invTrend && <DataTable columns={invColumns} rows={invTrend} rowKey={(r) => r.period} emptyText="داده‌ای نیست." />}
      {tab === 'branch-performance' && branchPerf && <DataTable columns={branchColumns} rows={branchPerf} rowKey={(r) => r.branchId} emptyText="داده‌ای نیست." />}
      {tab === 'top-suppliers' && suppliers && <DataTable columns={supplierColumns} rows={suppliers} rowKey={(r) => r.supplierId} emptyText="داده‌ای نیست." />}
      {tab === 'alerts' && <DataTable columns={alertColumns} rows={alerts ?? []} rowKey={(r, i) => `${r.kind}-${i}`} emptyText="هشداری نیست." />}
      {tab === 'reorder' && <DataTable columns={reorderColumns} rows={reorder ?? []} rowKey={(r) => r.productId} emptyText="پیشنهادی نیست." />}

      {tab === 'customer360' && (
        <div>
          <div style={{ display: 'flex', gap: 'var(--space-2)', marginBottom: 'var(--space-3)' }}>
            <input className="input" placeholder="جست‌وجویِ مشتری…" value={custSearch}
              onChange={(e) => setCustSearch(e.target.value)} style={{ maxWidth: 260 }} />
            <button type="button" className="btn btn-secondary btn-sm" onClick={searchCustomers}>جست‌وجو</button>
          </div>
          {custResults.length > 0 && !custAnalytics && (
            <div className="gbox" style={{ padding: 'var(--space-2)', marginBottom: 'var(--space-3)' }}>
              {custResults.map((c) => (
                <div key={c.id} style={{ padding: '6px 4px', cursor: 'pointer' }}
                  role="button" tabIndex={0}
                  onClick={() => loadCustomer360(c.id)}
                  onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); loadCustomer360(c.id); } }}>
                  {c.code} — {c.name}
                </div>
              ))}
            </div>
          )}
          {custAnalytics && (
            <div>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => { setCustAnalytics(null); setCustResults([]); }}>← جست‌وجویِ دیگر</button>
              <div className="gh" style={{ marginTop: 'var(--space-2)' }}>{custAnalytics.name}</div>
              <div className="sumbar" style={{ margin: 'var(--space-3) 0' }}>
                <span>مانده: {money(custAnalytics.balance)}</span>
                <span>جمعِ فروش: {money(custAnalytics.totalSales)}</span>
                <span>تعدادِ فاکتور: {custAnalytics.invoiceCount}</span>
                <span>میانگین: {money(custAnalytics.averagePerInvoice)}</span>
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 'var(--space-3)' }}>
                اولین فاکتور: {custAnalytics.firstInvoiceDate ?? '—'} · آخرین فاکتور: {custAnalytics.lastInvoiceDate ?? '—'}
              </div>
              <div style={{ fontWeight: 600, marginBottom: 6 }}>روندِ ماهانه</div>
              <DataTable columns={trendColumns} rows={custAnalytics.monthlyTrend} rowKey={(r) => r.period} emptyText="روندی نیست." />
              <div style={{ fontWeight: 600, margin: '14px 0 6px' }}>کالاهایِ برتر</div>
              <DataTable columns={topProductColumns} rows={custAnalytics.topProducts} rowKey={(r) => r.productId} emptyText="کالایی نیست." />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
