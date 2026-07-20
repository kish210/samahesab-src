import { useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { money, numberFormat } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';

interface VoucherListDto {
  id: number;
  voucherNumber: string;
  voucherDate: string;
  voucherTypeName: string;
  statusName: string;
  totalDebit: number;
  totalCredit: number;
  description: string | null;
  isBalanced: boolean;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export function VouchersPage() {
  const fiscalYearId = useActiveFiscalYear();
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<VoucherListDto> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function search(nextPage = 1) {
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<PagedResult<VoucherListDto>>(
        `/api/vouchers?fiscalYearId=${fiscalYearId ?? 1}&fromDate=${encodeURIComponent(fromDate)}&toDate=${encodeURIComponent(toDate)}&page=${nextPage}&size=20`,
      );
      setResult(data);
      setPage(nextPage);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ اسنادِ حسابداری.');
    } finally {
      setLoading(false);
    }
  }

  const columns: Column<VoucherListDto>[] = [
    { key: 'num', header: 'شمارهٔ سند', render: (r) => r.voucherNumber },
    { key: 'date', header: 'تاریخ', render: (r) => r.voucherDate },
    { key: 'type', header: 'نوع', render: (r) => r.voucherTypeName },
    { key: 'desc', header: 'شرح', render: (r) => r.description ?? '' },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.totalDebit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.totalCredit) },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${r.isBalanced ? 'badge-green' : 'badge-red'}`}>{r.statusName}</span> },
  ];

  return (
    <div>
      <PageHeader title="اسنادِ حسابداری" />

      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)' }}>
        <div className="field">
          <label className="label">از تاریخ</label>
          <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} placeholder="1405/01/01" />
        </div>
        <div className="field">
          <label className="label">تا تاریخ</label>
          <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} placeholder="1405/12/29" />
        </div>
        <button className="btn btn-primary" onClick={() => search(1)} disabled={loading}>
          {loading ? 'در حالِ جست‌وجو…' : 'جست‌وجو'}
        </button>
      </div>

      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {result && !error && (
        <>
          <DataTable columns={columns} rows={result.items} rowKey={(r) => r.id} emptyText="سندی یافت نشد." />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>
            <span>
              صفحهٔ {numberFormat.format(result.pageNumber)} از {numberFormat.format(result.totalPages)} — کل: {money(result.totalCount)} سند
            </span>
            <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
              <button className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => search(page - 1)}>
                قبلی
              </button>
              <button className="btn btn-secondary btn-sm" disabled={page >= result.totalPages} onClick={() => search(page + 1)}>
                بعدی
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
