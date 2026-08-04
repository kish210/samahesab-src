import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { QuickCreateModal } from '../components/QuickCreateModal';
import { InvoiceLineEditor, emptyLine, computeInvoiceTotals, type InvoiceLine, type ProductOption } from '../components/InvoiceLineEditor';
import { InvoiceSidePanel } from '../components/InvoiceSidePanel';
import { useAuth } from '../auth/AuthContext';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';
import { todayJalaliString } from '../lib/jalali';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';

interface SupplierOption {
  id: number;
  name: string;
  code: string;
}

interface WarehouseOption {
  id: number;
  name: string;
}

interface PurchaseInvoiceDetailDto {
  id: number; number: string; date: string; supplierId: number; warehouseId: number;
  shipping: number; otherCosts: number; paidAmount: number; description: string | null;
  items: { productId: number; quantity: number; unitPrice: number; discountPct: number; taxPct: number }[];
}

/** قرینهٔ CreateSalesInvoicePage — هم ثبتِ فاکتورِ نو، هم (روی مسیرِ `purchase/invoices/:id/edit`)
 * ویرایش (U-WEB-INV-EDIT، از طریقِ `EditPurchaseInvoiceCommand`). */
export function CreatePurchaseInvoicePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { id: editId } = useParams<{ id: string }>();
  const isEdit = !!editId;
  const fiscalYearId = useActiveFiscalYear();
  const [suppliers, setSuppliers] = useState<SupplierOption[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([]);
  const [products, setProducts] = useState<ProductOption[]>([]);

  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [invoiceDate, setInvoiceDate] = useState(todayJalaliString());
  const [paidAmount, setPaidAmount] = useState('0');
  const [lines, setLines] = useState<InvoiceLine[]>([emptyLine()]);
  const [shipping, setShipping] = useState('0');
  const [otherCosts, setOtherCosts] = useState('0');
  const [notes, setNotes] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [quickAddSupplier, setQuickAddSupplier] = useState<string | null>(null);
  const [loadingInvoice, setLoadingInvoice] = useState(isEdit);
  const [originalNumber, setOriginalNumber] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      apiGet<SupplierOption[]>('/api/suppliers'),
      apiGet<WarehouseOption[]>('/api/warehouse'),
      apiGet<ProductOption[]>('/api/products/list'),
    ]).then(([s, w, p]) => {
      setSuppliers(s);
      setWarehouses(w);
      setProducts(p);
      if (!isEdit && w.length > 0) setWarehouseId(w[0].id);
    });
  }, [isEdit]);

  useEffect(() => {
    if (!isEdit) return;
    apiGet<PurchaseInvoiceDetailDto>(`/api/purchase/invoices/${editId}`)
      .then((inv) => {
        setOriginalNumber(inv.number);
        setSupplierId(inv.supplierId);
        setWarehouseId(inv.warehouseId);
        setInvoiceDate(inv.date);
        setShipping(String(inv.shipping || 0));
        setOtherCosts(String(inv.otherCosts || 0));
        setNotes(inv.description ?? '');
        setLines(inv.items.length > 0
          ? inv.items.map((it) => ({
              productId: it.productId, quantity: String(it.quantity), unitPrice: String(it.unitPrice),
              discountPct: String(it.discountPct), taxPct: String(it.taxPct),
            }))
          : [emptyLine()]);
        if (inv.paidAmount !== 0) {
          setError('این فاکتور پرداختی/دریافتیِ ثبت‌شده دارد — ابتدا از «دریافت/پرداخت» یا مرجوعی، آن را برگردانید، سپس ویرایش کنید.');
        }
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'بارگیریِ فاکتور برایِ ویرایش ناموفق بود.'))
      .finally(() => setLoadingInvoice(false));
  }, [isEdit, editId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!supplierId || !warehouseId) {
      setError('انتخابِ تأمین‌کننده و انبار الزامی است.');
      return;
    }
    if (!isValidJalali(invoiceDate)) {
      setError('تاریخِ فاکتور معتبر نیست (قالب: ۱۴۰۵/۰۴/۲۶).');
      return;
    }
    const items = lines
      .filter((l) => l.productId)
      .map((l) => ({
        productId: l.productId!,
        quantity: Number(l.quantity) || 0,
        unitPrice: Number(l.unitPrice) || 0,
        discountPct: Number(l.discountPct) || 0,
        taxPct: Number(l.taxPct) || 0,
      }));
    if (items.length === 0) {
      setError('فاکتور باید حداقل یک ردیفِ کالا داشته باشد.');
      return;
    }

    setSubmitting(true);
    try {
      if (isEdit) {
        await apiPut(`/api/purchase/invoices/${editId}`, {
          invoiceId: Number(editId),
          invoiceDate,
          supplierId,
          warehouseId,
          description: notes || null,
          shipping: Number(shipping) || 0,
          otherCosts: Number(otherCosts) || 0,
          items,
          paidAmount: Number(paidAmount) || 0,
        });
      } else {
        await apiPost('/api/purchase/invoices', {
          branchId: user?.branchId ?? 1,
          fiscalYearId: fiscalYearId ?? 1,
          invoiceDate,
          supplierId,
          warehouseId,
          invoiceType: 'فاکتور خرید',
          description: notes || null,
          shipping: Number(shipping) || 0,
          otherCosts: Number(otherCosts) || 0,
          items,
          paidAmount: Number(paidAmount) || 0,
        });
      }
      navigate('/purchase', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ثبتِ فاکتور ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  const totals = computeInvoiceTotals(lines);
  const grandTotal = totals.itemsTotal + (Number(shipping) || 0) + (Number(otherCosts) || 0);

  if (loadingInvoice) return <StatusMessage kind="muted">در حالِ بارگیریِ فاکتور…</StatusMessage>;

  return (
    <div>
      <PageHeader title={isEdit ? `ویرایشِ فاکتورِ خرید — ${originalNumber ?? ''}` : 'فاکتورِ خریدِ نو'} />
      {isEdit && (
        <p style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)', marginBottom: 'var(--space-3)' }}>
          ذخیره یعنی فاکتورِ اصلیِ «{originalNumber}» به‌طورِ کامل مرجوع می‌شود و یک فاکتورِ نو با دادهٔ زیر صادر می‌شود
          (فاکتورِ اصلی به‌عنوانِ سابقهٔ تاریخی می‌ماند).
        </p>
      )}
      <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {/* پورتِ `.gbox`ِ design-system برایِ مشخصاتِ فاکتور (هم‌الگو با sales-invoice.html) */}
        <div className="gbox">
          <div className="gh">مشخصاتِ فاکتور</div>
          <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
            <div className="field">
              <label className="label">تأمین‌کننده</label>
              <SearchSelect
                options={suppliers.map((s) => ({ id: s.id, label: s.name, sublabel: s.code }))}
                value={supplierId}
                onChange={setSupplierId}
                placeholder="جست‌وجویِ تأمین‌کننده…"
                createNewLabel="تأمین‌کنندهٔ جدید"
                onCreateNew={(q) => setQuickAddSupplier(q)}
              />
            </div>
            <div className="field">
              <label className="label">انبار</label>
              <select className="select" value={warehouseId ?? ''} onChange={(e) => setWarehouseId(Number(e.target.value))}>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </div>
            <JalaliDateInput label="تاریخِ فاکتور" value={invoiceDate} onChange={setInvoiceDate} />
            <div className="field">
              <label className="label">مبلغِ پرداختی (نقد)</label>
              <input className="input" type="number" min="0" value={paidAmount} onChange={(e) => setPaidAmount(e.target.value)} />
            </div>
          </div>
        </div>

        <div className="inv-layout">
          <div className="inv-left">
            <InvoiceLineEditor products={products} lines={lines} onChange={setLines} priceField="purchasePrice"
              onProductCreated={(p) => setProducts((prev) => [...prev, p])} />
          </div>
          <InvoiceSidePanel
            itemsSubtotal={totals.subTotal}
            lineDiscount={totals.lineDiscount}
            tax={totals.tax}
            shipping={shipping}
            onShippingChange={setShipping}
            otherCosts={otherCosts}
            onOtherCostsChange={setOtherCosts}
            grandTotal={grandTotal}
            notes={notes}
            onNotesChange={setNotes}
          />
        </div>

        {error && (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <StatusMessage kind="error">{error}</StatusMessage>
          </div>
        )}

        <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-4)' }}>
          {submitting ? 'در حالِ ثبت…' : isEdit ? 'ذخیرهٔ ویرایش' : 'ثبتِ فاکتور'}
        </button>
      </form>

      {quickAddSupplier !== null && (
        <QuickCreateModal
          kind="supplier"
          initialName={quickAddSupplier}
          onClose={() => setQuickAddSupplier(null)}
          onCreated={(item) => {
            setSuppliers((prev) => [...prev, { id: item.id, name: item.name, code: item.code }]);
            setSupplierId(item.id);
            setQuickAddSupplier(null);
          }}
        />
      )}
    </div>
  );
}
