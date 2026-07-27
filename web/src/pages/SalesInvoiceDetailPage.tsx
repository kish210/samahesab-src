import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface SalesInvoiceDetailItem {
  productId: number;
  code: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  taxPct: number;
  description: string | null;
}

interface SalesInvoiceDetailDto {
  id: number;
  number: string;
  date: string;
  customerId: number;
  customerName: string | null;
  priceLevel: string;
  shipping: number;
  otherCosts: number;
  grandTotal: number;
  paidAmount: number;
  remainAmount: number;
  dueDate: string | null;
  description: string | null;
  reference: string | null;
  title: string | null;
  invoiceType: string;
  items: SalesInvoiceDetailItem[];
  invoiceDiscount: number;
}

function lineTotal(it: SalesInvoiceDetailItem): number {
  const gross = it.quantity * it.unitPrice;
  const net = gross * (1 - it.discountPct / 100);
  return net * (1 + it.taxPct / 100);
}

/** UX-WEB-PRINT — صفحهٔ نمایش/چاپِ یک فاکتورِ فروش (قبلاً اصلاً وجود نداشت؛ فهرست فقط لیست بود). */
export function SalesInvoiceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [inv, setInv] = useState<SalesInvoiceDetailDto | null>(null);
  const [company, setCompany] = useState<Record<string, string | null>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<SalesInvoiceDetailDto>(`/api/sales/invoices/${id}`)
      .then(setInv)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فاکتور.'))
      .finally(() => setLoading(false));
    apiGet<Record<string, string | null>>('/api/settings/company').then(setCompany).catch(() => {});
  }, [id]);

  const columns: Column<SalesInvoiceDetailItem>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'شرح', render: (r) => r.name },
    { key: 'qty', header: 'تعداد', numeric: true, render: (r) => money(r.quantity) },
    { key: 'price', header: 'فی', numeric: true, render: (r) => money(r.unitPrice) },
    { key: 'disc', header: 'تخفیف٪', numeric: true, render: (r) => r.discountPct || '-' },
    { key: 'tax', header: 'مالیات٪', numeric: true, render: (r) => r.taxPct || '-' },
    { key: 'total', header: 'مبلغِ کل', numeric: true, render: (r) => money(lineTotal(r)) },
  ];

  return (
    <div>
      <PageHeader
        title={`فاکتورِ فروش${inv ? ' — ' + inv.number : ''}`}
        actions={
          <>
            <Link to="/sales" className="btn btn-secondary btn-sm">بازگشت به فهرست</Link>
            <button type="button" className="btn btn-primary btn-sm" disabled={!inv} onClick={() => window.print()}>چاپ</button>
          </>
        }
      />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}

      {inv && (
        <div className="print-area">
          <div className="print-only" style={{ marginBottom: 'var(--space-4)', textAlign: 'center' }}>
            {company.CompanyName && <div style={{ fontSize: 18, fontWeight: 700 }}>{company.CompanyName}</div>}
            {company.CompanyAddress && <div style={{ fontSize: 12 }}>{company.CompanyAddress}</div>}
            {(company.CompanyPhone || company.CompanyEconomicCode) && (
              <div style={{ fontSize: 12 }}>
                {company.CompanyPhone && <>تلفن: {company.CompanyPhone} </>}
                {company.CompanyEconomicCode && <>— کدِ اقتصادی: {company.CompanyEconomicCode}</>}
              </div>
            )}
            <h2 style={{ marginTop: 'var(--space-2)' }}>{inv.invoiceType}</h2>
          </div>

          <div className="gbox" style={{ marginBottom: 'var(--space-3)' }}>
            <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              <div><b>شماره:</b> {inv.number}</div>
              <div><b>تاریخ:</b> {inv.date}</div>
              <div><b>مشتری:</b> {inv.customerName ?? `#${inv.customerId}`}</div>
              {inv.reference && <div><b>مرجع:</b> {inv.reference}</div>}
              {inv.dueDate && <div><b>سررسید:</b> {inv.dueDate}</div>}
              <div><b>سطحِ قیمت:</b> {inv.priceLevel}</div>
            </div>
            {inv.description && <div className="gb" style={{ borderTop: '1px solid var(--border)' }}>{inv.description}</div>}
          </div>

          <DataTable columns={columns} rows={inv.items} rowKey={(r) => r.productId} emptyText="قلمی ثبت نشده." />

          <div className="sumbar" style={{ marginTop: 'var(--space-3)' }}>
            <div>تخفیفِ فاکتور: {money(inv.invoiceDiscount)}</div>
            <div>حمل: {money(inv.shipping)}</div>
            <div>سایرِ هزینه‌ها: {money(inv.otherCosts)}</div>
            <div style={{ fontWeight: 700 }}>مبلغِ کل: {money(inv.grandTotal)}</div>
            <div>پرداختی: {money(inv.paidAmount)}</div>
            <div style={{ fontWeight: 700, color: inv.remainAmount > 0 ? 'var(--danger-700)' : 'inherit' }}>مانده: {money(inv.remainAmount)}</div>
          </div>
        </div>
      )}
    </div>
  );
}
