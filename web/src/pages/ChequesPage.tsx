import { useEffect, useState } from 'react';
import { apiGet, ApiError } from '../api/client';
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

const stateLabel: Record<ChequeBoardDto['dueState'], { text: string; cls: string }> = {
  Overdue: { text: 'سررسیدگذشته', cls: 'badge-red' },
  DueToday: { text: 'سررسیدِ امروز', cls: 'badge-amber' },
  Upcoming: { text: 'آینده', cls: 'badge-gray' },
};

export function ChequesPage() {
  const [rows, setRows] = useState<ChequeBoardDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<ChequeBoardDto[]>(`/api/cheques/board?today=${encodeURIComponent(todayJalaliString())}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تابلویِ چک.'))
      .finally(() => setLoading(false));
  }, []);

  const columns: Column<ChequeBoardDto>[] = [
    { key: 'num', header: 'شمارهٔ چک', render: (r) => r.chequeNumber },
    { key: 'bank', header: 'بانک', render: (r) => r.bankName },
    { key: 'type', header: 'نوع', render: (r) => <span className={`badge ${r.type === 'دریافتی' ? 'badge-blue' : 'badge-gray'}`}>{r.type}</span> },
    { key: 'due', header: 'سررسید', render: (r) => r.dueDate },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    {
      key: 'state', header: 'وضعیت',
      render: (r) => <span className={`badge ${stateLabel[r.dueState].cls}`}>{stateLabel[r.dueState].text}</span>,
    },
  ];

  return (
    <div>
      <PageHeader title="تابلویِ چک" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="چکی در جریان نیست." />}
    </div>
  );
}
