import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';

interface AccountDto {
  id: number;
  code: string;
  name: string;
  isLeaf: boolean;
}

interface LedgerRow {
  date: string;
  voucherNumber: string;
  code: string;
  name: string;
  description: string | null;
  debit: number;
  credit: number;
  balance: number;
}

export function GeneralLedgerPage() {
  const [searchParams] = useSearchParams();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [accountId, setAccountId] = useState<number | null>(() => {
    const fromUrl = searchParams.get('accountId');
    return fromUrl ? Number(fromUrl) : null;
  });
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [rows, setRows] = useState<LedgerRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    apiGet<AccountDto[]>('/api/accounts?leafOnly=true').then(setAccounts).catch(() => {});
  }, []);

  async function search() {
    if (!accountId) {
      setError('انتخابِ حساب الزامی است.');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<LedgerRow[]>(
        `/api/reports/general-ledger?from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}&accountId=${accountId}`,
      );
      setRows(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ دفترِ کل/معین.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (accountId) search();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const columns: Column<LedgerRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'num', header: 'شمارهٔ سند', render: (r) => r.voucherNumber },
    { key: 'desc', header: 'شرح', render: (r) => r.description ?? '' },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.debit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.credit) },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600 }}>{money(r.balance)}</span> },
  ];

  return (
    <div>
      <PageHeader title="دفترِ کل / معین" />
      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)', flexWrap: 'wrap' }}>
        <div className="field" style={{ minWidth: 240 }}>
          <label className="label">حساب</label>
          <SearchSelect
            options={accounts.map((a) => ({ id: a.id, label: a.name, sublabel: a.code }))}
            value={accountId}
            onChange={setAccountId}
            placeholder="جست‌وجویِ حساب…"
          />
        </div>
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
      {rows && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => `${r.date}-${r.voucherNumber}-${r.debit}-${r.credit}`} emptyText="ردیفی یافت نشد." />}
    </div>
  );
}
