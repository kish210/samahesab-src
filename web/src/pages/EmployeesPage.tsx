import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, apiFetch, ApiError } from '../api/client';
import { money } from '../lib/format';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface EmployeeRow {
  id: number;
  code: string;
  fullName: string;
  nationalCode: string;
  mobile: string | null;
  baseSalary: number;
  contractType: string;
  isActive: boolean;
}

/** U-WEB-HR — فهرستِ پرسنل. CQRSِ کامل (`GetEmployeesQuery`) از قبل در Application/HRM بود
 * ولی هیچ endpoint/صفحهٔ وبی صدایش نمی‌زد. */
export function EmployeesPage() {
  const [rows, setRows] = useState<EmployeeRow[]>([]);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const qs = new URLSearchParams();
    if (debouncedSearch.trim()) qs.set('search', debouncedSearch.trim());
    if (includeInactive) qs.set('includeInactive', 'true');
    apiGet<EmployeeRow[]>(`/api/employees?${qs.toString()}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ پرسنل.'))
      .finally(() => setLoading(false));
  }, [debouncedSearch, includeInactive]);

  async function remove(r: EmployeeRow) {
    if (!window.confirm(`حذفِ «${r.fullName}»؟`)) return;
    try {
      await apiFetch(`/api/employees/${r.id}`, { method: 'DELETE' });
      setRows((prev) => prev.filter((x) => x.id !== r.id));
    } catch (e) {
      // ممکن است بجایِ حذف، فقط غیرفعال شده باشد (سابقهٔ فیش/تردد) — پیامِ سرور را نشان بده.
      setError(e instanceof ApiError ? e.message : 'حذفِ کارمند ناموفق بود.');
    }
  }

  const columns: Column<EmployeeRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => <Link to={`/hr/employees/${r.id}/edit`}>{r.fullName}</Link> },
    { key: 'nationalCode', header: 'کدِ ملی', render: (r) => <span style={{ direction: 'ltr' }}>{r.nationalCode}</span> },
    { key: 'mobile', header: 'موبایل', render: (r) => <span style={{ direction: 'ltr' }}>{r.mobile ?? '—'}</span> },
    { key: 'contractType', header: 'نوعِ قرارداد', render: (r) => r.contractType },
    { key: 'baseSalary', header: 'حقوقِ پایه', numeric: true, render: (r) => money(r.baseSalary) },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => <span className={`badge ${r.isActive ? 'badge-green' : 'badge-gray'}`}>{r.isActive ? 'فعال' : 'غیرفعال'}</span>,
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <Link to={`/hr/employees/${r.id}/edit`} className="btn btn-ghost btn-sm">ویرایش</Link>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => remove(r)}>حذف</button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="پرسنل"
        actions={<Link to="/hr/employees/new" className="btn btn-primary btn-sm">کارمندِ نو</Link>}
      />
      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'center', marginBottom: 'var(--space-4)' }}>
        <div className="field" style={{ maxWidth: 320, marginBottom: 0 }}>
          <input className="input" placeholder="جست‌وجو بر اساسِ نام/کد/کدِ ملی…" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
          <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />
          نمایشِ غیرفعال‌ها
        </label>
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="کارمندی یافت نشد." />}
    </div>
  );
}
