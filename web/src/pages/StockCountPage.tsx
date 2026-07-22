import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface StockCountRow { id: number; warehouseId: number; warehouseName: string; date: string; status: string }
interface StockCountLine { productId: number; productName: string; systemQty: number; countedQty: number; variance: number }
interface StockCountDetail { id: number; warehouseId: number; date: string; status: string; lineCount: number; varianceCount: number; lines: StockCountLine[] }
interface WarehouseRow { id: number; name: string }

const numberFormat = new Intl.NumberFormat('fa-IR');

/** U-WEB-STOCKCOUNT — انبارگردانیِ سندی. Application از قبل کامل بود (شروع/ثبتِ شمارش/نهایی‌سازی)
 * ولی هیچ کوئریِ فهرست‌کننده‌ای نداشت (فقط جزئیاتِ تکی) و هیچ صفحهٔ وبی نداشت. */
export function StockCountPage() {
  const [sessions, setSessions] = useState<StockCountRow[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseRow[]>([]);
  const [selected, setSelected] = useState<StockCountDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  function loadSessions() {
    apiGet<StockCountRow[]>('/api/stockcount').then(setSessions)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ انبارگردانی.'));
  }
  useEffect(loadSessions, []);
  useEffect(() => {
    apiGet<WarehouseRow[]>('/api/warehouse/list').then(setWarehouses).catch(() => {});
  }, []);

  function loadDetail(id: number) {
    apiGet<StockCountDetail>(`/api/stockcount/${id}`).then(setSelected)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ سند.'));
  }

  // ── شروعِ انبارگردانیِ نو ──
  const [showNew, setShowNew] = useState(false);
  const [newWarehouseId, setNewWarehouseId] = useState<number | ''>('');
  const [newDate, setNewDate] = useState(todayJalaliString());

  async function startCount() {
    if (!newWarehouseId) { setError('انتخابِ انبار الزامی است.'); return; }
    try {
      const r = await apiPost<{ sessionId: number }>('/api/stockcount/start', { warehouseId: newWarehouseId, date: newDate });
      setNotice('انبارگردانی آغاز شد.');
      setShowNew(false);
      loadSessions();
      loadDetail(r.sessionId);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'شروعِ انبارگردانی ناموفق بود.'); }
  }

  async function setCounted(productId: number, qty: number) {
    if (!selected) return;
    try {
      await apiPost(`/api/stockcount/${selected.id}/count`, { productId, countedQty: qty });
      loadDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ شمارش ناموفق بود.'); }
  }

  async function postCount() {
    if (!selected) return;
    try {
      const r = await apiPost<{ adjustedItems: number; totalVariance: number }>(`/api/stockcount/${selected.id}/post`, {});
      setNotice(`نهایی‌سازی انجام شد — ${r.adjustedItems} قلم تعدیل شد.`);
      loadSessions();
      loadDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'نهایی‌سازی ناموفق بود.'); }
  }

  const sessionColumns: Column<StockCountRow>[] = [
    { key: 'id', header: '#', render: (r) => <a onClick={() => loadDetail(r.id)} style={{ cursor: 'pointer' }}>{r.id}</a> },
    { key: 'warehouse', header: 'انبار', render: (r) => r.warehouseName },
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => <span className={`badge ${r.status === 'نهایی‌شده' ? 'badge-green' : 'badge-yellow'}`}>{r.status}</span>,
    },
  ];

  const lineColumns: Column<StockCountLine>[] = [
    { key: 'product', header: 'کالا', render: (r) => r.productName },
    { key: 'system', header: 'موجودیِ سیستمی', numeric: true, render: (r) => numberFormat.format(r.systemQty) },
    {
      key: 'counted', header: 'تعدادِ شمرده‌شده', numeric: true,
      render: (r) => selected?.status === 'نهایی‌شده' ? numberFormat.format(r.countedQty) : (
        <input className="input" type="number" defaultValue={r.countedQty} style={{ width: 100, direction: 'ltr' }}
          onBlur={(e) => { const v = Number(e.target.value); if (!Number.isNaN(v) && v !== r.countedQty) setCounted(r.productId, v); }} />
      ),
    },
    {
      key: 'variance', header: 'مغایرت', numeric: true,
      render: (r) => <span style={{ color: r.variance !== 0 ? 'var(--danger-700)' : undefined, fontWeight: r.variance !== 0 ? 600 : undefined }}>
        {numberFormat.format(r.variance)}
      </span>,
    },
  ];

  return (
    <div>
      <PageHeader title="انبارگردانی" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      {!selected && (
        <div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNew((v) => !v)}>انبارگردانیِ نو</button>
          </div>
          {showNew && (
            <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)', maxWidth: 480 }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
                <div className="field">
                  <label className="label">انبار</label>
                  <select className="select" value={newWarehouseId} onChange={(e) => setNewWarehouseId(e.target.value ? Number(e.target.value) : '')}>
                    <option value="">— انتخاب —</option>
                    {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                  </select>
                </div>
                <JalaliDateInput value={newDate} onChange={setNewDate} label="تاریخ" />
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button type="button" className="btn btn-primary btn-sm" onClick={startCount}>شروع (snapshotِ موجودی)</button>
              </div>
            </div>
          )}
          <DataTable columns={sessionColumns} rows={sessions} rowKey={(r) => r.id} emptyText="انبارگردانی‌ای ثبت نشده." />
        </div>
      )}

      {selected && (
        <div>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => setSelected(null)}>← بازگشت به فهرست</button>
          <div className="gh" style={{ marginTop: 'var(--space-2)' }}>
            سندِ انبارگردانیِ #{selected.id} — {selected.date}
            <span className={`badge ${selected.status === 'نهایی‌شده' ? 'badge-green' : 'badge-yellow'}`} style={{ marginInlineStart: 8 }}>{selected.status}</span>
          </div>
          <div className="sumbar" style={{ margin: 'var(--space-3) 0' }}>
            <span>تعدادِ اقلام: {selected.lineCount}</span>
            <span>اقلامِ مغایر: {selected.varianceCount}</span>
          </div>
          <DataTable columns={lineColumns} rows={selected.lines} rowKey={(r) => r.productId} emptyText="قلمی نیست." />
          {selected.status !== 'نهایی‌شده' && (
            <div style={{ marginTop: 'var(--space-3)' }}>
              <button type="button" className="btn btn-primary btn-sm" onClick={postCount}>نهایی‌سازی (تبدیلِ مغایرت به تعدیلِ موجودی)</button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
