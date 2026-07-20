import { useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { money, numberFormat } from '../lib/format';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ProductRow {
  id: number;
  code: string;
  name: string;
  salePrice: number;
  taxRate: number;
}

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

/** جمع‌هایِ یک ردیف — همان فرمولِ سرور: (تعداد×قیمت) − تخفیف، سپس مالیات رویِ خالص. */
function lineTotals(l: CartLine) {
  const sub = l.quantity * l.unitPrice;
  const disc = (sub * l.discountPct) / 100;
  const net = sub - disc;
  const tax = (net * l.taxPct) / 100;
  return { sub, disc, net, tax, total: net + tax };
}

/**
 * U-WEB-POS — صندوقِ فروشِ وب. معادلِ جریانِ POSِ دسکتاپ رویِ همان APIها:
 * جست‌وجو/افزودن به سبد · تعداد · تخفیفِ ردیف · تعلیق (Hold) و فراخوان (Recall) · پرداخت.
 * ثبتِ نهایی از `POST /api/sales/pos` (سرور خودش تاریخ/شعبه/سالِ مالی و سندِ حسابداری را می‌سازد).
 */
export function PosPage() {
  const [products, setProducts] = useState<ProductRow[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [customerId, setCustomerId] = useState<number | null>(null);

  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 200);
  const [cart, setCart] = useState<CartLine[]>([]);

  const [invoiceDiscount, setInvoiceDiscount] = useState('0');
  const [paymentMethod, setPaymentMethod] = useState('نقدی');
  const [paid, setPaid] = useState('0');

  const [held, setHeld] = useState<HeldSaleListDto[]>([]);
  const [showHeld, setShowHeld] = useState(false);

  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    Promise.all([
      apiGet<ProductRow[]>('/api/products/list'),
      apiGet<WarehouseDto[]>('/api/warehouse'),
      apiGet<CustomerOption[]>('/api/customers'),
    ])
      .then(([p, w, c]) => {
        setProducts(p);
        setWarehouses(w);
        setCustomers(c);
        if (w.length > 0) setWarehouseId(w[0].id);
        // مشتریِ پیش‌فرض: «متفرقه/نقدی» اگر بود، وگرنه اولین مشتری.
        const walkIn = c.find((x) => x.code === 'WALKIN') ?? c[0];
        if (walkIn) setCustomerId(walkIn.id);
      })
      .catch((e) => setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'خطا در بارگیریِ دادهٔ اولیه.' }));
    refreshHeld();
  }, []);

  function refreshHeld() {
    apiGet<HeldSaleListDto[]>('/api/heldsales').then(setHeld).catch(() => {});
  }

  const filtered = useMemo(() => {
    const term = debouncedSearch.trim();
    if (!term) return products.slice(0, 24);
    return products.filter((p) => p.name.includes(term) || p.code.includes(term)).slice(0, 24);
  }, [products, debouncedSearch]);

  function addToCart(p: ProductRow) {
    setCart((prev) => {
      const i = prev.findIndex((l) => l.productId === p.id);
      if (i >= 0) {
        const next = prev.slice();
        next[i] = { ...next[i], quantity: next[i].quantity + 1 };
        return next;
      }
      return [...prev, {
        productId: p.id, code: p.code, name: p.name,
        unitPrice: p.salePrice, taxPct: p.taxRate ?? 0, quantity: 1, discountPct: 0,
      }];
    });
  }

  function updateLine(index: number, patch: Partial<CartLine>) {
    setCart((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  }

  function removeLine(index: number) {
    setCart((prev) => prev.filter((_, i) => i !== index));
  }

  const totals = useMemo(() => {
    const t = cart.reduce(
      (acc, l) => {
        const x = lineTotals(l);
        return { net: acc.net + x.net, tax: acc.tax + x.tax, total: acc.total + x.total };
      },
      { net: 0, tax: 0, total: 0 },
    );
    const invDisc = Number(invoiceDiscount) || 0;
    return { ...t, grand: Math.max(0, t.total - invDisc) };
  }, [cart, invoiceDiscount]);

  async function checkout() {
    setMsg(null);
    if (cart.length === 0) {
      setMsg({ kind: 'error', text: 'سبد خالی است.' });
      return;
    }
    if (paymentMethod !== 'نسیه' && (Number(paid) || 0) < totals.grand) {
      setMsg({ kind: 'error', text: `مبلغِ دریافتی کافی نیست (لازم: ${money(totals.grand)} ریال).` });
      return;
    }
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
        paid: paymentMethod === 'نسیه' ? 0 : Number(paid) || 0,
        paymentMethod,
        customerId: customerId ?? 1,
        warehouseId: warehouseId ?? 1,
        discount: Number(invoiceDiscount) || 0,
        otherCosts: 0,
        description: 'فروشِ صندوق (کلاینتِ وب)',
      });
      setMsg({ kind: 'success', text: `فاکتور با موفقیت ثبت شد (شناسه: ${res.invoiceId}).` });
      setCart([]);
      setInvoiceDiscount('0');
      setPaid('0');
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ثبتِ فروش ناموفق بود.' });
    } finally {
      setBusy(false);
    }
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

  return (
    <div>
      <PageHeader
        title="صندوقِ فروش (POS)"
        actions={
          <>
            <button className="btn btn-secondary btn-sm" onClick={() => setShowHeld((s) => !s)}>
              سبدهایِ معلق ({numberFormat.format(held.length)})
            </button>
            <button className="btn btn-secondary btn-sm" onClick={hold} disabled={busy}>
              تعلیقِ سبد
            </button>
          </>
        }
      />

      {msg && <div style={{ marginBottom: 'var(--space-3)' }}><StatusMessage kind={msg.kind}>{msg.text}</StatusMessage></div>}

      {showHeld && (
        <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-3)', marginBottom: 'var(--space-4)' }}>
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

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 420px', gap: 'var(--space-4)', alignItems: 'start' }}>
        {/* ── انتخابِ کالا ── */}
        <div>
          <input
            className="input"
            placeholder="جست‌وجویِ کالا (نام/کد)… سپس روی کالا بزنید"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ marginBottom: 'var(--space-3)' }}
            autoFocus
          />
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: 'var(--space-2)' }}>
            {filtered.map((p) => (
              <button
                key={p.id}
                onClick={() => addToCart(p)}
                style={{
                  textAlign: 'start', padding: '10px 12px', cursor: 'pointer',
                  background: 'var(--bg-surface)', border: '1px solid var(--border-strong)',
                  borderRadius: 'var(--radius-sm)', fontFamily: 'var(--font-sans)',
                }}
              >
                <div style={{ fontSize: 'var(--text-sm)', fontWeight: 600, color: 'var(--text-strong)' }}>{p.name}</div>
                <div className="num" style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)' }}>{p.code}</div>
                <div className="num" style={{ fontSize: 'var(--text-sm)', color: 'var(--blue-700)', marginTop: 4 }}>{money(p.salePrice)}</div>
              </button>
            ))}
            {filtered.length === 0 && <div style={{ color: 'var(--text-muted)' }}>کالایی یافت نشد.</div>}
          </div>
        </div>

        {/* ── سبد و پرداخت ── */}
        <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-3)' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-2)', marginBottom: 'var(--space-3)' }}>
            <div className="field">
              <label className="label">مشتری</label>
              <select className="select select-sm" value={customerId ?? ''} onChange={(e) => setCustomerId(Number(e.target.value))}>
                {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label className="label">انبار</label>
              <select className="select select-sm" value={warehouseId ?? ''} onChange={(e) => setWarehouseId(Number(e.target.value))}>
                {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 'var(--space-2)' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                {['کالا', 'تعداد', 'تخفیف٪', 'جمع', ''].map((h) => (
                  <th key={h} style={{ padding: '6px 4px', fontSize: 'var(--text-xs)', color: 'var(--text-muted)', textAlign: 'start' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {cart.map((l, i) => (
                <tr key={l.productId} style={{ borderBottom: '1px solid var(--gray-100)' }}>
                  <td style={{ padding: '5px 4px', fontSize: 'var(--text-sm)' }}>{l.name}</td>
                  <td style={{ padding: '5px 4px' }}>
                    <input className="input input-sm" type="number" min="0" step="any" style={{ width: 62 }}
                      value={l.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) || 0 })} />
                  </td>
                  <td style={{ padding: '5px 4px' }}>
                    <input className="input input-sm" type="number" min="0" max="100" style={{ width: 58 }}
                      value={l.discountPct} onChange={(e) => updateLine(i, { discountPct: Number(e.target.value) || 0 })} />
                  </td>
                  <td className="num" style={{ padding: '5px 4px', fontSize: 'var(--text-sm)', fontWeight: 600, whiteSpace: 'nowrap' }}>
                    {money(lineTotals(l).total)}
                  </td>
                  <td style={{ padding: '5px 4px' }}>
                    <button className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>×</button>
                  </td>
                </tr>
              ))}
              {cart.length === 0 && (
                <tr><td colSpan={5} style={{ padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)' }}>سبد خالی است.</td></tr>
              )}
            </tbody>
          </table>

          <div style={{ borderTop: '1px solid var(--border)', paddingTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }}>
            <Row label="جمعِ خالص" value={money(totals.net)} />
            <Row label="مالیات" value={money(totals.tax)} />
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '4px 0' }}>
              <span style={{ color: 'var(--text-muted)' }}>تخفیفِ فاکتور</span>
              <input className="input input-sm" type="number" min="0" style={{ width: 120 }}
                value={invoiceDiscount} onChange={(e) => setInvoiceDiscount(e.target.value)} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', fontSize: 'var(--text-md)', fontWeight: 700, borderTop: '1px solid var(--gray-100)' }}>
              <span>قابلِ پرداخت</span>
              <span className="num">{money(totals.grand)} ریال</span>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-2)', marginTop: 'var(--space-2)' }}>
            <div className="field">
              <label className="label">روشِ پرداخت</label>
              <select className="select select-sm" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
                <option value="نقدی">نقدی</option>
                <option value="بانک">بانک</option>
                <option value="چک">چک</option>
                <option value="نسیه">نسیه</option>
              </select>
            </div>
            <div className="field">
              <label className="label">دریافتی</label>
              <input className="input input-sm" type="number" min="0" value={paid}
                onChange={(e) => setPaid(e.target.value)} disabled={paymentMethod === 'نسیه'} />
            </div>
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 'var(--space-3)' }}>
            <button className="btn btn-secondary btn-sm" onClick={() => setPaid(String(Math.round(totals.grand)))} disabled={paymentMethod === 'نسیه'}>
              مبلغِ دقیق
            </button>
            <button className="btn btn-primary" style={{ flex: 1 }} onClick={checkout} disabled={busy}>
              {busy ? 'در حالِ ثبت…' : 'ثبتِ فروش'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0' }}>
      <span style={{ color: 'var(--text-muted)' }}>{label}</span>
      <span className="num">{value}</span>
    </div>
  );
}
