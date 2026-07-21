import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ModianSettingsDto {
  taxMemoryId: string | null;
  useSandbox: boolean;
  certificatePath: string | null;
  certificatePassword: string | null;
  enabled: boolean;
}
interface SubmissionRow {
  id: number; salesInvoiceId: number; status: string; uniqueTaxId: string | null;
  referenceNumber: string | null; errorMessage: string | null; retryCount: number;
  sentAt: string | null; createdAt: string;
}
interface ItemCodeRow {
  productId: number; productCode: string; productName: string;
  itemId: string | null; measurementUnitCode: string | null;
}
interface RetrySummary { attempted: number; succeeded: number; failed: number }

const STATUS_BADGE: Record<string, string> = {
  'در انتظار': 'badge-amber', 'ارسال‌شده': 'badge-blue', 'پذیرفته‌شده': 'badge-green',
  'ردشده': 'badge-red', 'خطا': 'badge-red',
};

/** ماژولِ اختیاریِ «سامانهٔ مودیان» — لایهٔ Application از پیش آماده بود (فقط دسکتاپ داشت)؛
 * این صفحه اولین UIِ وبِ آن است. طبقِ محدودیتِ صادقانهٔ مستندشده در todo.rm، تا دریافتِ
 * اعتبارنامهٔ Sandbox/واقعی، «سازمان واقعاً می‌پذیرد یا نه» تأییدنشده می‌ماند. */
export function TaxInvoicingPage() {
  const [tab, setTab] = useState<'settings' | 'submissions' | 'itemcodes'>('settings');

  const [settings, setSettings] = useState<ModianSettingsDto | null>(null);
  const [settingsMsg, setSettingsMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [savingSettings, setSavingSettings] = useState(false);

  const [submissions, setSubmissions] = useState<SubmissionRow[]>([]);
  const [submissionsError, setSubmissionsError] = useState<string | null>(null);
  const [retrying, setRetrying] = useState(false);
  const [retryMsg, setRetryMsg] = useState<string | null>(null);

  const [itemCodes, setItemCodes] = useState<ItemCodeRow[]>([]);
  const [itemCodesError, setItemCodesError] = useState<string | null>(null);
  const [savingCode, setSavingCode] = useState<number | null>(null);

  useEffect(() => {
    apiGet<ModianSettingsDto>('/api/tax-invoicing/settings').then(setSettings).catch(() => {});
  }, []);

  useEffect(() => {
    if (tab === 'submissions') {
      apiGet<SubmissionRow[]>('/api/tax-invoicing/submissions')
        .then(setSubmissions)
        .catch((e) => setSubmissionsError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرست.'));
    }
    if (tab === 'itemcodes') {
      apiGet<ItemCodeRow[]>('/api/tax-invoicing/item-codes')
        .then(setItemCodes)
        .catch((e) => setItemCodesError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرست.'));
    }
  }, [tab]);

  async function saveSettings() {
    if (!settings) return;
    setSavingSettings(true);
    setSettingsMsg(null);
    try {
      await apiPost('/api/tax-invoicing/settings', settings);
      setSettingsMsg({ kind: 'success', text: 'تنظیمات ذخیره شد.' });
    } catch (e) {
      setSettingsMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.' });
    } finally {
      setSavingSettings(false);
    }
  }

  async function retryPending() {
    setRetrying(true);
    setRetryMsg(null);
    try {
      const r = await apiPost<RetrySummary>('/api/tax-invoicing/retry-pending');
      setRetryMsg(`تلاش برای ${r.attempted} رکورد: ${r.succeeded} موفق، ${r.failed} ناموفق.`);
      setSubmissions(await apiGet<SubmissionRow[]>('/api/tax-invoicing/submissions'));
    } catch (e) {
      setRetryMsg(e instanceof ApiError ? e.message : 'ارسالِ صف ناموفق بود.');
    } finally {
      setRetrying(false);
    }
  }

  /** ورودی‌هایِ شناسه/کدِ واحد کنترل‌شده‌اند (نه defaultValue) — چون هر دو فیلد با هم اعتبارسنجی
   * می‌شوند (`SaveTaxItemCodeCommand`)، اگر onBlur از رویِ شیِ رَدیفِ قدیمی/uncontrolled بخواند،
   * فیلدِ خواهر همیشه خالی می‌رسد و سرور ۴۰۰ می‌دهد (باگِ واقعیِ کشف‌شده در تستِ زنده). */
  function updateItemCodeField(productId: number, field: 'itemId' | 'measurementUnitCode', value: string) {
    setItemCodes((prev) => prev.map((r) => (r.productId === productId ? { ...r, [field]: value } : r)));
  }

  async function saveItemCode(productId: number) {
    setSavingCode(productId);
    try {
      setItemCodes((prev) => {
        const row = prev.find((r) => r.productId === productId);
        if (row && (row.itemId?.trim() || row.measurementUnitCode?.trim())) {
          apiPost('/api/tax-invoicing/item-codes', {
            productId: row.productId, itemId: row.itemId ?? '', measurementUnitCode: row.measurementUnitCode ?? '',
          })
            .catch(() => {})
            .finally(() => setSavingCode(null));
        } else {
          setSavingCode(null);
        }
        return prev;
      });
    } catch {
      setSavingCode(null);
    }
  }

  const submissionColumns: Column<SubmissionRow>[] = [
    { key: 'invoice', header: 'فاکتور', render: (r) => <span className="num">#{r.salesInvoiceId}</span> },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${STATUS_BADGE[r.status] ?? 'badge-amber'}`}>{r.status}</span> },
    { key: 'uid', header: 'شناسهٔ مالیاتی', render: (r) => <span className="num" style={{ direction: 'ltr' }}>{r.uniqueTaxId ?? '—'}</span> },
    { key: 'ref', header: 'شمارهٔ مرجع', render: (r) => <span className="num" style={{ direction: 'ltr' }}>{r.referenceNumber ?? '—'}</span> },
    { key: 'retry', header: 'تلاش', numeric: true, render: (r) => <span className="num">{r.retryCount}</span> },
    { key: 'error', header: 'خطا', render: (r) => r.errorMessage ?? '—' },
    { key: 'created', header: 'تاریخ', render: (r) => <span className="num mut">{new Date(r.createdAt).toLocaleDateString('fa-IR')}</span> },
  ];

  const itemCodeColumns: Column<ItemCodeRow>[] = [
    { key: 'code', header: 'کد', render: (r) => <span className="num">{r.productCode}</span> },
    { key: 'name', header: 'کالا', render: (r) => r.productName },
    {
      key: 'itemId', header: 'شناسهٔ کالایِ رسمی',
      render: (r) => (
        <input className="input-c" style={{ direction: 'ltr' }} value={r.itemId ?? ''}
          onChange={(e) => updateItemCodeField(r.productId, 'itemId', e.target.value)}
          onBlur={() => saveItemCode(r.productId)} />
      ),
    },
    {
      key: 'unit', header: 'کدِ واحد',
      render: (r) => (
        <input className="input-c" style={{ direction: 'ltr', width: 90 }} value={r.measurementUnitCode ?? ''}
          onChange={(e) => updateItemCodeField(r.productId, 'measurementUnitCode', e.target.value)}
          onBlur={() => saveItemCode(r.productId)} />
      ),
    },
    { key: 'status', header: '', render: (r) => (savingCode === r.productId ? <span className="mut">در حالِ ذخیره…</span> : null) },
  ];

  return (
    <div>
      <PageHeader title="سامانهٔ مودیان" />

      <div className="minitabs">
        <button type="button" className={tab === 'settings' ? 'on' : ''} onClick={() => setTab('settings')}>تنظیمات</button>
        <button type="button" className={tab === 'submissions' ? 'on' : ''} onClick={() => setTab('submissions')}>صورتحساب‌هایِ ارسالی</button>
        <button type="button" className={tab === 'itemcodes' ? 'on' : ''} onClick={() => setTab('itemcodes')}>کدهایِ کالایی</button>
      </div>

      {tab === 'settings' && (
        <div className="gbox" style={{ marginTop: 'var(--space-4)', maxWidth: 460 }}>
          <div className="gh">تنظیماتِ اتصال</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            {settings == null ? (
              <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>
            ) : (
              <>
                <div className="frow">
                  <label>فعال</label>
                  <input type="checkbox" checked={settings.enabled}
                    onChange={(e) => setSettings({ ...settings, enabled: e.target.checked })} />
                </div>
                <div className="frow">
                  <label>محیطِ آزمایشی (Sandbox)</label>
                  <input type="checkbox" checked={settings.useSandbox}
                    onChange={(e) => setSettings({ ...settings, useSandbox: e.target.checked })} />
                </div>
                <div className="frow">
                  <label>شناسهٔ حافظهٔ مالیاتی</label>
                  <input className="input-c" style={{ direction: 'ltr' }} value={settings.taxMemoryId ?? ''}
                    onChange={(e) => setSettings({ ...settings, taxMemoryId: e.target.value })} />
                </div>
                <div className="frow">
                  <label>مسیرِ فایلِ گواهی</label>
                  <input className="input-c" style={{ direction: 'ltr' }} value={settings.certificatePath ?? ''}
                    onChange={(e) => setSettings({ ...settings, certificatePath: e.target.value })} />
                </div>
                <div className="frow">
                  <label>رمزِ گواهی</label>
                  <input className="input-c" type="password" style={{ direction: 'ltr' }} value={settings.certificatePassword ?? ''}
                    onChange={(e) => setSettings({ ...settings, certificatePassword: e.target.value })} />
                </div>
                <div>
                  <button className="btn btn-primary btn-sm" disabled={savingSettings} onClick={saveSettings}>
                    {savingSettings ? 'در حالِ ذخیره…' : 'ذخیره'}
                  </button>
                </div>
                {settingsMsg && <StatusMessage kind={settingsMsg.kind}>{settingsMsg.text}</StatusMessage>}
              </>
            )}
          </div>
        </div>
      )}

      {tab === 'submissions' && (
        <div style={{ marginTop: 'var(--space-4)' }}>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 'var(--space-3)' }}>
            <button className="btn btn-ghost btn-sm" disabled={retrying} onClick={retryPending}>
              {retrying ? 'در حالِ ارسال…' : 'ارسالِ صفِ در‌انتظار/خطا'}
            </button>
          </div>
          {retryMsg && <StatusMessage kind="muted">{retryMsg}</StatusMessage>}
          {submissionsError && <StatusMessage kind="error">{submissionsError}</StatusMessage>}
          {!submissionsError && (
            <DataTable columns={submissionColumns} rows={submissions} rowKey={(r) => r.id}
              emptyText="هیچ صورتحسابِ الکترونیکی‌ای ثبت نشده است." />
          )}
        </div>
      )}

      {tab === 'itemcodes' && (
        <div style={{ marginTop: 'var(--space-4)' }}>
          {itemCodesError && <StatusMessage kind="error">{itemCodesError}</StatusMessage>}
          {!itemCodesError && (
            <DataTable columns={itemCodeColumns} rows={itemCodes} rowKey={(r) => r.productId}
              emptyText="کالایی یافت نشد." />
          )}
        </div>
      )}
    </div>
  );
}
