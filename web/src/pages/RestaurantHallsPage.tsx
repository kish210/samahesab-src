import { useEffect, useMemo, useState, type MouseEvent } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { money, numberFormat, numberToPersianWords } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { printThermal } from '../lib/thermalPrint';
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
  waiterId: number | null;
  subTotal: number;
  discount: number;
  serviceCharge: number;
  tax: number;
  tip: number;
  grandTotal: number;
  paidAmount: number;
  items: OrderItemDto[];
}
interface WaiterDto { id: number; name: string }
interface ProductGroupDto { id: number; name: string }
interface ProductDto { id: number; groupId: number | null; code: string; name: string; salePrice: number; isActive: boolean }

const TABLE_STYLE: Record<number, string> = { 0: 'free', 1: 'busy', 2: 'reserved', 3: 'bill' };

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
  const [takeoutMode, setTakeoutMode] = useState(false);
  const [waiters, setWaiters] = useState<WaiterDto[]>([]);
  const [settleOpen, setSettleOpen] = useState(false);
  const [settleDiscount, setSettleDiscount] = useState(0);
  const [settleTip, setSettleTip] = useState(0);
  const [settlePaid, setSettlePaid] = useState(0);

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
    apiGet<WaiterDto[]>('/api/restaurant/waiters').then(setWaiters).catch(() => {});
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

  async function openTakeout(orderType: 1 | 2) {
    setError(null);
    setBusy(true);
    try {
      const res = await apiPost<{ orderId: number }>('/api/restaurant/orders/open', { orderType, tableId: null, guestCount: 1 });
      loadOrder(res.orderId);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بازکردنِ سفارش ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  /** تغییر تعداد مهمان — از همان endpoint جدیدِ backend (ChangeGuestCountCommand). */
  async function changeGuestCount(delta: number) {
    if (!order) return;
    const next = Math.max(1, order.guestCount + delta);
    if (next === order.guestCount) return;
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/guest-count/${next}`, undefined);
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ تعداد مهمان ناموفق بود.');
    }
  }

  /** یادداشتِ آشپزخانهٔ ردیف (مثل «بدون پیاز») — SetOrderItemNotesCommand. */
  async function setItemNotes(item: OrderItemDto) {
    if (!order) return;
    const notes = window.prompt(`یادداشتِ آشپزخانه برای «${item.productName}»:`, item.notes ?? '');
    if (notes == null) return;
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/items/${item.id}/notes`, notes || null);
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ثبتِ یادداشت ناموفق بود.');
    }
  }

  /** رزرو / لغوِ رزروِ میز — SetTableReservationCommand. */
  async function toggleReserve(t: TableDto, e: MouseEvent) {
    e.stopPropagation();
    setError(null);
    setBusy(true);
    try {
      await apiPost(`/api/restaurant/tables/${t.id}/reserve/${t.statusCode !== 2}`, undefined);
      loadHalls();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تغییرِ وضعیتِ رزرو ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  /** تخصیصِ گارسون به سفارشِ باز — AssignWaiterCommand. */
  async function assignWaiter(id: number | null) {
    if (!order || id == null) return;
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/waiter/${id}`, undefined);
      loadOrder(order.id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تخصیصِ گارسون ناموفق بود.');
    }
  }

  /** بازکردنِ مودالِ تسویه — تخفیف/انعام با جمعِ زنده. */
  function openSettle() {
    if (!order) return;
    setSettleDiscount(order.discount);
    setSettleTip(order.tip);
    setSettlePaid(order.grandTotal);
    setSettleOpen(true);
  }

  const settleGrand = order ? Math.max(0, order.subTotal - settleDiscount + settleTip) : 0;

  const ORDER_TYPE_FA: Record<string, string> = { DineIn: 'سالن', Takeaway: 'بیرون‌بر', Delivery: 'پیک' };

  /** چاپِ حرارتیِ صورتحسابِ سفارش (۸۰mm). */
  function printBill() {
    if (!order) return;
    printThermal({
      title: 'صورتحسابِ رستوران',
      header: [
        { label: 'سفارش', value: order.orderNumber },
        { label: 'نوع', value: ORDER_TYPE_FA[order.orderType] ?? order.orderType },
        ...(activeTable ? [{ label: 'میز', value: activeTable.name }] : []),
        { label: 'نفرات', value: numberFormat.format(order.guestCount) },
      ],
      items: order.items.map((it) => ({
        name: it.productName,
        qty: `${numberFormat.format(it.quantity)} × ${money(it.unitPrice)}`,
        amount: money(it.lineTotal),
        note: it.notes ?? undefined,
      })),
      totals: [
        { label: 'جمعِ اقلام', value: money(order.subTotal) },
        ...(order.discount > 0 ? [{ label: 'تخفیف', value: `−${money(order.discount)}` }] : []),
        ...(order.serviceCharge > 0 ? [{ label: 'مالیات و خدمات', value: money(order.serviceCharge) }] : []),
        ...(order.tax > 0 ? [{ label: 'مالیات', value: money(order.tax) }] : []),
        ...(order.tip > 0 ? [{ label: 'انعام', value: money(order.tip) }] : []),
        { label: 'جمعِ کل', value: `${money(order.grandTotal)} ریال`, bold: true },
      ],
      amountInWords: `${numberToPersianWords(order.grandTotal)} ریال`,
      footer: ['ممنون از حضورِ شما'],
    });
  }

  async function doSettle() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/restaurant/orders/${order.id}/settle`, {
        orderId: order.id, paidAmount: settlePaid, discount: settleDiscount, tip: settleTip,
      });
      setSettleOpen(false);
      setOrder(null);
      loadHalls();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تسویه ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  const waiterOptions: SearchSelectOption[] = waiters.map((w) => ({ id: w.id, label: w.name }));
  const hasPendingItems = !!order?.items.some((i) => i.statusCode === 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <PageHeader title="رستوران" />
      {error && <div style={{ marginBottom: 'var(--space-2)' }}><StatusMessage kind="error">{error}</StatusMessage></div>}

      <div className="rest">
        <div className="rest-main">
          <div className="floor-tabs">
            {(halls ?? []).map((h) => (
              <button key={h.id} type="button" className={`ft${!takeoutMode && h.id === hallId ? ' on' : ''}`} onClick={() => { setHallId(h.id); setTakeoutMode(false); }}>
                {h.name}
              </button>
            ))}
            <button type="button" className={`ft${takeoutMode ? ' on' : ''}`} onClick={() => setTakeoutMode(true)}>
              بیرون‌بر / پیک
            </button>
            {moveMode && (
              <button type="button" className="ft on" style={{ marginInlineStart: 'auto', background: 'var(--gold-500)', borderColor: 'var(--gold-500)' }}
                onClick={() => setMoveMode(false)}>
                لغوِ انتقال ✕
              </button>
            )}
          </div>

          {takeoutMode ? (
            <div style={{ display: 'flex', gap: 10, padding: '14px 4px' }}>
              <button type="button" className="tbl-card free" style={{ flex: 1, minHeight: 90 }} disabled={busy} onClick={() => openTakeout(1)}>
                <div className="tn">+ سفارشِ بیرون‌بر</div>
              </button>
              <button type="button" className="tbl-card free" style={{ flex: 1, minHeight: 90 }} disabled={busy} onClick={() => openTakeout(2)}>
                <div className="tn">+ سفارشِ پیک</div>
              </button>
            </div>
          ) : (
            <div className="rest-tables">
              {activeHall?.tables.map((t) => (
                <div key={t.id}
                  className={`tbl-card ${TABLE_STYLE[t.statusCode] ?? 'free'}${order?.tableId === t.id ? ' selected' : ''}`}
                  role="button" tabIndex={0} aria-disabled={busy}
                  onClick={() => !busy && openOrMoveTable(t)}
                  onKeyDown={(e) => { if (e.target === e.currentTarget && !busy && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); openOrMoveTable(t); } }}>
                  <span className="badge2">{t.status}</span>
                  <div className="tn">{t.name}</div>
                  <div className="seats">{numberFormat.format(t.capacity)} نفره</div>
                  {(t.statusCode === 0 || t.statusCode === 2) && (
                    <button type="button" className="resv-btn" disabled={busy} onClick={(e) => toggleReserve(t, e)}>
                      {t.statusCode === 2 ? 'لغو رزرو' : 'رزرو'}
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          <div className="menu-cats">
            {groups.map((g) => (
              <button key={g.id} type="button" className={`mc${g.id === groupId ? ' on' : ''}`} onClick={() => setGroupId(g.id)}>
                {g.name}
              </button>
            ))}
          </div>
          <div className="menu-grid">
            {menuProducts.map((p) => (
              <div key={p.id} className="tbl-card menu-tile"
                role="button" tabIndex={0}
                onClick={() => addToOrder(p)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); addToOrder(p); } }}>
                <div className="tn" style={{ fontSize: 13, fontWeight: 600 }}>{p.name}</div>
                <div className="foot" style={{ color: 'var(--blue-700)' }}>{money(p.salePrice)} ریال</div>
              </div>
            ))}
          </div>
        </div>

        <div className="rest-order">
          <div className="order-card print-area">
            <div className="hd">
              <span className="tn">{activeTable ? activeTable.name : order ? order.orderType : 'میزی انتخاب نشده'}</span>
              {order && (
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 3, fontSize: 11, background: 'rgba(255,255,255,.18)', borderRadius: 99, padding: '1px 4px' }}>
                  <button type="button" className="hstep" onClick={() => changeGuestCount(-1)}>−</button>
                  <span style={{ minWidth: 36, textAlign: 'center' }}>{numberFormat.format(order.guestCount)} نفر</span>
                  <button type="button" className="hstep" onClick={() => changeGuestCount(1)}>+</button>
                </span>
              )}
              {order && <span className="m">{order.orderNumber} · {order.status}</span>}
            </div>
            {order && (
              <div style={{ padding: '8px 14px', borderBottom: '1px solid var(--gray-100)' }}>
                <SearchSelect options={waiterOptions} value={order.waiterId} onChange={assignWaiter} placeholder="گارسون (اختیاری)…" />
              </div>
            )}
            <div className="rows">
              {!order && <div style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 12.5 }}>یک میز را انتخاب کنید یا سفارشِ نو باز کنید.</div>}
              {order?.items.map((it) => (
                <div key={it.id} className="orow">
                  <div className="t">
                    {it.productName}
                    {it.notes && <div className="sub">📝 {it.notes}</div>}
                    <button type="button" className="note-btn" title="یادداشتِ آشپزخانه" onClick={() => setItemNotes(it)}>✎</button>
                  </div>
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
                {order.serviceCharge > 0 && <div className="r"><span>مالیات و خدمات</span><span className="v">{money(order.serviceCharge)}</span></div>}
                {order.tax > 0 && <div className="r"><span>مالیات</span><span className="v">{money(order.tax)}</span></div>}
                <div className="grand"><span className="l">جمعِ کل</span><span className="v">{money(order.grandTotal)}</span></div>
              </div>
            )}
          </div>
          <div className="obtns">
            <button type="button" className="kot" disabled={!order || !hasPendingItems || busy} onClick={sendToKitchen}>
              ارسال به آشپزخانه (KOT)
            </button>
            <button type="button" disabled={!order} onClick={printBill}>
              صورتحساب
            </button>
            <button type="button" disabled={!order || !order.tableId || busy} onClick={() => setMoveMode((m) => !m)}>
              انتقالِ میز
            </button>
            <button type="button" className="settle" disabled={!order || order.items.length === 0 || busy} onClick={openSettle}>
              تسویه و پرداخت
            </button>
          </div>
        </div>
      </div>

      {settleOpen && order && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', borderRadius: 'var(--radius-md)', padding: 'var(--space-4)', width: 380 }}>
            <div style={{ fontWeight: 700, marginBottom: 12 }}>تسویهٔ سفارش {order.orderNumber}</div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 10 }}>
              <span>جمعِ اقلام</span><span className="num">{money(order.subTotal)}</span>
            </div>
            <label style={{ fontSize: 12, color: 'var(--text-muted)', display: 'block', marginBottom: 3 }}>تخفیف (ریال)</label>
            <input className="input" style={{ marginBottom: 8, direction: 'ltr', textAlign: 'end' }} type="number" min={0}
              value={settleDiscount || ''} onChange={(e) => setSettleDiscount(Number(e.target.value) || 0)} />
            <label style={{ fontSize: 12, color: 'var(--text-muted)', display: 'block', marginBottom: 3 }}>انعام (ریال)</label>
            <input className="input" style={{ marginBottom: 8, direction: 'ltr', textAlign: 'end' }} type="number" min={0}
              value={settleTip || ''} onChange={(e) => setSettleTip(Number(e.target.value) || 0)} />
            <label style={{ fontSize: 12, color: 'var(--text-muted)', display: 'block', marginBottom: 3 }}>مبلغِ دریافتی (ریال)</label>
            <input className="input" style={{ marginBottom: 4, direction: 'ltr', textAlign: 'end' }} type="number" min={0}
              value={settlePaid || ''} onChange={(e) => setSettlePaid(Number(e.target.value) || 0)} />
            <button type="button" className="btn btn-secondary btn-sm" style={{ marginBottom: 12 }} onClick={() => setSettlePaid(settleGrand)}>
              = مبلغِ دقیق
            </button>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700, marginBottom: 12, paddingTop: 8, borderTop: '1px solid var(--gray-100)' }}>
              <span>قابلِ پرداخت</span><span className="num">{money(settleGrand)}</span>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <button type="button" className="btn btn-primary" style={{ flex: 1 }} disabled={busy} onClick={doSettle}>ثبتِ تسویه</button>
              <button type="button" className="btn btn-secondary" onClick={() => setSettleOpen(false)}>انصراف</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
