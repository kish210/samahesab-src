import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { InvoiceLineEditor, emptyLine, type InvoiceLine, type ProductOption } from '../components/InvoiceLineEditor';
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

export function CreatePurchaseInvoicePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const fiscalYearId = useActiveFiscalYear();
  const [suppliers, setSuppliers] = useState<SupplierOption[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([]);
  const [products, setProducts] = useState<ProductOption[]>([]);

  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [invoiceDate, setInvoiceDate] = useState(todayJalaliString());
  const [paidAmount, setPaidAmount] = useState('0');
  const [lines, setLines] = useState<InvoiceLine[]>([emptyLine()]);

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    Promise.all([
      apiGet<SupplierOption[]>('/api/suppliers'),
      apiGet<WarehouseOption[]>('/api/warehouse'),
      apiGet<ProductOption[]>('/api/products/list'),
    ]).then(([s, w, p]) => {
      setSuppliers(s);
      setWarehouses(w);
      setProducts(p);
      if (w.length > 0) setWarehouseId(w[0].id);
    });
  }, []);

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
      await apiPost('/api/purchase/invoices', {
        branchId: user?.branchId ?? 1,
        fiscalYearId: fiscalYearId ?? 1,
        invoiceDate,
        supplierId,
        warehouseId,
        invoiceType: 'فاکتور خرید',
        shipping: 0,
        otherCosts: 0,
        items,
        paidAmount: Number(paidAmount) || 0,
      });
      navigate('/purchase', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ثبتِ فاکتور ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div>
      <PageHeader title="فاکتورِ خریدِ نو" />
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

        <InvoiceLineEditor products={products} lines={lines} onChange={setLines} priceField="purchasePrice" />

        {error && (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <StatusMessage kind="error">{error}</StatusMessage>
          </div>
        )}

        <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-4)' }}>
          {submitting ? 'در حالِ ثبت…' : 'ثبتِ فاکتور'}
        </button>
      </form>
    </div>
  );
}
