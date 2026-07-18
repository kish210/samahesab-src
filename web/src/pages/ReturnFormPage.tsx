import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';

import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';
import { InvoiceLineEditor, emptyLine, type InvoiceLine, type ProductOption } from '../components/InvoiceLineEditor';
import { useAuth } from '../auth/AuthContext';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';
import { todayJalaliString } from '../lib/jalali';

interface PartyOption {
  id: number;
  name: string;
  code: string;
}

interface WarehouseOption {
  id: number;
  name: string;
}

type Mode = 'sales' | 'purchase';

const CONFIG: Record<Mode, {
  title: string;
  partyLabel: string;
  partyEndpoint: string;
  submitEndpoint: string;
  partyKey: 'customerId' | 'supplierId';
  priceField: 'salePrice' | 'purchasePrice';
  backTo: string;
  refundLabel: string;
}> = {
  sales: {
    title: 'مرجوعیِ فروش',
    partyLabel: 'مشتری',
    partyEndpoint: '/api/customers',
    submitEndpoint: '/api/sales/returns',
    partyKey: 'customerId',
    priceField: 'salePrice',
    backTo: '/sales',
    refundLabel: 'بازپرداختِ نقدی (وگرنه از بدهیِ مشتری کم می‌شود)',
  },
  purchase: {
    title: 'مرجوعیِ خرید',
    partyLabel: 'تأمین‌کننده',
    partyEndpoint: '/api/suppliers',
    submitEndpoint: '/api/purchase/returns',
    partyKey: 'supplierId',
    priceField: 'purchasePrice',
    backTo: '/purchase',
    refundLabel: 'دریافتِ نقدی (وگرنه از بدهی به تأمین‌کننده کم می‌شود)',
  },
};

/**
 * U-WEB-RETURN — فرمِ مرجوعیِ فروش/خرید. تا پیش از این فقط API داشت و در UIِ وب نبود.
 * هر دو Command شکلِ یکسانی دارند (فقط customerId/supplierId فرق دارد) پس یک کامپوننتِ
 * پارامتری هر دو را پوشش می‌دهد. توجه: Commandِ مرجوعی **تخفیفِ ردیف ندارد** ⇒ ستونش پنهان است.
 */
export function ReturnFormPage({ mode }: { mode: Mode }) {
  const cfg = CONFIG[mode];
  const { user } = useAuth();
  const navigate = useNavigate();
  const fiscalYearId = useActiveFiscalYear();

  const [parties, setParties] = useState<PartyOption[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([]);
  const [products, setProducts] = useState<ProductOption[]>([]);

  const [partyId, setPartyId] = useState<number | null>(null);
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [date, setDate] = useState(todayJalaliString());
  const [refundCash, setRefundCash] = useState(false);
  const [description, setDescription] = useState('');
  const [lines, setLines] = useState<InvoiceLine[]>([emptyLine()]);

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    Promise.all([
      apiGet<PartyOption[]>(cfg.partyEndpoint),
      apiGet<WarehouseOption[]>('/api/warehouse'),
      apiGet<ProductOption[]>('/api/products/list'),
    ])
      .then(([p, w, pr]) => {
        setParties(p);
        setWarehouses(w);
        setProducts(pr);
        if (w.length > 0) setWarehouseId(w[0].id);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ دادهٔ اولیه.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!partyId || !warehouseId) {
      setError(`انتخابِ ${cfg.partyLabel} و انبار الزامی است.`);
      return;
    }
    if (!isValidJalali(date)) {
      setError('تاریخِ مرجوعی معتبر نیست (قالب: ۱۴۰۵/۰۴/۲۶).');
      return;
    }
    // Commandِ مرجوعی فقط این چهار فیلد را می‌گیرد (بدونِ تخفیفِ ردیف).
    const items = lines
      .filter((l) => l.productId)
      .map((l) => ({
        productId: l.productId!,
        quantity: Number(l.quantity) || 0,
        unitPrice: Number(l.unitPrice) || 0,
        taxPct: Number(l.taxPct) || 0,
      }));
    if (items.length === 0) {
      setError('مرجوعی باید حداقل یک ردیفِ کالا داشته باشد.');
      return;
    }
    if (items.some((i) => i.quantity <= 0)) {
      setError('مقدارِ هر ردیف باید بزرگ‌تر از صفر باشد.');
      return;
    }

    setSubmitting(true);
    try {
      await apiPost(cfg.submitEndpoint, {
        branchId: user?.branchId ?? 1,
        fiscalYearId: fiscalYearId ?? 1,
        date,
        [cfg.partyKey]: partyId,
        warehouseId,
        items,
        description: description || null,
        refundCash,
        originalInvoiceId: null,
      });
      navigate(cfg.backTo, { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ثبتِ مرجوعی ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div>
      <PageHeader title={cfg.title} />
      <form onSubmit={submit}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)', marginBottom: 'var(--space-4)' }}>
          <div className="field">
            <label className="label">{cfg.partyLabel}</label>
            <SearchSelect
              options={parties.map((p) => ({ id: p.id, label: p.name, sublabel: p.code }))}
              value={partyId}
              onChange={setPartyId}
              placeholder={`جست‌وجویِ ${cfg.partyLabel}…`}
            />
          </div>
          <div className="field">
            <label className="label">انبار</label>
            <select className="select" value={warehouseId ?? ''} onChange={(e) => setWarehouseId(Number(e.target.value))}>
              {warehouses.map((w) => (
                <option key={w.id} value={w.id}>{w.name}</option>
              ))}
            </select>
          </div>
          <JalaliDateInput label="تاریخِ مرجوعی" value={date} onChange={setDate} />
          <div className="field" style={{ gridColumn: 'span 2' }}>
            <label className="label">شرح</label>
            <input className="input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="اختیاری" />
          </div>
          <div className="field" style={{ justifyContent: 'end' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: 'var(--text-sm)' }}>
              <input type="checkbox" checked={refundCash} onChange={(e) => setRefundCash(e.target.checked)} />
              {cfg.refundLabel}
            </label>
          </div>
        </div>

        <InvoiceLineEditor products={products} lines={lines} onChange={setLines} priceField={cfg.priceField} hideDiscount />

        {error && (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <StatusMessage kind="error">{error}</StatusMessage>
          </div>
        )}

        <div style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 'var(--space-4)' }}>
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'در حالِ ثبت…' : 'ثبتِ مرجوعی'}
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => navigate(-1)}>انصراف</button>
        </div>
      </form>
    </div>
  );
}
