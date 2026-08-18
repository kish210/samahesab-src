import { useEffect, useMemo, useState, type KeyboardEvent } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { money, numberFormat, numberToPersianWords } from '../lib/format';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { Barcode } from '../components/Barcode';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { printThermal } from '../lib/thermalPrint';
import './pos.css';

interface ProductRow {
  id: number;
  groupId: number | null;
  code: string;
  name: string;
  salePrice: number;
  taxRate: number;
  minStock: number;
  isActive: boolean;
}

interface ProductGroupDto { id: number; name: string }

interface WarehouseDto {
  id: number;
  name: string;
}

interface CustomerOption {
  id: number;
  name: string;
  code: string;
}

interface HeldSaleListDto {
  id: number;
  label: string;
  total: number;
  createdAt: string;
}

interface HeldSaleDetailDto {
  id: number;
  label: string;
  payload: string;
  total: number;
}

interface CartLine {
  productId: number;
  code: string;
  name: string;
  unitPrice: number;
  taxPct: number;
  quantity: number;
  discountPct: number;
}

interface ReceiptData {
  invoiceId: number;
  date: string;
  items: CartLine[];
  paymentMethod: string;
  customerName: string;
  invoiceDiscount: number;
  grand: number;
  kind: 'فروش' | 'پیش‌فاکتور';
}

/** جمع‌هایِ یک ردیف — همان فرمولِ سرور: (تعداد×قیمت) − تخفیف، سپس مالیات رویِ خالص. */
function lineTotals(l: CartLine) {
  const sub = l.quantity * l.unitPrice;
  const disc = (sub * l.discountPct) / 100;
  const net = sub - disc;
  const tax = (net * l.taxPct) / 100;
  return { sub, disc, net, tax, total: net + tax };
}

/**
 * U-WEB-POS — صندوقِ فروشِ وب، پورتِ ساختاریِ design-system/screens/pos.html: تب‌هایِ دستهٔ کالا +
 * کاشیِ کالایِ لمسی + سبدِ ردیفی با استپرِ ±/جمع + دکمه‌هایِ پرداختِ مستقیم (نقدی/کارت‌خوان/چک/نسیه).
 * معادلِ جریانِ POSِ دسکتاپ رویِ همان APIها — ثبتِ نهایی از `POST /api/sales/pos`.
 */
export function PosPage() {
  const [products, setProducts] = useState<ProductRow[]>([]);
  const [groups, setGroups] = useState<ProductGroupDto[]>([]);
  const [groupId, setGroupId] = useState<number | null>(null);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [customerId, setCustomerId] = useState<number | null>(null);
  const [customerName, setCustomerName] = useState('متفرقه');
  const [customers, setCustomers] = useState<CustomerOption[]>([]);

  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 200);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [invoiceDiscount, setInvoiceDiscount] = useState(0);

  const [held, setHeld] = useState<HeldSaleListDto[]>([]);
  const [showHeld, setShowHeld] = useState(false);

  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState<ReceiptData | null>(null);

  useEffect(() => {
    Promise.all([
      apiGet<ProductRow[]>('/api/products'),
      apiGet<ProductGroupDto[]>('/api/products/groups'),
      apiGet<WarehouseDto[]>('/api/warehouse'),
      apiGet<CustomerOption[]>('/api/customers'),
    ])
      .then(([p, g, w, c]) => {
        setProducts(p.filter((x) => x.isActive));
        setGroups(g);
        setWarehouses(w);
        if (w.length > 0) setWarehouseId(w[0].id);
        setCustomers(c);
        const walkIn = c.find((x) => x.code === 'WALKIN') ?? c[0];
        if (walkIn) { setCustomerId(walkIn.id); setCustomerName(walkIn.name); }
      })
      .catch((e) => setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'خطا در بارگیریِ دادهٔ اولیه.' }));
    refreshHeld();
  }, []);

  function refreshHeld() {
    apiGet<HeldSaleListDto[]>('/api/heldsales').then(setHeld).catch(() => {});
  }

  const filtered = useMemo(() => {
    const term = debouncedSearch.trim();
    let list = products;
    if (groupId != null) list = list.filter((p) => p.groupId === groupId);
    if (term) list = list.filter((p) => p.name.includes(term) || p.code.includes(term));
    return list.slice(0, 40);
  }, [products, groupId, debouncedSearch]);

  function addToCart(p: ProductRow, quantity = 1) {
    setMsg(null);
    setCart((prev) => {
      const i = prev.findIndex((l) => l.productId === p.id);
      if (i >= 0) {
        const next = prev.slice();
        next[i] = { ...next[i], quantity: next[i].quantity + quantity };
        return next;
      }
      return [...prev, {
        productId: p.id, code: p.code, name: p.name,
        unitPrice: p.salePrice, taxPct: p.taxRate ?? 0, quantity, discountPct: 0,
      }];
    });
  }

  /** انتخابِ مشتری از کمبو — نام هم برایِ رسید/هدرِ فاکتور به‌روز می‌شود. */
  function selectCustomer(id: number | null) {
    setCustomerId(id);
    const c = customers.find((x) => x.id === id);
    setCustomerName(c ? c.name : 'متفرقه');
  }

  /** اسکنِ بارکد (Enter در کادر جست‌وجو): بارکد را واقعاً resolve می‌کند، نه فقط فیلترِ نام/کد. */
  async function resolveBarcodeAndAdd(code: string) {
    const hit = await apiGet<{
      productId: number; code: string; name: string; salePrice: number;
      taxRate: number; groupId: number | null; weighted: boolean; embeddedValue: number | null;
    }>(`/api/barcode/resolve?code=${encodeURIComponent(code)}`);
    const p = products.find((x) => x.id === hit.productId);
    if (!p) {
      // کالا در فهرستِ فعال نیست یا هنوز بارگیری نشده — از دادهٔ خودِ پاسخ استفاده کن.
      addToCart({
        id: hit.productId, groupId: hit.groupId, code: hit.code, name: hit.name,
        salePrice: hit.salePrice, taxRate: hit.taxRate, minStock: 0, isActive: true,
      }, hit.weighted && hit.embeddedValue ? hit.embeddedValue : 1);
      return;
    }
    addToCart(p, hit.weighted && hit.embeddedValue ? hit.embeddedValue : 1);
  }

  /** Enter در کادر جست‌وجو: اول بارکد resolve، بعدِ آن مطابقتِ دقیقِ کد/نام. */
  async function onSearchKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return;
    const code = search.trim();
    if (!code) return;
    e.preventDefault();
    setMsg(null);
    try {
      await resolveBarcodeAndAdd(code);
      setSearch('');
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        // بارکد نبود — مطابقتِ دقیقِ کد/نام به‌عنوانِ fallback (تایپِ دستیِ کدِ کالا).
        const exact = products.find((p) => p.code === code || p.name === code);
        if (exact) { addToCart(exact); setSearch(''); }
        else setMsg({ kind: 'error', text: `بارکد/کدِ «${code}» یافت نشد.` });
      } else {
        setMsg({ kind: 'error', text: err instanceof ApiError ? err.message : 'خطا در خواندنِ بارکد.' });
      }
    }
  }

  function changeQty(index: number, delta: number) {
    setCart((prev) => {
      const next = prev[index].quantity + delta;
      if (next <= 0) return prev.filter((_, i) => i !== index);
      return prev.map((l, i) => (i === index ? { ...l, quantity: next } : l));
    });
  }

  const totals = useMemo(() => {
    const t = cart.reduce(
      (acc, l) => {
        const x = lineTotals(l);
        return { sub: acc.sub + x.sub, disc: acc.disc + x.disc, net: acc.net + x.net, tax: acc.tax + x.tax, total: acc.total + x.total };
      },
      { sub: 0, disc: 0, net: 0, tax: 0, total: 0 },
    );
    return { ...t, grand: Math.max(0, t.total - invoiceDiscount) };
  }, [cart, invoiceDiscount]);

  /** پرداختِ مستقیم — مبلغِ دقیق، هم‌الگو با دکمه‌هایِ `.paygrid`ِ نمونهٔ سیستم‌طراحی (بدونِ فرمِ جدا).
   * `kind='پیش‌فاکتور'` هم از همین مسیر می‌رود ولی InvoiceType.Quotation=2 می‌فرستد (سند/موجودی نمی‌سازد). */
  async function pay(paymentMethod: 'نقدی' | 'بانک' | 'چک' | 'نسیه', kind: 'فروش' | 'پیش‌فاکتور' = 'فروش') {
    setMsg(null);
    if (cart.length === 0) {
      setMsg({ kind: 'error', text: 'سبد خالی است.' });
      return;
    }
    const isQuote = kind === 'پیش‌فاکتور';
    setBusy(true);
    try {
      const res = await apiPost<{ invoiceId: number }>('/api/sales/pos', {
        items: cart.map((l) => ({
          productId: l.productId,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          discountPct: l.discountPct,
          taxPct: l.taxPct,
        })),
        paid: isQuote ? 0 : (paymentMethod === 'نسیه' ? 0 : Math.round(totals.grand)),
        paymentMethod,
        customerId: customerId ?? 1,
        warehouseId: warehouseId ?? 1,
        discount: invoiceDiscount,
        otherCosts: 0,
        description: isQuote ? 'پیش‌فاکتور (کلاینتِ وب)' : 'فروشِ صندوق (کلاینتِ وب)',
        type: isQuote ? 2 : 0,   // InvoiceType.Quotation=2 / Sale=0
      });
      setMsg({ kind: 'success', text: isQuote
        ? `پیش‌فاکتور با موفقیت ثبت شد (شناسه: ${res.invoiceId}).`
        : `فاکتور با موفقیت ثبت شد (شناسه: ${res.invoiceId}).` });
      setReceipt({
        invoiceId: res.invoiceId,
        date: new Date().toLocaleString('fa-IR'),
        items: cart,
        paymentMethod,
        customerName,
        invoiceDiscount,
        grand: totals.grand,
        kind,
      });
      setCart([]);
      setInvoiceDiscount(0);
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ثبتِ فروش ناموفق بود.' });
    } finally {
      setBusy(false);
    }
  }

  /** چاپِ حرارتیِ رسید (۸۰mm) — همان دادهٔ رسیدِ رویِ صفحه را در پنجرهٔ چاپ می‌برد. */
  function printReceipt() {
    if (!receipt) return;
    printThermal({
      title: receipt.kind === 'پیش‌فاکتور' ? 'پیش‌فاکتور' : 'رسیدِ فروش',
      header: [
        { label: 'شماره', value: String(receipt.invoiceId) },
        { label: 'تاریخ', value: receipt.date },
        { label: 'مشتری', value: receipt.customerName },
        { label: 'پرداخت', value: receipt.kind === 'پیش‌فاکتور' ? '—' : receipt.paymentMethod },
      ],
      items: receipt.items.map((l) => {
        const t = lineTotals(l);
        return {
          name: l.name,
          qty: `${numberFormat.format(l.quantity)} × ${money(l.unitPrice)}`,
          amount: money(t.total),
        };
      }),
      totals: [
        { label: 'جمعِ اقلام', value: money(totals.sub) },
        ...(totals.disc > 0 ? [{ label: 'تخفیفِ سطری', value: `−${money(totals.disc)}` }] : []),
        ...(receipt.invoiceDiscount > 0 ? [{ label: 'تخفیفِ کلی', value: `−${money(receipt.invoiceDiscount)}` }] : []),
        ...(totals.tax > 0 ? [{ label: 'مالیات', value: money(totals.tax) }] : []),
        { label: 'مبلغِ کل', value: `${money(receipt.grand)} ریال`, bold: true },
      ],
      amountInWords: `${numberToPersianWords(receipt.grand)} ریال`,
      footer: ['ممنون از خریدِ شما — سما حساب'],
    });
  }

  async function hold() {
    setMsg(null);
    if (cart.length === 0) {
      setMsg({ kind: 'error', text: 'سبدِ خالی تعلیق نمی‌شود.' });
      return;
    }
    setBusy(true);
    try {
      const label = `سبدِ ${new Date().toLocaleTimeString('fa-IR')}`;
      await apiPost('/api/heldsales', { label, payload: JSON.stringify(cart), total: totals.grand });
      setMsg({ kind: 'success', text: 'سبد تعلیق شد.' });
      setCart([]);
      refreshHeld();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'تعلیقِ سبد ناموفق بود.' });
    } finally {
      setBusy(false);
    }
  }

  async function recall(id: number) {
    setMsg(null);
    try {
      const d = await apiGet<HeldSaleDetailDto>(`/api/heldsales/${id}`);
      setCart(JSON.parse(d.payload) as CartLine[]);
      await apiDelete(`/api/heldsales/${id}`);   // فراخوانی = برداشتن از صف
      setShowHeld(false);
      setMsg({ kind: 'success', text: `سبدِ «${d.label}» فراخوانی شد.` });
      refreshHeld();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'فراخوانیِ سبد ناموفق بود.' });
    }
  }

  function cancelCart() {
    if (cart.length === 0) return;
    if (!confirm('سبدِ جاری ابطال شود؟')) return;
    setCart([]);
    setInvoiceDiscount(0);
    setMsg(null);
  }

  function promptInvoiceDiscount() {
    const v = window.prompt('تخفیفِ کلیِ فاکتور (ریال):', String(invoiceDiscount));
    if (v == null) return;
    setInvoiceDiscount(Number(v) || 0);
  }

  const warehouseOptions: SearchSelectOption[] = warehouses.map((w) => ({ id: w.id, label: w.name }));
  const customerOptions: SearchSelectOption[] = customers.map((c) => ({ id: c.id, label: c.name, sublabel: c.code }));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <PageHeader
        title="صندوقِ فروش (POS)"
        actions={
          <button className="btn btn-secondary btn-sm" onClick={() => setShowHeld((s) => !s)}>
            سبدهایِ معلق ({numberFormat.format(held.length)})
          </button>
        }
      />

      {msg && <div style={{ marginBottom: 'var(--space-2)' }}><StatusMessage kind={msg.kind}>{msg.text}</StatusMessage></div>}

      {receipt && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', borderRadius: 'var(--radius-md)', padding: 'var(--space-4)', width: 340, maxHeight: '90vh', overflow: 'auto' }}>
            <div className="print-area">
              <div style={{ textAlign: 'center', marginBottom: 'var(--space-2)' }}>
                <div style={{ fontWeight: 700 }}>{receipt.kind === 'پیش‌فاکتور' ? 'پیش‌فاکتور' : 'رسیدِ فروش'}</div>
                <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>شماره: {receipt.invoiceId} — {receipt.date}</div>
                <div style={{ fontSize: 12 }}>مشتری: {receipt.customerName} — پرداخت: {receipt.kind === 'پیش‌فاکتور' ? '—' : receipt.paymentMethod}</div>
              </div>
              <div style={{ borderTop: '1px dashed #999', borderBottom: '1px dashed #999', padding: '6px 0' }}>
                {receipt.items.map((l) => {
                  const t = lineTotals(l);
                  return (
                    <div key={l.productId} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '3px 0' }}>
                      <span>{l.name} × {numberFormat.format(l.quantity)}</span>
                      <span className="num">{money(t.total)}</span>
                    </div>
                  );
                })}
              </div>
              {receipt.invoiceDiscount > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginTop: 6 }}>
                  <span>تخفیفِ کلی</span><span className="num">−{money(receipt.invoiceDiscount)}</span>
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700, marginTop: 6 }}>
                <span>مبلغِ کل</span><span className="num">{money(receipt.grand)} ریال</span>
              </div>
              {/* بارکدِ شمارهٔ فاکتور — رسید را برایِ پیگیری/مرجوعیِ اسکن‌محور قابلِ‌اسکن می‌کند. */}
              <div style={{ textAlign: 'center', marginTop: 10 }}>
                <Barcode value={`INV${receipt.invoiceId}`} moduleWidth={1.6} height={44} />
              </div>
            </div>
            <div className="no-print" style={{ display: 'flex', gap: 8, marginTop: 'var(--space-3)' }}>
              <button type="button" className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={printReceipt}>🖨 چاپِ حرارتی</button>
              <button type="button" className="btn btn-secondary btn-sm" style={{ flex: 1 }} onClick={() => setReceipt(null)}>بستن</button>
            </div>
          </div>
        </div>
      )}

      {showHeld && (
        <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-3)', marginBottom: 'var(--space-2)' }}>
          <div style={{ fontWeight: 600, marginBottom: 'var(--space-2)' }}>سبدهایِ معلق</div>
          {held.length === 0 && <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>سبدِ معلقی نیست.</div>}
          {held.map((h) => (
            <div key={h.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--gray-100)' }}>
              <span>{h.label}</span>
              <span className="num">{money(h.total)} ریال</span>
              <button className="btn btn-primary btn-sm" onClick={() => recall(h.id)}>فراخوانی</button>
            </div>
          ))}
        </div>
      )}

      <div className="pos">
        <div className="pos-left">
          <div className="cart">
            <div className="hd">
              فاکتورِ صندوق
              <span className="cust">مشتری: <b style={{ color: '#fff' }}>{customerName}</b></span>
            </div>
            <div className="rows">
              {cart.map((l, i) => (
                <div className="crow" key={l.productId}>
                  <div className="nm">
                    <div className="t">{l.name}</div>
                    <div className="u">{numberFormat.format(l.quantity)} × {money(l.unitPrice)}{l.discountPct > 0 ? ' − تخفیف' : ''}</div>
                  </div>
                  <div className="qty">
                    <button type="button" onClick={() => changeQty(i, -1)}>−</button>
                    <span className="q">{numberFormat.format(l.quantity)}</span>
                    <button type="button" onClick={() => changeQty(i, 1)}>+</button>
                  </div>
                  <div className="amt">{money(lineTotals(l).total)}</div>
                </div>
              ))}
              {cart.length === 0 && (
                <div style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 12.5 }}>سبد خالی است — رویِ یک کالا بزنید.</div>
              )}
            </div>
            <div className="totals">
              <div className="r"><span>جمعِ اقلام ({numberFormat.format(cart.length)})</span><span className="v">{money(totals.sub)}</span></div>
              <div className="r"><span>تخفیفِ سطری</span><span className="v" style={{ color: 'var(--danger-500)' }}>−{money(totals.disc)}</span></div>
              <div className="r">
                <span>تخفیفِ کلی</span>
                <span className="v" style={{ cursor: 'pointer', textDecoration: 'underline dotted' }} onClick={promptInvoiceDiscount}>
                  {invoiceDiscount > 0 ? `−${money(invoiceDiscount)}` : 'تعیین'}
                </span>
              </div>
              <div className="r"><span>مالیات</span><span className="v">{money(totals.tax)}</span></div>
              <div className="grand"><span className="l">مبلغِ قابلِ پرداخت</span><span className="v">{money(totals.grand)}</span></div>
            </div>
          </div>

          <div className="paygrid">
            <button type="button" className="pb cash" disabled={busy || cart.length === 0} onClick={() => pay('نقدی')}>
              پرداختِ نقدی
            </button>
            <button type="button" className="pb" disabled={busy || cart.length === 0} onClick={() => pay('بانک')}>کارت‌خوان</button>
            <button type="button" className="pb" disabled={busy || cart.length === 0} onClick={() => pay('نسیه')}>نسیه</button>
            <button type="button" className="pb" disabled={busy || cart.length === 0} onClick={() => pay('چک')}>چک</button>
            <button type="button" className="pb quote" disabled={busy || cart.length === 0} onClick={() => pay('نقدی', 'پیش‌فاکتور')}>
              پیش‌فاکتور (بدونِ ثبتِ سند/موجودی)
            </button>
          </div>
          <div className="fnrow">
            <button type="button" className="fb" disabled={busy} onClick={hold}>تعلیقِ فاکتور</button>
            <button type="button" className="fb" disabled={busy} onClick={() => setShowHeld((s) => !s)}>بازیابی ({numberFormat.format(held.length)})</button>
            <button type="button" className="fb" disabled={busy} onClick={promptInvoiceDiscount}>تخفیفِ کلی</button>
            <button type="button" className="fb warn" disabled={busy || cart.length === 0} onClick={cancelCart}>ابطال</button>
          </div>
        </div>

        <div className="pos-right">
          <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
            <div style={{ flex: 1 }}>
              <SearchSelect options={warehouseOptions} value={warehouseId} onChange={setWarehouseId} placeholder="انبار…" />
            </div>
            <div style={{ flex: 1 }}>
              <SearchSelect options={customerOptions} value={customerId} onChange={selectCustomer} placeholder="مشتری (پیش‌فرض: متفرقه)…" />
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <input className="input-c" style={{ flex: 1, height: 44, fontSize: 14 }}
              placeholder="🔍 اسکن بارکد (Enter) یا جست‌وجویِ کالا…" value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={onSearchKeyDown} autoFocus />
          </div>
          <div className="cats">
            <button type="button" className={`cat${groupId == null ? ' on' : ''}`} onClick={() => setGroupId(null)}>همه</button>
            {groups.map((g) => (
              <button key={g.id} type="button" className={`cat${g.id === groupId ? ' on' : ''}`} onClick={() => setGroupId(g.id)}>
                {g.name}
              </button>
            ))}
          </div>
          <div className="pgrid">
            {filtered.map((p) => (
              <div key={p.id} className={`pitem${p.minStock > 0 ? ' low' : ''}`} onClick={() => addToCart(p)}>
                <div className="n">{p.name}</div>
                <div>
                  <div className="p">{money(p.salePrice)} ریال</div>
                  <div className="s">{p.code}</div>
                </div>
              </div>
            ))}
            {filtered.length === 0 && <div style={{ color: 'var(--text-muted)', padding: 12 }}>کالایی یافت نشد.</div>}
          </div>
        </div>
      </div>
    </div>
  );
}
