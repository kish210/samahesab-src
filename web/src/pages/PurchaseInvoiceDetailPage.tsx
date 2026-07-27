import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money, numberToPersianWords } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface PurchaseInvoiceDetailItem {
  productId: number;
  code: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  taxPct: number;
  description: string | null;
}

interface PurchaseInvoiceDetailDto {
  id: number;
  number: string;
  date: string;
  supplierId: number;
  supplierName: string | null;
  shipping: number;
  otherCosts: number;
  grandTotal: number;
  paidAmount: number;
  remainAmount: number;
  dueDate: string | null;
  description: string | null;
  invoiceType: string;
  statusCode: string;
  items: PurchaseInvoiceDetailItem[];
}

function lineTotal(it: PurchaseInvoiceDetailItem): number {
  const gross = it.quantity * it.unitPrice;
  const net = gross * (1 - it.discountPct / 100);
  return net * (1 + it.taxPct / 100);
}

/** UX-WEB-PRINT — صفحهٔ نمایش/چاپِ یک فاکتورِ خرید (قرینهٔ SalesInvoiceDetailPage). */
export function PurchaseInvoiceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [inv, setInv] = useState<PurchaseInvoiceDetailDto | null>(null);
  const [company, setCompany] = useState<Record<string, string | null>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<PurchaseInvoiceDetailDto>(`/api/purchase/invoices/${id}`)
      .then(setInv)
      // ۴۰۴ = فاکتور نیست (نه قطعیِ ارتباط) — قرینهٔ SalesInvoiceDetailPage.
      .catch((e) => setError(
        e instanceof ApiError
          ? (e.status === 404 ? 'فاکتوری با این شناسه یافت نشد.' : e.message)
          : 'خطا در بارگیریِ فاکتور.'))
      .finally(() => setLoading(false));
    apiGet<Record<string, string | null>>('/api/settings/company').then(setCompany).catch(() => {});
  }, [id]);

  const columns: Column<PurchaseInvoiceDetailItem>[] = [
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
        title={`فاکتورِ خرید${inv ? ' — ' + inv.number : ''}`}
        actions={
          <>
            <Link to="/purchase" className="btn btn-secondary btn-sm">بازگشت به فهرست</Link>
            <button type="button" className="btn btn-primary btn-sm" disabled={!inv} onClick={() => window.print()}>چاپ</button>
          </>
        }
      />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}

      {inv && (
        <div className="print-area">
          <div className="print-only" style={{ marginBottom: 'var(--space-4)', textAlign: 'center' }}>
            {company.CompanyLogo && (
              <img src={company.CompanyLogo} alt="" style={{ maxWidth: 240, maxHeight: 120, marginBottom: 6 }} />
            )}
            {company.CompanyName && <div style={{ fontSize: 18, fontWeight: 700 }}>{company.CompanyName}</div>}
            {company.CompanyAddress && <div style={{ fontSize: 12 }}>{company.CompanyAddress}</div>}
            {(company.CompanyPhone || company.CompanyEconomicCode) && (
              <div style={{ fontSize: 12 }}>
                {company.CompanyPhone && <>تلفن: {company.CompanyPhone} </>}
                {company.CompanyEconomicCode && <>— کدِ اقتصادی: {company.CompanyEconomicCode}</>}
              </div>
            )}
            <h2 style={{ marginTop: 'var(--space-2)' }}>فاکتورِ خرید</h2>
          </div>

          <div className="gbox" style={{ marginBottom: 'var(--space-3)' }}>
            <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              <div><b>شماره:</b> {inv.number}</div>
              <div><b>تاریخ:</b> {inv.date}</div>
              <div><b>تأمین‌کننده:</b> {inv.supplierName ?? `#${inv.supplierId}`}</div>
              {inv.dueDate && <div><b>سررسید:</b> {inv.dueDate}</div>}
              <div><b>وضعیت:</b> {inv.statusCode}</div>
            </div>
            {inv.description && <div className="gb" style={{ borderTop: '1px solid var(--border)' }}>{inv.description}</div>}
          </div>

          <DataTable columns={columns} rows={inv.items} rowKey={(r) => r.productId} emptyText="قلمی ثبت نشده." />

          <div className="sumbar" style={{ marginTop: 'var(--space-3)' }}>
            <div>حمل: {money(inv.shipping)}</div>
            <div>سایرِ هزینه‌ها: {money(inv.otherCosts)}</div>
            <div style={{ fontWeight: 700 }}>مبلغِ کل: {money(inv.grandTotal)}</div>
            <div>پرداختی: {money(inv.paidAmount)}</div>
            <div style={{ fontWeight: 700, color: inv.remainAmount > 0 ? 'var(--danger-700)' : 'inherit' }}>مانده: {money(inv.remainAmount)}</div>
          </div>

          <div style={{ marginTop: 'var(--space-3)', fontSize: 13 }}>
            <b>به حروف:</b> {numberToPersianWords(inv.grandTotal)} ریال
          </div>

          {/* .print-only مقدارِ display:block!important دارد → flex می‌شکند؛ inline-block می‌ماند. */}
          <div className="print-only" style={{ marginTop: 40, fontSize: 12 }}>
            <span style={{ display: 'inline-block', width: '48%' }}>مهر و امضایِ تأمین‌کننده</span>
            <span style={{ display: 'inline-block', width: '48%', textAlign: 'left' }}>مهر و امضایِ خریدار</span>
          </div>
        </div>
      )}
    </div>
  );
}
