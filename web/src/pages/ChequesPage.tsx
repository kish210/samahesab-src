import { useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ChequeBoardDto {
  id: number;
  chequeNumber: string;
  bankName: string;
  amount: number;
  dueDate: string;
  type: 'دریافتی' | 'پرداختی';
  dueState: 'Overdue' | 'DueToday' | 'Upcoming';
}

const STATE_LABEL: Record<ChequeBoardDto['dueState'], { text: string; cls: string; dot: string }> = {
  Overdue: { text: 'سررسیدگذشته', cls: 'badge-red', dot: 'var(--danger-500)' },
  DueToday: { text: 'سررسیدِ امروز', cls: 'badge-amber', dot: 'var(--warning-500)' },
  Upcoming: { text: 'آینده', cls: 'badge-gray', dot: 'var(--gray-400)' },
};

/** تابلویِ چک — پورتِ ساختاریِ design-system/screens/cheques.html: کاشی‌هایِ آماریِ کلیک‌پذیر
 * (فیلترِ وضعیت) + چیپِ نوع (دریافتی/پرداختی) + گریدِ چک + اقداماتِ واقعیِ تغییرِ وضعیت
 * (وصول/واگذاری‌به‌بانک/برگشت) رویِ ChangeChequeStatusCommandِ ازقبل‌موجود که هیچ UI‌ای صدایش نمی‌زد. */
export function ChequesPage() {
  const [rows, setRows] = useState<ChequeBoardDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [typeFilter, setTypeFilter] = useState<'همه' | 'دریافتی' | 'پرداختی'>('همه');
  const [stateFilter, setStateFilter] = useState<ChequeBoardDto['dueState'] | 'همه'>('همه');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  function load() {
    setLoading(true);
    apiGet<ChequeBoardDto[]>(`/api/cheques/board?today=${encodeURIComponent(todayJalaliString())}`)
      .then((data) => { setRows(data); setSelectedId(null); })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تابلویِ چک.'))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  const filtered = useMemo(
    () => rows.filter((r) => (typeFilter === 'همه' || r.type === typeFilter) && (stateFilter === 'همه' || r.dueState === stateFilter)),
    [rows, typeFilter, stateFilter],
  );
  const stats = useMemo(() => {
    const of = (state: ChequeBoardDto['dueState'] | 'همه') => {
      const list = state === 'همه' ? rows : rows.filter((r) => r.dueState === state);
      return { count: list.length, amount: list.reduce((s, r) => s + r.amount, 0) };
    };
    return { all: of('همه'), overdue: of('Overdue'), dueToday: of('DueToday'), upcoming: of('Upcoming') };
  }, [rows]);

  // ChequeAction (بک‌اند): Clear=0, Return=1, Transfer=2 — سریال‌سازیِ JSONِ سرور برایِ enum عددی
  // است (بدونِ JsonStringEnumConverter)، پس باید همان مقدارِ عددی ارسال شود، نه نامِ رشته‌ای.
  const ACTION_CODE = { Clear: 0, Return: 1, Transfer: 2 } as const;

  async function act(action: keyof typeof ACTION_CODE) {
    if (!selectedId) return;
    let returnReason: string | undefined;
    if (action === 'Return') {
      returnReason = window.prompt('علتِ برگشتِ چک؟') ?? undefined;
      if (!returnReason) return;
    }
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/cheques/${selectedId}/status`, { action: ACTION_CODE[action], date: todayJalaliString(), returnReason });
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیتِ چک ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  const columns: Column<ChequeBoardDto>[] = [
    { key: 'num', header: 'شمارهٔ چک', render: (r) => r.chequeNumber },
    { key: 'bank', header: 'بانک', render: (r) => r.bankName },
    { key: 'type', header: 'نوع', render: (r) => <span className={`badge ${r.type === 'دریافتی' ? 'badge-blue' : 'badge-gray'}`}>{r.type}</span> },
    { key: 'due', header: 'سررسید', render: (r) => r.dueDate },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    {
      key: 'state', header: 'وضعیت',
      render: (r) => <span className={`badge ${STATE_LABEL[r.dueState].cls}`}>{STATE_LABEL[r.dueState].text}</span>,
    },
  ];

  return (
    <div>
      <PageHeader title="تابلویِ چک" actions={
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-primary btn-sm" disabled={!selectedId || busy} onClick={() => act('Clear')}>وصول</button>
          <button type="button" className="btn btn-secondary btn-sm" disabled={!selectedId || busy} onClick={() => act('Transfer')}>واگذاری به بانک</button>
          <button type="button" className="btn btn-secondary btn-sm" style={{ color: 'var(--danger-700)' }} disabled={!selectedId || busy} onClick={() => act('Return')}>برگشتِ چک</button>
        </div>
      } />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, marginBottom: 'var(--space-3)' }}>
        {([
          ['همه', 'همهٔ چک‌ها', stats.all, 'var(--blue-600)'],
          ['Overdue', 'سررسیدگذشته', stats.overdue, 'var(--danger-500)'],
          ['DueToday', 'سررسیدِ امروز', stats.dueToday, 'var(--warning-500)'],
          ['Upcoming', 'آینده', stats.upcoming, 'var(--gray-400)'],
        ] as const).map(([key, label, s, dot]) => (
          <div key={key}
            onClick={() => setStateFilter(key as typeof stateFilter)}
            style={{
              background: 'var(--bg-surface)', cursor: 'pointer', borderRadius: 'var(--radius-md)', padding: '10px 12px',
              border: `1px solid ${stateFilter === key ? 'var(--blue-600)' : 'var(--border)'}`,
              boxShadow: stateFilter === key ? 'var(--ring-focus)' : 'none',
            }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 8, height: 8, borderRadius: 99, background: dot, display: 'inline-block' }} />
              {label}
            </div>
            <div style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{s.count} فقره</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-muted)', marginTop: 1 }}>{money(s.amount)} ریال</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 'var(--space-3)' }}>
        {(['همه', 'دریافتی', 'پرداختی'] as const).map((t) => (
          <button key={t} type="button" className={`chip${typeFilter === t ? ' active' : ''}`} onClick={() => setTypeFilter(t)}>
            {t}
          </button>
        ))}
      </div>

      {loading && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && (
        <DataTable
          columns={columns}
          rows={filtered}
          rowKey={(r) => r.id}
          emptyText="چکی در جریان نیست."
          selectedKey={selectedId}
          onRowClick={(r) => setSelectedId(r.id)}
        />
      )}
    </div>
  );
}
