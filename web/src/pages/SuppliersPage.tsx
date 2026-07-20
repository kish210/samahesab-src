import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface SupplierRow {
  id: number;
  code: string;
  name: string;
  mobile: string;
  city: string;
  balance: number;
  isActive: boolean;
}

export function SuppliersPage() {
  const [rows, setRows] = useState<SupplierRow[]>([]);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const qs = debouncedSearch.trim() ? `?search=${encodeURIComponent(debouncedSearch.trim())}` : '';
    apiGet<SupplierRow[]>(`/api/suppliers${qs}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ تأمین‌کنندگان.'))
      .finally(() => setLoading(false));
  }, [debouncedSearch]);

  /** U-WEB-DEACTIVATE — حذفِ واقعی نیست (فاکتورهایِ خریدِ تاریخی به همین Party ارجاع می‌دهند)،
   * فقط غیرفعال/فعال‌سازی. */
  async function toggleActive(r: SupplierRow) {
    try {
      await apiPost(`/api/suppliers/${r.id}/${r.isActive ? 'deactivate' : 'activate'}`);
      setRows((prev) => prev.map((x) => (x.id === r.id ? { ...x, isActive: !x.isActive } : x)));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    }
  }

  const columns: Column<SupplierRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => <Link to={`/parties/${r.id}`}>{r.name}</Link> },
    { key: 'mobile', header: 'موبایل', render: (r) => <span style={{ direction: 'ltr' }}>{r.mobile}</span> },
    { key: 'city', header: 'شهر', render: (r) => r.city },
    {
      key: 'balance', header: 'مانده (ریال)', numeric: true,
      render: (r) => <span style={{ fontWeight: 600, color: r.balance > 0 ? 'var(--danger-700)' : 'var(--text-strong)' }}>{money(r.balance)}</span>,
    },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => (!r.isActive ? <span className="badge badge-gray">غیرفعال</span> : null),
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <Link to={`/parties/${r.id}/edit`} className="btn btn-ghost btn-sm">ویرایش</Link>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleActive(r)}>
            {r.isActive ? 'غیرفعال‌سازی' : 'فعال‌سازی'}
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="تأمین‌کنندگان"
        actions={<Link to="/parties/new?role=supplier" className="btn btn-primary btn-sm">تأمین‌کنندهٔ نو</Link>}
      />
      <div className="field" style={{ maxWidth: 320, marginBottom: 'var(--space-4)' }}>
        <input className="input" placeholder="جست‌وجو بر اساسِ نام/کد/موبایل…" value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="تأمین‌کننده‌ای یافت نشد." />}
    </div>
  );
}
