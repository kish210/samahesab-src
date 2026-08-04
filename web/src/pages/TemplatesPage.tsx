import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface TemplateRow { id: number; name: string; paperSize: string; isDefault: boolean; isActive: boolean; isSystem: boolean }
interface TemplateFull { id: number; documentType: string; name: string; paperSize: string; headerHtml: string | null; bodyHtml: string; footerHtml: string | null }

const DOCUMENT_TYPES: { id: string; name: string }[] = [
  { id: 'SalesInvoice', name: 'فاکتور فروش' }, { id: 'PurchaseInvoice', name: 'فاکتور خرید' },
  { id: 'SalesReturn', name: 'برگشت از فروش' }, { id: 'PurchaseReturn', name: 'برگشت از خرید' },
  { id: 'Quotation', name: 'پیش‌فاکتور' }, { id: 'Proforma', name: 'پروفرما' },
  { id: 'Receipt', name: 'رسید دریافت' }, { id: 'Payment', name: 'رسید پرداخت' },
  { id: 'WarehouseReceipt', name: 'رسید انبار' }, { id: 'WarehouseIssue', name: 'حوالهٔ انبار' },
  { id: 'InventoryTransfer', name: 'انتقالِ انبار' }, { id: 'ChequeReceipt', name: 'رسید چک' },
  { id: 'ChequePayment', name: 'پرداخت چک' }, { id: 'Contract', name: 'قرارداد' },
  { id: 'TourismVoucher', name: 'واچر گردشگری' }, { id: 'HotelVoucher', name: 'واچر هتل' },
  { id: 'RestaurantReceipt', name: 'رسید رستوران' }, { id: 'PosReceipt', name: 'رسید صندوق' },
];
const PAPER_SIZES = ['A4P', 'A4L', 'A5', 'Thermal80', 'Thermal58', 'Custom'];

const BLANK_BODY = `<div style="font-family:Tahoma;direction:rtl;padding:16px">
  <h2 style="text-align:center">{InvoiceNumber}</h2>
  <p>مشتری: {CustomerName} — تاریخ: {InvoiceDate}</p>
  [[ROWS]]<div>{#}. {ProductName} × {Quantity} = {LineTotal}</div>[[/ROWS]]
  <h3>جمع: {TotalAmount}</h3>
</div>`;

/** «قالب‌هایِ چاپ» — پورتِ `DocumentTemplatesViewModel`ِ دسکتاپ به وب. پیش‌تر وب هیچ راهی
 * برایِ مدیریت/سفارشی‌سازیِ سربرگ/بدنه/فوترِ قالب‌هایِ چاپ نداشت (فقط دکمهٔ «نصبِ قالب‌هایِ
 * پیش‌فرض» از پکِ ازقبل‌موجودِ ۳۹ قالبِ نمونه در `Templates/`). موتورِ توکن (`{Field}`،
 * `[[ROWS]]…[[/ROWS]]`) سمتِ سرور رندر می‌شود (`DocumentTemplateEngine`) — همان منطقِ دسکتاپ. */
export function TemplatesPage() {
  const [docType, setDocType] = useState('SalesInvoice');
  const [templates, setTemplates] = useState<TemplateRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [installing, setInstalling] = useState(false);
  const [saving, setSaving] = useState(false);

  const [editId, setEditId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [paperSize, setPaperSize] = useState('A4P');
  const [header, setHeader] = useState('');
  const [body, setBody] = useState(BLANK_BODY);
  const [footer, setFooter] = useState('');

  async function load() {
    setLoading(true);
    try {
      setTemplates(await apiGet<TemplateRow[]>(`/api/document-templates?documentType=${encodeURIComponent(docType)}`));
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ قالب‌ها.' });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [docType]);

  function newTemplate() {
    setEditId(null); setName(''); setPaperSize('A4P'); setHeader(''); setBody(BLANK_BODY); setFooter('');
  }

  async function loadIntoEditor(id: number) {
    try {
      const t = await apiGet<TemplateFull>(`/api/document-templates/${id}`);
      setEditId(t.id); setName(t.name); setPaperSize(t.paperSize);
      setHeader(t.headerHtml ?? ''); setBody(t.bodyHtml); setFooter(t.footerHtml ?? '');
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'بارگیریِ قالب ناموفق بود.' });
    }
  }

  async function save() {
    if (!name.trim()) { setMsg({ kind: 'error', text: 'نامِ قالب الزامی است.' }); return; }
    if (!body.trim()) { setMsg({ kind: 'error', text: 'بدنهٔ قالب الزامی است.' }); return; }
    setSaving(true); setMsg(null);
    try {
      await apiPost('/api/document-templates', {
        id: editId, documentType: docType, name: name.trim(), paperSize,
        headerHtml: header || null, bodyHtml: body, footerHtml: footer || null,
      });
      setMsg({ kind: 'success', text: `قالبِ «${name}» ذخیره شد.` });
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.' });
    } finally {
      setSaving(false);
    }
  }

  async function setDefault(id: number) {
    setMsg(null);
    try {
      await apiPost(`/api/document-templates/${id}/set-default`);
      setMsg({ kind: 'success', text: 'قالبِ پیش‌فرضِ این نوعِ سند تعیین شد.' });
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'تعیینِ پیش‌فرض ناموفق بود.' });
    }
  }

  async function remove(id: number) {
    if (!confirm('این قالب حذف شود؟')) return;
    setMsg(null);
    try {
      await apiDelete(`/api/document-templates/${id}`);
      if (editId === id) newTemplate();
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'حذف ناموفق بود.' });
    }
  }

  async function installBuiltIn() {
    setInstalling(true); setMsg(null);
    try {
      const res = await apiPost<{ imported: number; skipped: number; failed: number }>('/api/document-templates/install-builtin');
      setMsg({
        kind: res.failed > 0 && res.imported === 0 ? 'error' : 'success',
        text: `نصبِ قالب‌هایِ پیش‌فرض پایان یافت — نصب‌شده: ${res.imported}، از قبل موجود: ${res.skipped}` +
          (res.failed > 0 ? `، ناموفق: ${res.failed}` : ''),
      });
      await load();
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'نصبِ قالب‌هایِ پیش‌فرض ناموفق بود.' });
    } finally {
      setInstalling(false);
    }
  }

  async function preview() {
    if (!body.trim()) { setMsg({ kind: 'error', text: 'بدنهٔ قالب خالی است.' }); return; }
    try {
      const res = await apiPost<{ html: string }>('/api/document-templates/preview', {
        headerHtml: header || null, bodyHtml: body, footerHtml: footer || null,
      });
      const w = window.open('', '_blank');
      if (w) { w.document.write(res.html); w.document.title = `پیش‌نمایش — ${name || 'قالب'}`; w.document.close(); }
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'پیش‌نمایش ناموفق بود.' });
    }
  }

  return (
    <div>
      <PageHeader
        title="قالب‌هایِ چاپ"
        actions={
          <button className="btn btn-secondary btn-sm" disabled={installing} onClick={installBuiltIn}>
            {installing ? 'در حالِ نصب…' : 'نصبِ قالب‌هایِ پیش‌فرض (نمونه)'}
          </button>
        }
      />
      <p style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)', marginBottom: 'var(--space-4)' }}>
        سربرگ/بدنه/فوترِ چاپِ هر نوعِ سند را سفارشی کنید. توکن‌ها: <code>{'{InvoiceNumber}'}</code>،
        <code> {'{CustomerName}'}</code>، <code>{'{TotalAmount}'}</code> و… ؛ ردیف‌هایِ اقلام بینِ
        <code> [[ROWS]]…[[/ROWS]]</code> با <code>{'{#}'}</code> برایِ شمارهٔ ردیف.
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 'var(--space-4)', alignItems: 'start' }}>
        <div className="gbox">
          <div className="gh">نوعِ سند</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {DOCUMENT_TYPES.map((d) => (
              <button key={d.id} type="button"
                className={`btn btn-sm ${d.id === docType ? 'btn-primary' : 'btn-ghost'}`}
                style={{ justifyContent: 'flex-start' }}
                onClick={() => { setDocType(d.id); newTemplate(); }}>
                {d.name}
              </button>
            ))}
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
          <div className="gbox">
            <div className="gh">قالب‌هایِ «{DOCUMENT_TYPES.find((d) => d.id === docType)?.name}»</div>
            <div className="gb">
              {loading ? (
                <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>
              ) : templates.length === 0 ? (
                <StatusMessage kind="muted">قالبی برایِ این نوعِ سند نیست — «نصبِ قالب‌هایِ پیش‌فرض» یا «قالبِ نو» را بزنید.</StatusMessage>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  {templates.map((t) => (
                    <div key={t.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 4px', borderBottom: '1px solid var(--gray-100)' }}>
                      <button type="button" className="btn btn-ghost btn-sm" style={{ flex: 1, justifyContent: 'flex-start' }}
                        onClick={() => loadIntoEditor(t.id)}>
                        {t.name}
                      </button>
                      {t.isDefault && <span className="badge badge-green">پیش‌فرض</span>}
                      {t.isSystem && <span className="badge badge-gray">سیستمی</span>}
                      <span style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)', direction: 'ltr' }}>{t.paperSize}</span>
                      {!t.isDefault && <button className="btn btn-ghost btn-sm" onClick={() => setDefault(t.id)}>پیش‌فرض کن</button>}
                    </div>
                  ))}
                </div>
              )}
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button className="btn btn-secondary btn-sm" onClick={newTemplate}>+ قالبِ نو</button>
              </div>
            </div>
          </div>

          <div className="gbox">
            <div className="gh">{editId ? 'ویرایشِ قالب' : 'قالبِ نو'}</div>
            <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 160px', gap: 'var(--space-3)' }}>
                <div className="field">
                  <label className="label">نامِ قالب</label>
                  <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">اندازهٔ کاغذ</label>
                  <select className="select" value={paperSize} onChange={(e) => setPaperSize(e.target.value)}>
                    {PAPER_SIZES.map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </div>
              </div>
              <div className="field">
                <label className="label">سربرگ (HTML، اختیاری)</label>
                <textarea className="input" style={{ direction: 'ltr', fontFamily: 'monospace', minHeight: 60 }}
                  value={header} onChange={(e) => setHeader(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">بدنه (HTML)</label>
                <textarea className="input" style={{ direction: 'ltr', fontFamily: 'monospace', minHeight: 180 }}
                  value={body} onChange={(e) => setBody(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">فوتر (HTML، اختیاری)</label>
                <textarea className="input" style={{ direction: 'ltr', fontFamily: 'monospace', minHeight: 60 }}
                  value={footer} onChange={(e) => setFooter(e.target.value)} />
              </div>
              <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                <button className="btn btn-primary btn-sm" disabled={saving} onClick={save}>
                  {saving ? 'در حالِ ذخیره…' : 'ذخیره'}
                </button>
                <button className="btn btn-secondary btn-sm" onClick={preview}>پیش‌نمایش با دادهٔ نمونه</button>
                {editId && <button className="btn btn-ghost btn-sm" onClick={() => remove(editId)}>حذف</button>}
              </div>
              {msg && <StatusMessage kind={msg.kind}>{msg.text}</StatusMessage>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
