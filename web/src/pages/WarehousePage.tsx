import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
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

interface TransferLine {
  productId: number | null;
  quantity: string;
}

function emptyLine(): TransferLine {
  return { productId: null, quantity: '' };
}

export function WarehousePage() {
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [rows, setRows] = useState<StockRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [transferOpen, setTransferOpen] = useState(false);
  const [toWarehouseId, setToWarehouseId] = useState<number | null>(null);
  const [transferDate, setTransferDate] = useState(todayJalaliString());
  const [deliveredBy, setDeliveredBy] = useState('');
  const [receivedBy, setReceivedBy] = useState('');
  const [description, setDescription] = useState('');
  const [lines, setLines] = useState<TransferLine[]>([emptyLine()]);
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

  function loadStock() {
    if (!warehouseId) return;
    setLoading(true);
    apiGet<StockRow[]>(`/api/warehouse/stock?warehouseId=${warehouseId}`)
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ موجودی.'))
      .finally(() => setLoading(false));
  }

  useEffect(loadStock, [warehouseId]);

  function updateLine(i: number, patch: Partial<TransferLine>) {
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  }
  function removeLine(i: number) {
    setLines((prev) => prev.filter((_, idx) => idx !== i));
  }
  function addLine() {
    setLines((prev) => [...prev, emptyLine()]);
  }
  function resetForm() {
    setLines([emptyLine()]);
    setDeliveredBy('');
    setReceivedBy('');
    setDescription('');
  }

  const stockByProduct = new Map(rows.map((r) => [r.productId, r]));
  const linesWithData = lines.filter((l) => l.productId && Number(l.quantity) > 0);
  const insufficientLines = linesWithData.filter((l) => {
    const stock = stockByProduct.get(l.productId!);
    return !stock || stock.quantity < Number(l.quantity);
  });
  const totalQty = linesWithData.reduce((s, l) => s + Number(l.quantity), 0);
  const totalValue = linesWithData.reduce((s, l) => {
    const stock = stockByProduct.get(l.productId!);
    return s + Number(l.quantity) * (stock?.averageCost ?? 0);
  }, 0);

  async function submitTransfer(e: React.FormEvent) {
    e.preventDefault();
    setTransferMsg(null);
    if (!warehouseId || !toWarehouseId) {
      setTransferMsg({ kind: 'error', text: 'انبارِ مبدأ و مقصد الزامی‌اند.' });
      return;
    }
    if (linesWithData.length === 0) {
      setTransferMsg({ kind: 'error', text: 'حداقل یک ردیفِ کالا با مقدارِ معتبر لازم است.' });
      return;
    }
    if (insufficientLines.length > 0) {
      setTransferMsg({ kind: 'error', text: 'موجودیِ برخی از ردیف‌ها کافی نیست.' });
      return;
    }

    setSubmitting(true);
    const noteParts = [description, deliveredBy && `تحویل‌دهنده: ${deliveredBy}`, receivedBy && `تحویل‌گیرنده: ${receivedBy}`].filter(Boolean);
    const note = noteParts.length > 0 ? noteParts.join(' — ') : 'انتقال از کلاینتِ وب';
    try {
      // بک‌اند (TransferStockCommand) فقط یک کالا در هر فراخوانی می‌پذیرد — این حواله با
      // فراخوانیِ پیاپیِ همان Commandِ تک‌کالاییِ ازقبل‌موجود برایِ هر ردیف پیاده شده است.
      for (const line of linesWithData) {
        await apiPost('/api/warehouse/transfer', {
          fromWarehouseId: warehouseId,
          toWarehouseId,
          productId: line.productId,
          quantity: Number(line.quantity),
          date: transferDate,
          description: note,
        });
      }
      setTransferMsg({ kind: 'success', text: `حواله با موفقیت ثبت شد — ${linesWithData.length} ردیف، ${money(totalQty)} کالا.` });
      resetForm();
      loadStock();
    } catch (err) {
      setTransferMsg({ kind: 'error', text: err instanceof ApiError ? err.message : 'ثبتِ حواله ناموفق بود (ممکن است بخشی از ردیف‌ها قبلاً ثبت شده باشند).' });
      loadStock();
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
            {transferOpen ? 'بستنِ فرمِ حواله' : '+ حوالهٔ انتقال'}
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
        <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="gh">حوالهٔ انتقالِ بینِ انبار</div>
          <form onSubmit={submitTransfer} className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)' }}>
              <div className="field">
                <label className="label">تاریخ</label>
                <input className="input" value={transferDate} onChange={(e) => setTransferDate(e.target.value)} placeholder="1405/04/26" />
              </div>
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
                <label className="label">تحویل‌دهنده</label>
                <input className="input" value={deliveredBy} onChange={(e) => setDeliveredBy(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">تحویل‌گیرنده</label>
                <input className="input" value={receivedBy} onChange={(e) => setReceivedBy(e.target.value)} />
              </div>
            </div>

            <div className="dgrid-wrap">
              <table className="dgrid">
                <thead>
                  <tr>
                    <th>کالا</th>
                    <th style={{ width: 110 }} className="num">موجودیِ مبدأ</th>
                    <th style={{ width: 110 }} className="num">مقدار</th>
                    <th style={{ width: 36 }} className="c" />
                  </tr>
                </thead>
                <tbody>
                  {lines.map((l, i) => {
                    const stock = l.productId ? stockByProduct.get(l.productId) : undefined;
                    const insufficient = l.productId && Number(l.quantity) > 0 && (!stock || stock.quantity < Number(l.quantity));
                    return (
                      <tr key={i}>
                        <td style={{ minWidth: 220 }}>
                          <SearchSelect
                            options={rows.map((r) => ({ id: r.productId, label: r.name, sublabel: r.code }))}
                            value={l.productId}
                            onChange={(id) => updateLine(i, { productId: id })}
                            placeholder="جست‌وجویِ کالا…"
                          />
                        </td>
                        <td className="num mut">{stock ? money(stock.quantity) : '—'}</td>
                        <td className="num">
                          <input
                            className="input input-sm" type="number" min="0" step="any"
                            style={{ borderColor: insufficient ? 'var(--danger-500)' : undefined }}
                            value={l.quantity} onChange={(e) => updateLine(i, { quantity: e.target.value })}
                          />
                        </td>
                        <td className="c">
                          {lines.length > 1 && (
                            <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>✕</button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div>
              <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>+ ردیفِ نو</button>
            </div>

            <div className="field">
              <label className="label">توضیحات</label>
              <input className="input" value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>

            <div className={`sumbar ${insufficientLines.length > 0 ? 'bad' : 'ok'}`}>
              <b>{insufficientLines.length > 0 ? '✗ موجودیِ برخی ردیف‌ها کافی نیست' : '✓ موجودیِ همهٔ اقلام کافی است'}</b>
              <div className="grow" />
              <div className="s"><span className="l">تعدادِ ردیف</span><span className="v">{linesWithData.length}</span></div>
              <div className="s"><span className="l">جمعِ مقدار</span><span className="v">{money(totalQty)}</span></div>
              <div className="s"><span className="l">ارزشِ انتقال</span><span className="v">{money(totalValue)}</span></div>
            </div>

            <div>
              <button type="submit" className="btn btn-primary" disabled={submitting}>
                {submitting ? 'در حالِ ثبت…' : 'تأییدِ انتقال'}
              </button>
            </div>
            {transferMsg && <StatusMessage kind={transferMsg.kind}>{transferMsg.text}</StatusMessage>}
          </form>
        </div>
      )}

      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.productId} emptyText="موجودی‌ای یافت نشد." />}
    </div>
  );
}
