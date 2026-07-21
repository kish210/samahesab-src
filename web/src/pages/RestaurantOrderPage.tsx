import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { money } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';

interface OrderItemDto {
  id: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  lineTotal: number;
  status: string;
  statusCode: number;
  notes: string | null;
}
interface OrderDto {
  id: number;
  orderNumber: string;
  orderType: string;
  status: string;
  tableId: number | null;
  guestCount: number;
  subTotal: number;
  discount: number;
  serviceCharge: number;
  tax: number;
  tip: number;
  grandTotal: number;
  paidAmount: number;
  salesInvoiceId: number | null;
  items: OrderItemDto[];
}
interface ProductRow {
  id: number;
  code: string;
  name: string;
  salePrice: number;
  taxRate: number;
}
interface WaiterDto {
  id: number;
  name: string;
}

const ORDER_TYPE_LABEL: Record<string, string> = { DineIn: 'صرفِ داخل', Takeaway: 'بیرون‌بر', Delivery: 'پیک' };

/** ویرایشگرِ سفارشِ رستوران — افزودنِ ردیف، ارسال به آشپزخانه، تخصیصِ گارسون، تسویه. */
export function RestaurantOrderPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [products, setProducts] = useState<ProductRow[]>([]);
  const [waiters, setWaiters] = useState<WaiterDto[]>([]);
  const [productId, setProductId] = useState<number | null>(null);
  const [qty, setQty] = useState('1');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [settling, setSettling] = useState(false);
  const [paidAmount, setPaidAmount] = useState('0');
  const [serviceCharge, setServiceCharge] = useState('0');

  function load() {
    if (!id) return;
    apiGet<OrderDto>(`/api/restaurant/orders/${id}`)
      .then(setOrder)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ سفارش.'));
  }

  useEffect(() => {
    load();
    apiGet<ProductRow[]>('/api/products/list').then(setProducts).catch(() => {});
    apiGet<WaiterDto[]>('/api/restaurant/waiters').then(setWaiters).catch(() => {});
  }, [id]);

  async function addItem() {
    if (!id || !productId) return;
    const p = products.find((x) => x.id === productId);
    if (!p) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${id}/items`, {
        orderId: Number(id), productId: p.id, productName: p.name, quantity: Number(qty) || 1, unitPrice: p.salePrice,
      });
      setProductId(null);
      setQty('1');
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'افزودنِ ردیف ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function removeItem(itemId: number) {
    if (!id) return;
    setError(null);
    try {
      await apiDelete(`/api/restaurant/orders/${id}/items/${itemId}`);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'حذفِ ردیف ناموفق بود.');
    }
  }

  async function changeQty(itemId: number, newQty: number) {
    if (!id) return;
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${id}/items/${itemId}/qty/${newQty}`, undefined);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ تعداد ناموفق بود.');
    }
  }

  async function sendToKitchen() {
    if (!id) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${id}/send-to-kitchen`, undefined);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ارسال به آشپزخانه ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function assignWaiter(waiterId: number) {
    if (!id) return;
    try {
      await apiPost(`/api/restaurant/orders/${id}/waiter/${waiterId}`, undefined);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تخصیصِ گارسون ناموفق بود.');
    }
  }

  async function settle() {
    if (!id || !order) return;
    setSettling(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${id}/settle`, {
        orderId: Number(id),
        paidAmount: Number(paidAmount) || order.grandTotal,
        serviceCharge: Number(serviceCharge) || 0,
      });
      navigate('/restaurant');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تسویه ناموفق بود.');
    } finally {
      setSettling(false);
    }
  }

  if (error && !order) return <StatusMessage kind="error">{error}</StatusMessage>;
  if (!order) return <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>;

  const hasPendingItems = order.items.some((i) => i.statusCode === 0);
  const productOptions = products.map((p) => ({ id: p.id, label: p.name, sublabel: p.code }));

  return (
    <div>
      <PageHeader title={`سفارشِ ${order.orderNumber} — ${ORDER_TYPE_LABEL[order.orderType] ?? order.orderType}`} actions={
        <button className="btn btn-secondary btn-sm" onClick={() => navigate('/restaurant')}>← بازگشت به میزها</button>
      } />

      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 'var(--space-3)', flexWrap: 'wrap' }}>
        <span className="badge badge-blue">{order.status}</span>
        <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>تعدادِ مهمان: {order.guestCount}</span>
        {waiters.length > 0 && (
          <select className="select" style={{ width: 160 }} onChange={(e) => assignWaiter(Number(e.target.value))} defaultValue="">
            <option value="" disabled>تخصیصِ گارسون…</option>
            {waiters.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </select>
        )}
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 'var(--space-3)' }}>
        <thead>
          <tr style={{ textAlign: 'right', color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
            <th style={{ padding: '6px 8px' }}>کالا</th>
            <th style={{ padding: '6px 8px', width: 100 }}>تعداد</th>
            <th style={{ padding: '6px 8px', width: 120 }}>قیمت</th>
            <th style={{ padding: '6px 8px', width: 120 }}>جمع</th>
            <th style={{ padding: '6px 8px', width: 100 }}>وضعیت</th>
            <th style={{ width: 40 }} />
          </tr>
        </thead>
        <tbody>
          {order.items.map((it) => (
            <tr key={it.id} style={{ borderTop: '1px solid var(--border)' }}>
              <td style={{ padding: '6px 8px' }}>{it.productName}{it.notes && <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{it.notes}</div>}</td>
              <td style={{ padding: '6px 8px' }}>
                {it.statusCode === 0 ? (
                  <input className="input num" type="number" min="0" defaultValue={it.quantity}
                    onBlur={(e) => { const v = Number(e.target.value); if (v !== it.quantity) changeQty(it.id, v); }}
                    style={{ width: 70 }} />
                ) : <span className="num">{it.quantity}</span>}
              </td>
              <td className="num" style={{ padding: '6px 8px' }}>{money(it.unitPrice)}</td>
              <td className="num" style={{ padding: '6px 8px' }}>{money(it.lineTotal)}</td>
              <td style={{ padding: '6px 8px' }}>{it.status}</td>
              <td style={{ textAlign: 'center' }}>
                {it.statusCode === 0 && (
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeItem(it.id)}>✕</button>
                )}
              </td>
            </tr>
          ))}
          {order.items.length === 0 && (
            <tr><td colSpan={6} style={{ padding: 16, textAlign: 'center', color: 'var(--text-muted)' }}>سفارش هنوز ردیفی ندارد.</td></tr>
          )}
        </tbody>
      </table>

      <div style={{ display: 'flex', gap: 8, alignItems: 'end', marginBottom: 'var(--space-4)' }}>
        <div className="field" style={{ minWidth: 240 }}>
          <label className="label">افزودنِ کالا</label>
          <SearchSelect options={productOptions} value={productId} onChange={setProductId} placeholder="جست‌وجویِ کالا…" />
        </div>
        <div className="field" style={{ maxWidth: 100 }}>
          <label className="label">تعداد</label>
          <input className="input num" type="number" min="1" value={qty} onChange={(e) => setQty(e.target.value)} />
        </div>
        <button className="btn btn-secondary" disabled={busy || !productId} onClick={addItem}>+ افزودن</button>
        <button className="btn btn-primary" disabled={busy || !hasPendingItems} onClick={sendToKitchen}>ارسال به آشپزخانه</button>
      </div>

      {error && <div style={{ marginBottom: 'var(--space-3)' }}><StatusMessage kind="error">{error}</StatusMessage></div>}

      <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 14, maxWidth: 360 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
          <span>جمعِ کالاها</span><span className="num">{money(order.subTotal)}</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 700, marginBottom: 10 }}>
          <span>جمعِ کل</span><span className="num">{money(order.grandTotal)}</span>
        </div>
        <div className="field" style={{ marginBottom: 8 }}>
          <label className="label">مبلغِ سرویس</label>
          <input className="input num" type="number" min="0" value={serviceCharge} onChange={(e) => setServiceCharge(e.target.value)} />
        </div>
        <div className="field" style={{ marginBottom: 8 }}>
          <label className="label">مبلغِ پرداختی</label>
          <input className="input num" type="number" min="0" value={paidAmount} onChange={(e) => setPaidAmount(e.target.value)} placeholder={String(order.grandTotal)} />
        </div>
        <button className="btn btn-primary" disabled={settling || order.items.length === 0} onClick={settle} style={{ width: '100%' }}>
          {settling ? 'در حالِ تسویه…' : 'تسویه و بستنِ میز'}
        </button>
      </div>
    </div>
  );
}
