import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { useAuth } from '../auth/AuthContext';

interface ReceivableDto {
  customerId: number;
  name: string;
  mobile: string | null;
  balance: number;
  creditLimit: number;
  isOverCreditLimit: boolean;
}

interface PayableDto {
  supplierId: number;
  name: string;
  mobile: string | null;
  balance: number;
}

function todayPersianIsoLike(): string {
  // این کلاینتِ سبک تاریخِ شمسیِ واقعی نمی‌سازد؛ سرور خودش تاریخِ ارسالی را ذخیره می‌کند —
  // برایِ عملیاتِ خزانه معمولاً همان تاریخِ روزِ جاری از تقویمِ سرور کافی است. اینجا فقط جهتِ
  // نمایش/ارسال یک رشتهٔ ساده می‌فرستیم؛ صفحاتِ بعدی (فاکتور) بایدِ انتخابِ تاریخِ شمسیِ واقعی داشته باشند.
  return new Date().toISOString().slice(0, 10);
}

export function TreasuryPage() {
  const { user } = useAuth();
  const [receivables, setReceivables] = useState<ReceivableDto[]>([]);
  const [payables, setPayables] = useState<PayableDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  async function load() {
    setLoading(true);
    try {
      const [r, p] = await Promise.all([
        apiGet<ReceivableDto[]>('/api/treasury/receivables'),
        apiGet<PayableDto[]>('/api/treasury/payables'),
      ]);
      setReceivables(r);
      setPayables(p);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ اطلاعاتِ خزانه.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function receiveFull(customerId: number, amount: number) {
    setMsg(null);
    setBusyId(customerId);
    try {
      await apiPost('/api/treasury/receipts', {
        branchId: user?.branchId ?? 1,
        fiscalYearId: 1,
        date: todayPersianIsoLike(),
        customerId,
        amount,
        paymentMethod: 'نقدی',
        description: 'وصول از فهرستِ دریافتنی‌ها (کلاینتِ وب)',
      });
      setMsg({ kind: 'success', text: 'دریافت ثبت شد.' });
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ثبتِ دریافت ناموفق بود.' });
    } finally {
      setBusyId(null);
    }
  }

  async function payFull(supplierId: number, amount: number) {
    setMsg(null);
    setBusyId(supplierId);
    try {
      await apiPost('/api/treasury/payments', {
        branchId: user?.branchId ?? 1,
        fiscalYearId: 1,
        date: todayPersianIsoLike(),
        supplierId,
        amount,
        paymentMethod: 'نقدی',
        description: 'پرداخت از فهرستِ پرداختنی‌ها (کلاینتِ وب)',
      });
      setMsg({ kind: 'success', text: 'پرداخت ثبت شد.' });
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ثبتِ پرداخت ناموفق بود.' });
    } finally {
      setBusyId(null);
    }
  }

  const receivableColumns: Column<ReceivableDto>[] = [
    { key: 'name', header: 'مشتری', render: (r) => r.name },
    { key: 'mobile', header: 'موبایل', render: (r) => <span style={{ direction: 'ltr' }}>{r.mobile ?? ''}</span> },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600, color: 'var(--danger-700)' }}>{money(r.balance)}</span> },
    {
      key: 'over', header: 'سقفِ اعتبار',
      render: (r) => (r.isOverCreditLimit ? <span className="badge badge-red">بیش از سقف</span> : money(r.creditLimit)),
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <button className="btn btn-secondary btn-sm" disabled={busyId === r.customerId} onClick={() => receiveFull(r.customerId, r.balance)}>
          {busyId === r.customerId ? '…' : 'وصولِ کامل'}
        </button>
      ),
    },
  ];

  const payableColumns: Column<PayableDto>[] = [
    { key: 'name', header: 'تأمین‌کننده', render: (r) => r.name },
    { key: 'mobile', header: 'موبایل', render: (r) => <span style={{ direction: 'ltr' }}>{r.mobile ?? ''}</span> },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600 }}>{money(r.balance)}</span> },
    {
      key: 'action', header: '',
      render: (r) => (
        <button className="btn btn-secondary btn-sm" disabled={busyId === r.supplierId} onClick={() => payFull(r.supplierId, r.balance)}>
          {busyId === r.supplierId ? '…' : 'پرداختِ کامل'}
        </button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="خزانه" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {msg && <StatusMessage kind={msg.kind}>{msg.text}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}

      {!loading && !error && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
          <div>
            <h2 style={{ marginBottom: 'var(--space-2)' }}>دریافتنی‌ها</h2>
            <DataTable columns={receivableColumns} rows={receivables} rowKey={(r) => r.customerId} emptyText="بدهکاری‌ای نیست." />
          </div>
          <div>
            <h2 style={{ marginBottom: 'var(--space-2)' }}>پرداختنی‌ها</h2>
            <DataTable columns={payableColumns} rows={payables} rowKey={(r) => r.supplierId} emptyText="بدهی‌ای نیست." />
          </div>
        </div>
      )}
    </div>
  );
}
