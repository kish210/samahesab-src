import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';

interface WarehouseDto {
  id: number;
  name: string;
}

interface StockRow {
  productId: number;
  code: string;
  name: string;
  quantity: number;
  averageCost: number;
  value: number;
}

export function WarehousePage() {
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [rows, setRows] = useState<StockRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [transferOpen, setTransferOpen] = useState(false);
  const [toWarehouseId, setToWarehouseId] = useState<number | null>(null);
  const [transferProductId, setTransferProductId] = useState<number | null>(null);
  const [transferQty, setTransferQty] = useState('');
  const [transferMsg, setTransferMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    apiGet<WarehouseDto[]>('/api/warehouse')
      .then((list) => {
        setWarehouses(list);
        if (list.length > 0) setWarehouseId(list[0].id);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ انبارها.'));
  }, []);

  useEffect(() => {
    if (!warehouseId) return;
    setLoading(true);
    apiGet<StockRow[]>(`/api/warehouse/stock?warehouseId=${warehouseId}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ موجودی.'))
      .finally(() => setLoading(false));
  }, [warehouseId]);

  async function submitTransfer(e: React.FormEvent) {
    e.preventDefault();
    setTransferMsg(null);
    if (!warehouseId || !toWarehouseId || !transferProductId || !transferQty) {
      setTransferMsg({ kind: 'error', text: 'همهٔ فیلدها الزامی‌اند.' });
      return;
    }
    setSubmitting(true);
    try {
      await apiPost('/api/warehouse/transfer', {
        fromWarehouseId: warehouseId,
        toWarehouseId,
        productId: transferProductId,
        quantity: Number(transferQty),
        date: new Date().toISOString().slice(0, 10).replace(/-/g, '/'),
        description: 'انتقال از کلاینتِ وب',
      });
      setTransferMsg({ kind: 'success', text: 'انتقال با موفقیت ثبت شد.' });
      setTransferQty('');
      setTransferProductId(null);
      const updated = await apiGet<StockRow[]>(`/api/warehouse/stock?warehouseId=${warehouseId}`);
      setRows(updated);
    } catch (err) {
      setTransferMsg({ kind: 'error', text: err instanceof ApiError ? err.message : 'انتقال ناموفق بود.' });
    } finally {
      setSubmitting(false);
    }
  }

  const columns: Column<StockRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => r.name },
    { key: 'quantity', header: 'موجودی', numeric: true, render: (r) => money(r.quantity) },
    { key: 'averageCost', header: 'بهایِ میانگین', numeric: true, render: (r) => money(r.averageCost) },
    { key: 'value', header: 'ارزش', numeric: true, render: (r) => money(r.value) },
  ];

  return (
    <div>
      <PageHeader
        title="انبار"
        actions={
          <button className="btn btn-primary btn-sm" onClick={() => setTransferOpen((o) => !o)}>
            {transferOpen ? 'بستنِ فرمِ انتقال' : 'انتقال بینِ انبار'}
          </button>
        }
      />

      <div className="field" style={{ maxWidth: 260, marginBottom: 'var(--space-4)' }}>
        <label className="label">انبار</label>
        <select className="select" value={warehouseId ?? ''} onChange={(e) => setWarehouseId(Number(e.target.value))}>
          {warehouses.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
      </div>

      {transferOpen && (
        <form
          onSubmit={submitTransfer}
          style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-4)', marginBottom: 'var(--space-4)', display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', alignItems: 'end' }}
        >
          <div className="field">
            <label className="label">به انبار</label>
            <select className="select" value={toWarehouseId ?? ''} onChange={(e) => setToWarehouseId(Number(e.target.value) || null)}>
              <option value="">— انتخاب —</option>
              {warehouses.filter((w) => w.id !== warehouseId).map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label className="label">کالا</label>
            <SearchSelect
              options={rows.map((r) => ({ id: r.productId, label: r.name, sublabel: r.code }))}
              value={transferProductId}
              onChange={setTransferProductId}
              placeholder="جست‌وجویِ کالا…"
            />
          </div>
          <div className="field">
            <label className="label">مقدار</label>
            <input className="input" type="number" min="0" step="any" value={transferQty} onChange={(e) => setTransferQty(e.target.value)} />
          </div>
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'در حالِ ثبت…' : 'ثبتِ انتقال'}
          </button>
          {transferMsg && (
            <div style={{ gridColumn: '1 / -1' }}>
              <StatusMessage kind={transferMsg.kind}>{transferMsg.text}</StatusMessage>
            </div>
          )}
        </form>
      )}

      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.productId} emptyText="موجودی‌ای یافت نشد." />}
    </div>
  );
}
