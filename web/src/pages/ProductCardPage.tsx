import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { StatusMessage } from '../components/PageHeader';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { Barcode } from '../components/Barcode';
import { jalaliOf } from '../lib/jalali';

interface ProductCardStockRow {
  warehouseName: string;
  quantity: number;
  isLow: boolean;
}

interface KardexRow {
  date: string;
  type: string;
  documentNumber: string | null;
  in: number;
  out: number;
  balance: number;
  unitCost: number;
  notes: string | null;
}

interface BatchRow {
  id: number;
  productId: number;
  batchNumber: string;
  productionDate: string | null;
  expiryDate: string | null;
  quantity: number;
  purchasePrice: number | null;
  notes: string | null;
}

interface SerialRow {
  id: number;
  productId: number;
  warehouseId: number | null;
  serialNumber: string;
  status: string;
  purchasePrice: number | null;
  purchaseDate: string | null;
  saleDate: string | null;
}

interface ProductCardDto {
  id: number;
  code: string;
  name: string;
  barcode: string | null;
  isActive: boolean;
  purchasePrice: number;
  salePrice: number;
  wholesalePrice: number;
  consumerPrice: number;
  taxRate: number;
  minStock: number;
  maxStock: number | null;
  reorderPoint: number | null;
  tracking: string;
  totalStock: number;
  warehouseStocks: ProductCardStockRow[];
  averageCost: number;
}

export function ProductCardPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [card, setCard] = useState<ProductCardDto | null>(null);
  const [kardex, setKardex] = useState<KardexRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // U-WEB-BATCHSERIAL — بچ/سریال: Application (GetBatchesQuery/SaveBatchCommand/GetSerialsQuery/
  // SaveSerialCommand) از قبل کامل بود، فقط در کارتِ کالا وایر نشده بود.
  const [batches, setBatches] = useState<BatchRow[]>([]);
  const [serials, setSerials] = useState<SerialRow[]>([]);
  const [showBatchForm, setShowBatchForm] = useState(false);
  const [batchForm, setBatchForm] = useState({ batchNumber: '', productionDate: '', expiryDate: '', quantity: 0, purchasePrice: '' });
  const [showSerialForm, setShowSerialForm] = useState(false);
  const [serialForm, setSerialForm] = useState({ serialNumber: '', purchasePrice: '', purchaseDate: '' });
  const [bsBusy, setBsBusy] = useState(false);
  const [bsError, setBsError] = useState<string | null>(null);

  function load() {
    if (!id) return;
    apiGet<ProductCardDto>(`/api/products/${id}/card`)
      .then(setCard)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کارتِ کالا.'));
    apiGet<KardexRow[]>(`/api/products/${id}/kardex`).then(setKardex).catch(() => {});
  }

  useEffect(load, [id]);

  function loadBatches() {
    if (!id) return;
    apiGet<BatchRow[]>(`/api/inventory/batches?productId=${id}`).then(setBatches).catch(() => {});
  }
  function loadSerials() {
    if (!id) return;
    apiGet<SerialRow[]>(`/api/inventory/serials?productId=${id}`).then(setSerials).catch(() => {});
  }
  useEffect(() => {
    if (!card) return;
    if (card.tracking === 'بچ') loadBatches();
    if (card.tracking === 'سریال') loadSerials();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [card?.tracking, id]);

  // افقِ ۶۰روزهٔ هشدارِ انقضا — تاریخ‌هایِ شمسیِ «yyyy/MM/dd» به‌صورتِ رشته‌ای قابلِ مقایسه‌اند.
  const expiryHorizon = (() => {
    const future = new Date();
    future.setDate(future.getDate() + 60);
    const { y, m, d } = jalaliOf(future);
    return `${y}/${String(m).padStart(2, '0')}/${String(d).padStart(2, '0')}`;
  })();

  async function saveBatch() {
    if (!id) return;
    if (!batchForm.batchNumber.trim()) { setBsError('شمارهٔ بچ الزامی است.'); return; }
    setBsBusy(true);
    setBsError(null);
    try {
      await apiPost('/api/inventory/batches', {
        productId: Number(id),
        batchNumber: batchForm.batchNumber,
        productionDate: batchForm.productionDate || null,
        expiryDate: batchForm.expiryDate || null,
        quantity: batchForm.quantity,
        purchasePrice: batchForm.purchasePrice ? Number(batchForm.purchasePrice) : null,
        notes: null,
      });
      setShowBatchForm(false);
      setBatchForm({ batchNumber: '', productionDate: '', expiryDate: '', quantity: 0, purchasePrice: '' });
      loadBatches();
    } catch (e) {
      setBsError(e instanceof ApiError ? e.message : 'ذخیرهٔ بچ ناموفق بود.');
    } finally {
      setBsBusy(false);
    }
  }

  async function saveSerial() {
    if (!id) return;
    if (!serialForm.serialNumber.trim()) { setBsError('شمارهٔ سریال الزامی است.'); return; }
    setBsBusy(true);
    setBsError(null);
    try {
      await apiPost('/api/inventory/serials', {
        productId: Number(id),
        serialNumber: serialForm.serialNumber,
        warehouseId: null,
        purchasePrice: serialForm.purchasePrice ? Number(serialForm.purchasePrice) : null,
        purchaseDate: serialForm.purchaseDate || null,
      });
      setShowSerialForm(false);
      setSerialForm({ serialNumber: '', purchasePrice: '', purchaseDate: '' });
      loadSerials();
    } catch (e) {
      setBsError(e instanceof ApiError ? e.message : 'ذخیرهٔ سریال ناموفق بود.');
    } finally {
      setBsBusy(false);
    }
  }

  async function toggleActive() {
    if (!id || !card) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/products/${id}/${card.isActive ? 'deactivate' : 'activate'}`, {});
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیتِ کالا ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  if (!card && error) return <StatusMessage kind="error">{error}</StatusMessage>;
  if (!card) return <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>;

  const stockColumns: Column<ProductCardStockRow>[] = [
    { key: 'w', header: 'انبار', render: (r) => r.warehouseName },
    {
      key: 'q', header: 'موجودی', numeric: true,
      render: (r) => <span style={{ fontWeight: 600, color: r.isLow ? 'var(--danger-700)' : 'var(--text-strong)' }}>{money(r.quantity)}</span>,
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Link to="/products" style={{ fontSize: 'var(--text-sm)' }}>
          ← بازگشت به فهرستِ کالاها
        </Link>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-secondary btn-sm" onClick={() => navigate(`/products/${id}/edit`)}>ذخیره</button>
          <button className="btn btn-secondary btn-sm" onClick={() => window.print()}>چاپِ بارکد</button>
          <button className="btn btn-secondary btn-sm" disabled={busy} onClick={toggleActive}>
            {busy ? '…' : card.isActive ? 'غیرفعال‌سازی' : 'فعال‌سازی'}
          </button>
        </div>
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {/* برچسبِ چاپیِ کالا — روی صفحه دیده نمی‌شود، فقط هنگامِ «چاپِ بارکد» ظاهر می‌شود.
          نمادِ بارکد حالا واقعاً اسکن‌شدنی است (Code 128 به‌صورتِ SVGِ خالص، بدونِ کتابخانه). */}
      <div className="print-area print-only" style={{ textAlign: 'center', padding: 24, border: '1px dashed #999', maxWidth: 320, margin: '0 auto' }}>
        <div style={{ fontSize: 18, fontWeight: 700 }}>{card.name}</div>
        <div style={{ margin: '12px 0' }}>
          <Barcode value={card.barcode || card.code} />
        </div>
        <div style={{ fontSize: 14 }}>{money(card.salePrice)} ریال</div>
      </div>

      <div className="no-print" style={{ display: 'flex', gap: 'var(--space-4)', marginTop: 'var(--space-4)', alignItems: 'flex-start' }}>
        <div style={{ width: 300, flex: 'none', background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 14 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-strong)', display: 'flex', alignItems: 'center', gap: 6 }}>
            {card.name}
            <span className={`st ${card.isActive ? 'g' : 'n'}`}><i />{card.isActive ? 'فعال' : 'غیرفعال'}</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 10 }}>
            کد: {card.code} {card.barcode ? `· بارکد: ${card.barcode}` : ''}
          </div>
          <hr style={{ margin: '10px 0', border: 'none', borderTop: '1px solid var(--gray-100)' }} />
          {[
            ['قیمتِ خرید', money(card.purchasePrice)],
            ['قیمتِ فروش', money(card.salePrice)],
            ['قیمتِ عمده', money(card.wholesalePrice)],
            ['قیمتِ مصرف‌کننده', money(card.consumerPrice)],
            ['نرخِ مالیات', `${card.taxRate}٪`],
            ['روشِ ردیابی', card.tracking],
            ['حداقلِ موجودی', money(card.minStock)],
            ...(card.reorderPoint != null ? [['نقطهٔ سفارش', money(card.reorderPoint)]] : []),
          ].map(([k, v]) => (
            <div key={k} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', padding: '5px 0' }}>
              <span style={{ color: 'var(--text-muted)' }}>{k}</span>
              <span style={{ fontWeight: 500 }}>{v}</span>
            </div>
          ))}
        </div>

        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
            <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 14px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>مجموعِ موجودی (همهٔ انبارها)</div>
              <div className="num" style={{ fontSize: 20, fontWeight: 800, marginTop: 3 }}>{money(card.totalStock)}</div>
            </div>
            {card.reorderPoint != null && (
              <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 14px' }}>
                <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>نقطهٔ سفارش</div>
                <div className="num" style={{ fontSize: 20, fontWeight: 800, marginTop: 3, color: card.totalStock <= card.reorderPoint ? 'var(--danger-700)' : undefined }}>{money(card.reorderPoint)}</div>
              </div>
            )}
            <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 14px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>میانگینِ بهایِ تمام‌شده</div>
              <div className="num" style={{ fontSize: 20, fontWeight: 800, marginTop: 3 }}>{money(card.averageCost)}</div>
            </div>
          </div>
          <DataTable columns={stockColumns} rows={card.warehouseStocks} rowKey={(r, i) => `${r.warehouseName}-${i}`} emptyText="موجودی‌ای ثبت نشده." />

          {/* پورتِ گریدِ «کاردکس (گردشِ کالا)»ِ product-card.html — رویِ GetKardexQueryِ
              ازقبل‌موجود که هیچ اندپوینتی صدایش نمی‌زد. */}
          <div>
            <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-strong)', marginBottom: 6 }}>کاردکس (گردشِ کالا)</div>
            <div className="dgrid-wrap">
              <table className="dgrid">
                <thead>
                  <tr>
                    <th style={{ width: 80 }}>تاریخ</th>
                    <th style={{ width: 95 }}>سند</th>
                    <th>شرح</th>
                    <th style={{ width: 70 }} className="num">ورود</th>
                    <th style={{ width: 70 }} className="num">خروج</th>
                    <th style={{ width: 70 }} className="num">مانده</th>
                    <th style={{ width: 110 }} className="num">فی</th>
                  </tr>
                </thead>
                <tbody>
                  {kardex.map((r, i) => (
                    <tr key={i}>
                      <td className="num mut">{r.date}</td>
                      <td className="num">{r.documentNumber ?? '—'}</td>
                      <td>{r.type}{r.notes && <div className="mut" style={{ fontSize: 10.5 }}>{r.notes}</div>}</td>
                      <td className="num" style={{ color: r.in > 0 ? 'var(--success-700)' : undefined }}>{r.in > 0 ? money(r.in) : '·'}</td>
                      <td className="num" style={{ color: r.out > 0 ? 'var(--danger-500)' : undefined }}>{r.out > 0 ? money(r.out) : '·'}</td>
                      <td className="num strong">{money(r.balance)}</td>
                      <td className="num mut">{r.unitCost > 0 ? money(r.unitCost) : '—'}</td>
                    </tr>
                  ))}
                  {kardex.length === 0 && (
                    <tr>
                      <td colSpan={7} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                        گردشی ثبت نشده.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {card.tracking === 'بچ' && (
            <div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
                <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-strong)' }}>بچ‌ها (کنترلِ انقضا)</div>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowBatchForm((v) => !v)}>افزودنِ بچ</button>
              </div>
              {bsError && <StatusMessage kind="error">{bsError}</StatusMessage>}
              {showBatchForm && (
                <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)' }}>
                    <div className="field">
                      <label className="label">شمارهٔ بچ</label>
                      <input className="input" value={batchForm.batchNumber} onChange={(e) => setBatchForm({ ...batchForm, batchNumber: e.target.value })} />
                    </div>
                    <JalaliDateInput value={batchForm.productionDate} onChange={(v) => setBatchForm({ ...batchForm, productionDate: v })} label="تاریخِ تولید" />
                    <JalaliDateInput value={batchForm.expiryDate} onChange={(v) => setBatchForm({ ...batchForm, expiryDate: v })} label="تاریخِ انقضا" />
                    <div className="field">
                      <label className="label">تعداد</label>
                      <input className="input" type="number" value={batchForm.quantity} onChange={(e) => setBatchForm({ ...batchForm, quantity: Number(e.target.value) })} style={{ direction: 'ltr' }} />
                    </div>
                  </div>
                  <div style={{ marginTop: 'var(--space-2)' }}>
                    <button type="button" className="btn btn-primary btn-sm" disabled={bsBusy} onClick={saveBatch}>{bsBusy ? '…' : 'ذخیره'}</button>
                  </div>
                </div>
              )}
              <div className="dgrid-wrap">
                <table className="dgrid">
                  <thead>
                    <tr>
                      <th>شمارهٔ بچ</th>
                      <th style={{ width: 90 }}>تولید</th>
                      <th style={{ width: 90 }}>انقضا</th>
                      <th style={{ width: 80 }} className="num">تعداد</th>
                    </tr>
                  </thead>
                  <tbody>
                    {batches.map((b) => {
                      const expiring = !!b.expiryDate && b.expiryDate <= expiryHorizon;
                      return (
                        <tr key={b.id}>
                          <td>{b.batchNumber}</td>
                          <td className="num mut">{b.productionDate ?? '—'}</td>
                          <td className="num" style={{ color: expiring ? 'var(--danger-700)' : undefined, fontWeight: expiring ? 600 : undefined }}>
                            {b.expiryDate ?? '—'}{expiring && ' ⚠'}
                          </td>
                          <td className="num">{money(b.quantity)}</td>
                        </tr>
                      );
                    })}
                    {batches.length === 0 && (
                      <tr><td colSpan={4} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>بچی ثبت نشده.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {card.tracking === 'سریال' && (
            <div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
                <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-strong)' }}>سریال‌ها</div>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowSerialForm((v) => !v)}>افزودنِ سریال</button>
              </div>
              {bsError && <StatusMessage kind="error">{bsError}</StatusMessage>}
              {showSerialForm && (
                <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
                    <div className="field">
                      <label className="label">شمارهٔ سریال</label>
                      <input className="input" value={serialForm.serialNumber} onChange={(e) => setSerialForm({ ...serialForm, serialNumber: e.target.value })} />
                    </div>
                    <div className="field">
                      <label className="label">قیمتِ خرید</label>
                      <input className="input" type="number" value={serialForm.purchasePrice} onChange={(e) => setSerialForm({ ...serialForm, purchasePrice: e.target.value })} style={{ direction: 'ltr' }} />
                    </div>
                    <JalaliDateInput value={serialForm.purchaseDate} onChange={(v) => setSerialForm({ ...serialForm, purchaseDate: v })} label="تاریخِ خرید" />
                  </div>
                  <div style={{ marginTop: 'var(--space-2)' }}>
                    <button type="button" className="btn btn-primary btn-sm" disabled={bsBusy} onClick={saveSerial}>{bsBusy ? '…' : 'ذخیره'}</button>
                  </div>
                </div>
              )}
              <div className="dgrid-wrap">
                <table className="dgrid">
                  <thead>
                    <tr>
                      <th>شمارهٔ سریال</th>
                      <th style={{ width: 100 }}>وضعیت</th>
                      <th style={{ width: 90 }}>تاریخِ خرید</th>
                    </tr>
                  </thead>
                  <tbody>
                    {serials.map((s) => (
                      <tr key={s.id}>
                        <td>{s.serialNumber}</td>
                        <td><span className={`badge ${s.status === 'موجود' ? 'badge-green' : s.status === 'فروخته شده' ? 'badge-gray' : 'badge-yellow'}`}>{s.status}</span></td>
                        <td className="num mut">{s.purchaseDate ?? '—'}</td>
                      </tr>
                    ))}
                    {serials.length === 0 && (
                      <tr><td colSpan={3} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>سریالی ثبت نشده.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
