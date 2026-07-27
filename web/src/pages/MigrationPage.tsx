import { useState } from 'react';
import { apiUpload, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ImportResult {
  imported: number;
  skipped: number;
  failed: number;
  errors: string[];
}

type EntityType = 'persons' | 'customers' | 'suppliers' | 'products';

const ENTITY_LABEL: Record<EntityType, string> = {
  persons: 'اشخاص (مشتری+تأمین‌کننده)',
  customers: 'مشتریان',
  suppliers: 'تأمین‌کنندگان',
  products: 'کالاها',
};

const COLUMN_HELP: Record<EntityType, string> = {
  persons: 'فایلِ ترکیبیِ مشتری+تأمین‌کننده (مثلِ خروجیِ «اشخاص»ِ حساب‌فا). بر اساسِ ستونِ پرچمِ «مشتری»/«تأمین‌کننده» (مقدارِ «+») تفکیک می‌شود؛ ردیفِ بدونِ پرچم → مشتری. ستون‌ها: کد · نام · نام خانوادگی · نام شرکت · تلفن · موبایل · ایمیل · استان · شهر · آدرس · کد پستی · کد ملی · کد اقتصادی.',
  customers: 'ستون‌ها: کد · نام · نام خانوادگی · نام شرکت · تلفن · موبایل · ایمیل · استان · شهر · آدرس · کد پستی · کد ملی · کد اقتصادی · توضیحات.',
  suppliers: 'ستون‌ها: کد · نام · نام خانوادگی · نام شرکت · تلفن · موبایل · ایمیل · استان · شهر · آدرس.',
  products: 'ستون‌ها: کد · نام · واحد · قیمت فروش · قیمت خرید · قیمت عمده · قیمت مصرف‌کننده · مالیات · بارکد (واحدِ ناشناخته → «عدد»).',
};

const SOURCE_HINT: Record<string, string> = {
  'حساب‌فا': 'فایلِ «اشخاص» را با نوعِ «اشخاص» وارد کنید (خودکار به مشتری/تأمین‌کننده تفکیک می‌شود) و فایلِ «کالاها» را با نوعِ «کالاها». فاکتور/سند/دریافت/پرداختِ حساب‌فا سرجمع‌اند و مستقیم وارد نمی‌شوند — مانده‌های افتتاحیه را به‌صورتِ سندِ افتتاحیه ثبت کنید.',
  'سپیدار': 'اشخاص/کالا را با همان نوع وارد کنید. اگر سرستون‌ها فرق داشت، عنوان‌های ستونِ فایل را مطابقِ ستون‌های بالا تنظیم کنید.',
  'هلو': 'اشخاص/کالا را با همان نوع وارد کنید؛ در صورتِ نیاز سرستون‌ها را مطابقِ ستون‌های بالا تنظیم کنید.',
  'اکسلِ استاندارد': 'یک فایلِ .xlsx با همان سرستون‌های بالا بسازید، پر کنید و وارد کنید. سطرِ اول باید سرستون باشد.',
};

/**
 * U-WEB-IMPORT — «مهاجرت از سایرِ برنامه‌ها» رویِ وب (پورتِ DataImportViewModelِ دسکتاپ).
 * فایلِ .xlsx آپلود می‌شود، سرور با همان زیرساختِ دسکتاپ (ClosedXML + Import*Command) آن را
 * می‌خواند و درج می‌کند (idempotent — کدهای تکراری رد می‌شوند).
 */
export function MigrationPage() {
  const [source, setSource] = useState('حساب‌فا');
  const [entity, setEntity] = useState<EntityType>('persons');
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function runImport() {
    if (!file) { setError('ابتدا یک فایلِ اکسل انتخاب کنید.'); return; }
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      const res = await apiUpload<ImportResult>(`/api/import/${entity}`, file);
      setResult(res);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ورودِ داده ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader title="مهاجرت از سایرِ برنامه‌ها" />

      <div className="gbox" style={{ maxWidth: 720 }}>
        <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
            دادهٔ اشخاص و کالا را از نرم‌افزارِ قبلی (به‌صورتِ فایلِ اکسل .xlsx) وارد کنید. کدهای
            تکراری رد می‌شوند، پس می‌توانید یک فایل را چند بار وارد کنید بدونِ ساختِ رکوردِ تکراری.
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(220px, 1fr))', gap: 'var(--space-3)' }}>
            <div className="field">
              <label className="label">نرم‌افزارِ مبدأ</label>
              <select className="select" value={source} onChange={(e) => setSource(e.target.value)}>
                {Object.keys(SOURCE_HINT).map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
            <div className="field">
              <label className="label">نوعِ داده</label>
              <select className="select" value={entity} onChange={(e) => setEntity(e.target.value as EntityType)}>
                {(Object.keys(ENTITY_LABEL) as EntityType[]).map((k) => <option key={k} value={k}>{ENTITY_LABEL[k]}</option>)}
              </select>
            </div>
          </div>

          <div style={{ background: 'var(--bg-sunken)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', padding: 10, fontSize: 12.5 }}>
            <div style={{ marginBottom: 6 }}><b>راهنمای {source}:</b> {SOURCE_HINT[source]}</div>
            <div style={{ color: 'var(--text-muted)' }}>{COLUMN_HELP[entity]}</div>
          </div>

          <div className="field">
            <label className="label">فایلِ اکسل (.xlsx)</label>
            <input type="file" accept=".xlsx" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setResult(null); setError(null); }} />
          </div>

          <div>
            <button className="btn btn-primary btn-sm" disabled={busy || !file} onClick={runImport}>
              {busy ? 'در حالِ ورودِ داده…' : 'ورودِ داده'}
            </button>
          </div>

          {error && <StatusMessage kind="error">{error}</StatusMessage>}

          {result && (
            <div style={{
              background: result.failed > 0 || result.errors.length > 0 ? 'var(--danger-50, #fef2f2)' : 'var(--success-50, #ecfdf5)',
              border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', padding: 12, fontSize: 13,
            }}>
              <div style={{ fontWeight: 700 }}>
                وارد شد: {result.imported} · از قبل موجود: {result.skipped} · ناموفق: {result.failed}
              </div>
              {result.errors.length > 0 && (
                <ul style={{ margin: '8px 0 0', paddingInlineStart: 18 }}>
                  {result.errors.slice(0, 30).map((er, i) => <li key={i}>{er}</li>)}
                  {result.errors.length > 30 && <li>… و {result.errors.length - 30} خطای دیگر</li>}
                </ul>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
