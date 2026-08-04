import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { QuickCreateModal } from '../components/QuickCreateModal';
import { InvoiceLineEditor, emptyLine, computeInvoiceTotals, type InvoiceLine, type ProductOption } from '../components/InvoiceLineEditor';
import { InvoiceSidePanel } from '../components/InvoiceSidePanel';
import { useAuth } from '../auth/AuthContext';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';
import { todayJalaliString } from '../lib/jalali';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';

interface CustomerOption {
  id: number;
  name: string;
  code: string;
}

interface WarehouseOption {
  id: number;
  name: string;
}

export function CreateSalesInvoicePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const fiscalYearId = useActiveFiscalYear();
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([]);
  const [products, setProducts] = useState<ProductOption[]>([]);

  const presetCustomerId = Number(searchParams.get('customerId')) || null;
  const [customerId, setCustomerId] = useState<number | null>(presetCustomerId);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [invoiceDate, setInvoiceDate] = useState(todayJalaliString());
  const [paymentMethod, setPaymentMethod] = useState('نسیه');
  const [paidAmount, setPaidAmount] = useState('0');
  const [lines, setLines] = useState<InvoiceLine[]>([emptyLine()]);
  const [invoiceDiscount, setInvoiceDiscount] = useState('0');
  const [shipping, setShipping] = useState('0');
  const [otherCosts, setOtherCosts] = useState('0');
  const [notes, setNotes] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [quickAddCustomer, setQuickAddCustomer] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      apiGet<CustomerOption[]>('/api/customers'),
      apiGet<WarehouseOption[]>('/api/warehouse'),
      apiGet<ProductOption[]>('/api/products/list'),
    ]).then(([c, w, p]) => {
      setCustomers(c);
      setWarehouses(w);
      setProducts(p);
      if (w.length > 0) setWarehouseId(w[0].id);
    });
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!customerId || !warehouseId) {
      setError('انتخابِ مشتری و انبار الزامی است.');
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
      await apiPost<{ invoiceId: number }>('/api/sales/invoices', {
        branchId: user?.branchId ?? 1,
        fiscalYearId: fiscalYearId ?? 1,
        invoiceDate,
        customerId,
        warehouseId,
        invoiceType: 0, // Sale
        priceLevel: 'خرده',
        description: notes || null,
        shipping: Number(shipping) || 0,
        otherCosts: Number(otherCosts) || 0,
        invoiceDiscount: Number(invoiceDiscount) || 0,
        items,
        paidAmount: paymentMethod === 'نسیه' ? 0 : Number(paidAmount) || 0,
        paymentMethod,
      });
      navigate('/sales', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ثبتِ فاکتور ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  const totals = computeInvoiceTotals(lines);
  const grandTotal = totals.itemsTotal - (Number(invoiceDiscount) || 0) + (Number(shipping) || 0) + (Number(otherCosts) || 0);

  return (
    <div>
      <PageHeader title="فاکتورِ فروشِ نو" />
      <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {/* پورتِ `.gbox`ِ design-system برایِ مشخصاتِ فاکتور (sales-invoice.html) */}
        <div className="gbox">
          <div className="gh">مشخصاتِ فاکتور</div>
          <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
            <div className="field">
              <label className="label">مشتری</label>
              <SearchSelect
                options={customers.map((c) => ({ id: c.id, label: c.name, sublabel: c.code }))}
                value={customerId}
                onChange={setCustomerId}
                placeholder="جست‌وجویِ مشتری…"
                createNewLabel="مشتریِ جدید"
                onCreateNew={(q) => setQuickAddCustomer(q)}
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
              <label className="label">روشِ پرداخت</label>
              <select className="select" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
                <option value="نسیه">نسیه</option>
                <option value="نقدی">نقدی</option>
                <option value="بانک">بانک</option>
                <option value="چک">چک</option>
              </select>
            </div>
            {paymentMethod !== 'نسیه' && (
              <div className="field">
                <label className="label">مبلغِ دریافتی</label>
                <input className="input" type="number" min="0" value={paidAmount} onChange={(e) => setPaidAmount(e.target.value)} />
              </div>
            )}
          </div>
        </div>

        <div className="inv-layout">
          <div className="inv-left">
            <InvoiceLineEditor products={products} lines={lines} onChange={setLines} priceField="salePrice"
              onProductCreated={(p) => setProducts((prev) => [...prev, p])} />
          </div>
          <InvoiceSidePanel
            itemsSubtotal={totals.subTotal}
            lineDiscount={totals.lineDiscount}
            tax={totals.tax}
            invoiceDiscount={Number(invoiceDiscount) || 0}
            onInvoiceDiscountChange={setInvoiceDiscount}
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
          {submitting ? 'در حالِ ثبت…' : 'ثبتِ فاکتور'}
        </button>
      </form>

      {quickAddCustomer !== null && (
        <QuickCreateModal
          kind="customer"
          initialName={quickAddCustomer}
          onClose={() => setQuickAddCustomer(null)}
          onCreated={(item) => {
            setCustomers((prev) => [...prev, { id: item.id, name: item.name, code: item.code }]);
            setCustomerId(item.id);
            setQuickAddCustomer(null);
          }}
        />
      )}
    </div>
  );
}
