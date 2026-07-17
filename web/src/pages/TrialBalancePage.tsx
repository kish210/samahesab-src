import { useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface TrialBalanceRow {
  code: string;
  name: string;
  debit: number;
  credit: number;
  balance: number;
  accountId: number;
}

export function TrialBalancePage() {
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [rows, setRows] = useState<TrialBalanceRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function search() {
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<TrialBalanceRow[]>(`/api/reports/trial-balance?from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}`);
      setRows(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تراز آزمایشی.');
    } finally {
      setLoading(false);
    }
  }

  const columns: Column<TrialBalanceRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => <Link to={`/general-ledger?accountId=${r.accountId}`}>{r.name}</Link> },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.debit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.credit) },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600 }}>{money(r.balance)}</span> },
  ];

  return (
    <div>
      <PageHeader title="تراز آزمایشی" />
      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)' }}>
        <div className="field">
          <label className="label">از تاریخ</label>
          <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="field">
          <label className="label">تا تاریخ</label>
          <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn btn-primary" onClick={search} disabled={loading}>
          {loading ? 'در حالِ جست‌وجو…' : 'نمایش'}
        </button>
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {rows && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.accountId} emptyText="ردیفی یافت نشد." />}
    </div>
  );
}
