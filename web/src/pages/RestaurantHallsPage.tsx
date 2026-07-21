import { useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { money, numberFormat } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import './restaurant.css';

interface TableDto {
  id: number;
  hallId: number;
  name: string;
  capacity: number;
  status: string;
  statusCode: number;
  currentOrderId: number | null;
}
interface HallDto {
  id: number;
  name: string;
  displayOrder: number;
  tables: TableDto[];
}
interface OrderItemDto {
  id: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
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
  serviceCharge: number;
  tax: number;
  grandTotal: number;
  items: OrderItemDto[];
}
interface ProductGroupDto { id: number; name: string }
interface ProductDto { id: number; groupId: number | null; code: string; name: string; salePrice: number; isActive: boolean }

const TABLE_STYLE: Record<number, string> = { 0: 'free', 1: 'busy', 2: 'busy', 3: 'bill' };

/** صفحهٔ لمسیِ رستوران — پورتِ ساختاریِ design-system/screens/restaurant.html:
 * تب‌هایِ سالن + نقشهٔ میز رنگی + تب‌هایِ دستهٔ منو + کاشیِ منو + کارتِ سفارشِ جاری. */
export function RestaurantHallsPage() {
  const [halls, setHalls] = useState<HallDto[] | null>(null);
  const [hallId, setHallId] = useState<number | null>(null);
  const [groups, setGroups] = useState<ProductGroupDto[]>([]);
  const [groupId, setGroupId] = useState<number | null>(null);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [moveMode, setMoveMode] = useState(false);

  function loadHalls() {
    apiGet<HallDto[]>('/api/restaurant/halls').then((data) => {
      setHalls(data);
      setHallId((prev) => prev ?? data[0]?.id ?? null);
    }).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ سالن‌ها.'));
  }

  useEffect(() => {
    loadHalls();
    apiGet<ProductGroupDto[]>('/api/products/groups').then((g) => { setGroups(g); setGroupId(g[0]?.id ?? null); }).catch(() => {});
    apiGet<ProductDto[]>('/api/products').then(setProducts).catch(() => {});
  }, []);

  function loadOrder(orderId: number) {
    apiGet<OrderDto>(`/api/restaurant/orders/${orderId}`).then(setOrder).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ سفارش.'));
  }

  const activeHall = halls?.find((h) => h.id === hallId) ?? null;
  const activeTable = order ? activeHall?.tables.find((t) => t.id === order.tableId) : null;
  const menuProducts = useMemo(
    () => products.filter((p) => p.isActive && (groupId == null || p.groupId === groupId)),
    [products, groupId],
  );

  async function openOrMoveTable(t: TableDto) {
    setError(null);
    if (moveMode && order) {
      if (t.statusCode !== 0) { setError('میزِ مقصد باید آزاد باشد.'); return; }
      setBusy(true);
      try {
        await apiPost(`/api/restaurant/orders/${order.id}/move-table/${t.id}`, undefined);
        setMoveMode(false);
        loadOrder(order.id);
        loadHalls();
      } catch (e) {
        setError(e instanceof ApiError ? e.message : 'انتقالِ میز ناموفق بود.');
      } finally {
        setBusy(false);
      }
      return;
    }
    if (t.currentOrderId) { loadOrder(t.currentOrderId); return; }
    setBusy(true);
    try {
      const res = await apiPost<{ orderId: number }>('/api/restaurant/orders/open', { orderType: 0, tableId: t.id, guestCount: 1 });
      loadOrder(res.orderId);
      loadHalls();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بازکردنِ میز ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function addToOrder(p: ProductDto) {
    if (!order) { setError('اول یک میز را انتخاب کنید.'); return; }
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/items`, {
        orderId: order.id, productId: p.id, productName: p.name, quantity: 1, unitPrice: p.salePrice,
      });
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'افزودنِ کالا ناموفق بود.');
    }
  }

  async function changeQty(item: OrderItemDto, delta: number) {
    if (!order) return;
    const next = item.quantity + delta;
    setError(null);
    try {
      if (next <= 0) await apiDelete(`/api/restaurant/orders/${order.id}/items/${item.id}`);
      else await apiPost(`/api/restaurant/orders/${order.id}/items/${item.id}/qty/${next}`, undefined);
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ تعداد ناموفق بود.');
    }
  }

  async function sendToKitchen() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/send-to-kitchen`, undefined);
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ارسال به آشپزخانه ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function settle() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/settle`, { orderId: order.id, paidAmount: order.grandTotal });
      setOrder(null);
      loadHalls();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تسویه ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  const hasPendingItems = !!order?.items.some((i) => i.statusCode === 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <PageHeader title="رستوران" />
      {error && <div style={{ marginBottom: 'var(--space-2)' }}><StatusMessage kind="error">{error}</StatusMessage></div>}

      <div className="rest">
        <div className="rest-main">
          <div className="floor-tabs">
            {(halls ?? []).map((h) => (
              <button key={h.id} type="button" className={`ft${h.id === hallId ? ' on' : ''}`} onClick={() => setHallId(h.id)}>
                {h.name}
              </button>
            ))}
            {moveMode && (
              <button type="button" className="ft on" style={{ marginInlineStart: 'auto', background: 'var(--gold-500)', borderColor: 'var(--gold-500)' }}
                onClick={() => setMoveMode(false)}>
                لغوِ انتقال ✕
              </button>
            )}
          </div>

          <div className="rest-tables">
            {activeHall?.tables.map((t) => (
              <div key={t.id}
                className={`tbl-card ${TABLE_STYLE[t.statusCode] ?? 'free'}${order?.tableId === t.id ? ' selected' : ''}`}
                onClick={() => !busy && openOrMoveTable(t)}>
                <span className="badge2">{t.status}</span>
                <div className="tn">{t.name}</div>
                <div className="seats">{numberFormat.format(t.capacity)} نفره</div>
              </div>
            ))}
          </div>

          <div className="menu-cats">
            {groups.map((g) => (
              <button key={g.id} type="button" className={`mc${g.id === groupId ? ' on' : ''}`} onClick={() => setGroupId(g.id)}>
                {g.name}
              </button>
            ))}
          </div>
          <div className="menu-grid">
            {menuProducts.map((p) => (
              <div key={p.id} className="tbl-card menu-tile" onClick={() => addToOrder(p)}>
                <div className="tn" style={{ fontSize: 13, fontWeight: 600 }}>{p.name}</div>
                <div className="foot" style={{ color: 'var(--blue-700)' }}>{money(p.salePrice)} ریال</div>
              </div>
            ))}
          </div>
        </div>

        <div className="rest-order">
          <div className="order-card">
            <div className="hd">
              <span className="tn">{activeTable ? activeTable.name : order ? order.orderType : 'میزی انتخاب نشده'}</span>
              {order && <span style={{ fontSize: 11, background: 'rgba(255,255,255,.18)', padding: '2px 8px', borderRadius: 99 }}>{numberFormat.format(order.guestCount)} نفر</span>}
              {order && <span className="m">{order.orderNumber} · {order.status}</span>}
            </div>
            <div className="rows">
              {!order && <div style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 12.5 }}>یک میز را انتخاب کنید یا سفارشِ نو باز کنید.</div>}
              {order?.items.map((it) => (
                <div key={it.id} className="orow">
                  <div className="t">{it.productName}{it.notes && <div className="sub">{it.notes}</div>}</div>
                  {it.statusCode === 0 ? (
                    <div className="q">
                      <button type="button" onClick={() => changeQty(it, -1)}>−</button>
                      <span className="n">{numberFormat.format(it.quantity)}</span>
                      <button type="button" onClick={() => changeQty(it, 1)}>+</button>
                    </div>
                  ) : (
                    <div className="q"><span className="n">{numberFormat.format(it.quantity)}</span></div>
                  )}
                  <div className="amt">{money(it.lineTotal)}</div>
                </div>
              ))}
            </div>
            {order && (
              <div className="ot">
                <div className="r"><span>جمعِ اقلام</span><span className="v">{money(order.subTotal)}</span></div>
                <div className="grand"><span className="l">جمعِ کل</span><span className="v">{money(order.grandTotal)}</span></div>
              </div>
            )}
          </div>
          <div className="obtns">
            <button type="button" className="kot" disabled={!order || !hasPendingItems || busy} onClick={sendToKitchen}>
              ارسال به آشپزخانه (KOT)
            </button>
            <button type="button" disabled={!order || busy} onClick={() => setMoveMode((m) => !m)}>
              انتقالِ میز
            </button>
            <button type="button" className="settle" disabled={!order || order.items.length === 0 || busy} onClick={settle}>
              تسویه و پرداخت
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
