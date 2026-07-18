import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface PurchaseInvoiceRow {
  id: number;
  number: string;
  date: string;
  supplierName: string;
  total: number;
  paid: number;
  remain: number;
  status: string;
}

export function PurchaseInvoicesPage() {
  const [rows, setRows] = useState<PurchaseInvoiceRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<PurchaseInvoiceRow[]>('/api/purchase/invoices')
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فاکتورهایِ خرید.'))
      .finally(() => setLoading(false));
  }, []);

  const columns: Column<PurchaseInvoiceRow>[] = [
    { key: 'num', header: 'شماره', render: (r) => r.number },
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'supplier', header: 'تأمین‌کننده', render: (r) => r.supplierName },
    { key: 'total', header: 'مبلغِ کل', numeric: true, render: (r) => money(r.total) },
    { key: 'paid', header: 'پرداختی', numeric: true, render: (r) => money(r.paid) },
    { key: 'remain', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600, color: r.remain > 0 ? 'var(--danger-700)' : 'inherit' }}>{money(r.remain)}</span> },
    { key: 'status', header: 'وضعیت', render: (r) => <span className="badge badge-blue">{r.status}</span> },
  ];

  return (
    <div>
      <PageHeader
        title="فاکتورهایِ خرید"
        actions={
          <>
            <Link to="/purchase/return" className="btn btn-secondary btn-sm">مرجوعیِ خرید</Link>
            <Link to="/purchase/new" className="btn btn-primary btn-sm">فاکتورِ نو</Link>
          </>
        }
      />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="فاکتوری یافت نشد." />}
    </div>
  );
}
