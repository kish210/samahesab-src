import { useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface BalanceSheetLine {
  code: string;
  name: string;
  amount: number;
}

interface BalanceSheetDto {
  assets: BalanceSheetLine[];
  liabilities: BalanceSheetLine[];
  equity: BalanceSheetLine[];
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  netProfit: number;
  isBalanced: boolean;
}

const lineColumns: Column<BalanceSheetLine>[] = [
  { key: 'code', header: 'کد', render: (r) => r.code },
  { key: 'name', header: 'نام', render: (r) => r.name },
  { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
];

function Section({ title, lines, total }: { title: string; lines: BalanceSheetLine[]; total: number }) {
  return (
    <div style={{ marginBottom: 'var(--space-5)' }}>
      <h3 style={{ marginBottom: 'var(--space-2)' }}>{title}</h3>
      <DataTable columns={lineColumns} rows={lines} rowKey={(r) => r.code} emptyText="ردیفی یافت نشد." />
      <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700, padding: '8px 12px', borderTop: '2px solid var(--border-strong)' }}>
        <span>جمعِ {title}</span>
        <span className="num">{money(total)}</span>
      </div>
    </div>
  );
}

/** ترازنامه — دارایی/بدهی/حقوقِ صاحبانِ سهام، متصل به endpointِ ازقبل‌موجودِ GetBalanceSheetQuery. */
export function BalanceSheetPage() {
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [data, setData] = useState<BalanceSheetDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function search() {
    setLoading(true);
    setError(null);
    try {
      const d = await apiGet<BalanceSheetDto>(`/api/reports/balance-sheet?from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}`);
      setData(d);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ ترازنامه.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <PageHeader title="ترازنامه" />
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

      {data && !error && (
        <>
          <Section title="دارایی‌ها" lines={data.assets} total={data.totalAssets} />
          <Section title="بدهی‌ها" lines={data.liabilities} total={data.totalLiabilities} />
          <Section title="حقوقِ صاحبانِ سهام" lines={data.equity} total={data.totalEquity} />
          <div style={{
            display: 'flex', justifyContent: 'space-between', fontWeight: 700, fontSize: 'var(--text-lg)',
            padding: '10px 12px', background: 'var(--bg-app)', borderRadius: 'var(--radius-md)',
          }}>
            <span>سودِ خالصِ دوره</span>
            <span className="num">{money(data.netProfit)}</span>
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <span className={`badge ${data.isBalanced ? 'badge-green' : 'badge-red'}`}>
              {data.isBalanced ? 'ترازنامه متوازن است' : 'ترازنامه متوازن نیست'}
            </span>
          </div>
        </>
      )}
    </div>
  );
}
