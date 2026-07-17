import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface SalesInvoiceRow {
  id: number;
  number: string;
  date: string;
  customerName: string;
  total: number;
  paid: number;
  remain: number;
  status: string;
}

export function SalesInvoicesPage() {
  const [rows, setRows] = useState<SalesInvoiceRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<SalesInvoiceRow[]>('/api/sales/invoices')
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فاکتورهایِ فروش.'))
      .finally(() => setLoading(false));
  }, []);

  const columns: Column<SalesInvoiceRow>[] = [
    { key: 'num', header: 'شماره', render: (r) => r.number },
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'customer', header: 'مشتری', render: (r) => r.customerName },
    { key: 'total', header: 'مبلغِ کل', numeric: true, render: (r) => money(r.total) },
    { key: 'paid', header: 'پرداختی', numeric: true, render: (r) => money(r.paid) },
    { key: 'remain', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600, color: r.remain > 0 ? 'var(--danger-700)' : 'inherit' }}>{money(r.remain)}</span> },
    { key: 'status', header: 'وضعیت', render: (r) => <span className="badge badge-blue">{r.status}</span> },
  ];

  return (
    <div>
      <PageHeader title="فاکتورهایِ فروش" actions={<Link to="/sales/new" className="btn btn-primary btn-sm">فاکتورِ نو</Link>} />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="فاکتوری یافت نشد." />}
    </div>
  );
}
