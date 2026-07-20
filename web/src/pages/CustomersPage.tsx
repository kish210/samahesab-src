import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader } from '../components/PageHeader';

interface CustomerRow {
  id: number;
  code: string;
  name: string;
  mobile: string;
  balance: number;
  priceLevel: string;
  isActive: boolean;
}

const numberFormat = new Intl.NumberFormat('fa-IR');

export function CustomersPage() {
  const [rows, setRows] = useState<CustomerRow[]>([]);
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const handle = setTimeout(() => {
      setLoading(true);
      const qs = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
      apiGet<CustomerRow[]>(`/api/customers${qs}`)
        .then(setRows)
        .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ مشتریان.'))
        .finally(() => setLoading(false));
    }, 250);
    return () => clearTimeout(handle);
  }, [search]);

  /** U-WEB-DEACTIVATE — حذفِ واقعی نیست (فاکتورهایِ تاریخی به همین Party ارجاع می‌دهند)،
   * فقط غیرفعال/فعال‌سازی؛ رکورد در فهرست می‌ماند. */
  async function toggleActive(r: CustomerRow) {
    try {
      await apiPost(`/api/customers/${r.id}/${r.isActive ? 'deactivate' : 'activate'}`);
      setRows((prev) => prev.map((x) => (x.id === r.id ? { ...x, isActive: !x.isActive } : x)));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    }
  }

  return (
    <div>
      <PageHeader
        title="مشتریان"
        actions={<Link to="/parties/new" className="btn btn-primary btn-sm">مشتریِ نو</Link>}
      />

      <div className="field" style={{ maxWidth: 320, marginBottom: 'var(--space-4)' }}>
        <input
          className="input"
          placeholder="جست‌وجو بر اساسِ نام/کد/موبایل…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {error && <div style={{ color: 'var(--danger-700)' }}>{error}</div>}
      {loading && !error && <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>}

      {!loading && !error && (
        <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ background: 'var(--gray-50)', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '10px 12px', textAlign: 'start', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>کد</th>
                <th style={{ padding: '10px 12px', textAlign: 'start', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>نام</th>
                <th style={{ padding: '10px 12px', textAlign: 'start', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>موبایل</th>
                <th className="num" style={{ padding: '10px 12px', textAlign: 'start', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>مانده (ریال)</th>
                <th style={{ padding: '10px 12px' }}></th>
                <th style={{ padding: '10px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} style={{ borderBottom: '1px solid var(--gray-100)' }}>
                  <td style={{ padding: '10px 12px' }}>{r.code}</td>
                  <td style={{ padding: '10px 12px' }}>
                    <Link to={`/customers/${r.id}`}>{r.name}</Link>
                  </td>
                  <td className="num" style={{ padding: '10px 12px', direction: 'ltr', textAlign: 'end' }}>
                    {r.mobile}
                  </td>
                  <td
                    className="num"
                    style={{
                      padding: '10px 12px',
                      textAlign: 'end',
                      fontWeight: 600,
                      color: r.balance > 0 ? 'var(--danger-700)' : 'var(--text-strong)',
                    }}
                  >
                    {numberFormat.format(r.balance)}
                  </td>
                  <td style={{ padding: '10px 12px' }}>
                    {!r.isActive && <span className="badge badge-gray">غیرفعال</span>}
                  </td>
                  <td style={{ padding: '10px 12px', display: 'flex', gap: 6 }}>
                    <Link to={`/parties/${r.id}/edit`} className="btn btn-ghost btn-sm">ویرایش</Link>
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleActive(r)}>
                      {r.isActive ? 'غیرفعال‌سازی' : 'فعال‌سازی'}
                    </button>
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--text-muted)' }}>
                    مشتری‌ای یافت نشد.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
