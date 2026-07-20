import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ProductRow {
  id: number;
  code: string;
  barcode: string;
  name: string;
  salePrice: number;
  purchasePrice: number;
  minStock: number;
  isActive: boolean;
  isLowStock: boolean;
}

export function ProductsPage() {
  const [rows, setRows] = useState<ProductRow[]>([]);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const qs = debouncedSearch.trim() ? `?search=${encodeURIComponent(debouncedSearch.trim())}` : '';
    apiGet<ProductRow[]>(`/api/products/list${qs}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ کالاها.'))
      .finally(() => setLoading(false));
  }, [debouncedSearch]);

  /** U-WEB-DEACTIVATE — حذفِ واقعی نیست (اقلامِ فاکتورهایِ تاریخی به کالا ارجاع می‌دهند)، فقط
   * غیرفعال/فعال‌سازی؛ رکورد در فهرست می‌ماند (با نشانگرِ وضعیت) نه اینکه ناپدید شود. */
  async function toggleActive(r: ProductRow) {
    try {
      await apiPost(`/api/products/${r.id}/${r.isActive ? 'deactivate' : 'activate'}`);
      setRows((prev) => prev.map((x) => (x.id === r.id ? { ...x, isActive: !x.isActive } : x)));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    }
  }

  const columns: Column<ProductRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => <Link to={`/products/${r.id}`}>{r.name}</Link> },
    { key: 'salePrice', header: 'قیمتِ فروش', numeric: true, render: (r) => money(r.salePrice) },
    { key: 'purchasePrice', header: 'قیمتِ خرید', numeric: true, render: (r) => money(r.purchasePrice) },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => (
        <span className={`badge ${!r.isActive ? 'badge-gray' : r.isLowStock ? 'badge-red' : 'badge-green'}`}>
          {!r.isActive ? 'غیرفعال' : r.isLowStock ? 'کسریِ موجودی' : 'موجود'}
        </span>
      ),
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <Link to={`/products/${r.id}/edit`} className="btn btn-ghost btn-sm">ویرایش</Link>
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
        title="کالاها"
        actions={<Link to="/products/new" className="btn btn-primary btn-sm">کالایِ نو</Link>}
      />
      <div className="field" style={{ maxWidth: 320, marginBottom: 'var(--space-4)' }}>
        <input className="input" placeholder="جست‌وجو بر اساسِ نام/کد/بارکد…" value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="کالایی یافت نشد." />}
    </div>
  );
}
