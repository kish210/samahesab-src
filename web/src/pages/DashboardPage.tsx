import { useEffect, useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { numberFormat } from '../lib/format';

interface DashboardSummary {
  totalProducts: number;
  totalCustomers: number;
  lowStock: number;
  receivable: number;
  payable: number;
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="card" style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-4)' }}>
      <div style={{ fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>{label}</div>
      <div className="num" style={{ fontSize: 'var(--text-2xl)', fontWeight: 700, color: 'var(--text-strong)', marginTop: 4 }}>
        {value}
      </div>
    </div>
  );
}

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiGet<DashboardSummary>('/api/dashboard/summary')
      .then(setSummary)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ داشبورد.'));
  }, []);

  if (error) return <div style={{ color: 'var(--danger-700)' }}>{error}</div>;
  if (!summary) return <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>;

  return (
    <div>
      <h1 style={{ marginBottom: 'var(--space-5)' }}>داشبورد</h1>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 'var(--space-4)' }}>
        <StatCard label="تعدادِ کالاها" value={numberFormat.format(summary.totalProducts)} />
        <StatCard label="تعدادِ مشتریان" value={numberFormat.format(summary.totalCustomers)} />
        <StatCard label="کسریِ موجودی" value={numberFormat.format(summary.lowStock)} />
        <StatCard label="جمعِ دریافتنی" value={numberFormat.format(summary.receivable) + ' ریال'} />
        <StatCard label="جمعِ پرداختنی" value={numberFormat.format(summary.payable) + ' ریال'} />
      </div>
    </div>
  );
}
