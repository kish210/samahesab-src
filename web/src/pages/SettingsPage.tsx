import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface LicenseStatus { isExpired: boolean; daysRemaining: number | null; expiresUtc: string | null }

/** کدِ بازیابیِ تصادفیِ ۱۶نویسه‌ای — همان الگویِ `RecoveryCodeGenerator`ِ دسکتاپ (حروفِ بزرگ+عدد،
 * بدونِ کاراکترهایِ مبهم مثلِ 0/O یا 1/I). */
function generateRecoveryCode(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let code = '';
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  for (let i = 0; i < 16; i++) {
    code += chars[bytes[i] % chars.length];
    if (i % 4 === 3 && i < 15) code += '-';
  }
  return code;
}

function Kv({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', padding: '7px 0', borderBottom: '1px solid var(--gray-100)' }}>
      <span style={{ color: 'var(--text-muted)' }}>{label}</span>
      <span style={{ fontWeight: 500 }}>{value}</span>
    </div>
  );
}

/** کلیدهایِ `CompanySettingKeys`ِ سرور — همین‌ها در `PUT /api/settings/company` مجازند. */
const COMPANY_FIELDS = [
  { key: 'CompanyName', label: 'نامِ شرکت' },
  { key: 'CompanyNationalId', label: 'شناسهٔ ملی' },
  { key: 'CompanyEconomicCode', label: 'کدِ اقتصادی' },
  { key: 'CompanyPhone', label: 'تلفن' },
  { key: 'CompanyAddress', label: 'آدرس' },
] as const;

/** «تنظیمات → دربارهٔ سیستم» — وب برخلافِ دسکتاپ هیچ صفحهٔ تنظیماتی نداشت. این صفحه اطلاعاتِ
 * پایه (نسخه/مجوز/کاربر) + **اطلاعاتِ شرکت** را نشان/ویرایش می‌کند. اطلاعاتِ شرکت تا پیش از
 * این هیچ UIای نداشت (نه وب، نه دسکتاپ که فقط استابِ «در حالِ توسعه» بود) ⇒ سربرگِ چاپیِ
 * فاکتور همیشه خالی می‌ماند؛ با این فرم پر می‌شود. */
export function SettingsPage() {
  const { user } = useAuth();
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [newCode, setNewCode] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  const [company, setCompany] = useState<Record<string, string>>({});
  const [savingCompany, setSavingCompany] = useState(false);
  const [companyMsg, setCompanyMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  useEffect(() => {
    apiGet<LicenseStatus>('/api/license/status').then(setLicense).catch(() => {});
    apiGet<Record<string, string | null>>('/api/settings/company')
      .then((d) => setCompany(Object.fromEntries(Object.entries(d).map(([k, v]) => [k, v ?? '']))))
      .catch(() => {});
  }, []);

  async function saveCompany() {
    setSavingCompany(true);
    setCompanyMsg(null);
    try {
      // فقط کلیدهایِ شناخته‌شده ارسال می‌شوند — سرور بقیه را رد می‌کند.
      const body = Object.fromEntries(COMPANY_FIELDS.map((f) => [f.key, company[f.key]?.trim() || null]));
      await apiPut('/api/settings/company', body);
      setCompanyMsg({ kind: 'success', text: 'اطلاعاتِ شرکت ذخیره شد — از این پس در سربرگِ چاپیِ فاکتورها می‌آید.' });
    } catch (e) {
      setCompanyMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.' });
    } finally {
      setSavingCompany(false);
    }
  }

  function makeCode() {
    setNewCode(generateRecoveryCode());
    setMsg(null);
  }

  async function saveCode() {
    if (!newCode) return;
    setSaving(true);
    setMsg(null);
    try {
      await apiPost('/api/auth/recovery-code', { recoveryCode: newCode.replace(/-/g, '') });
      setMsg({ kind: 'success', text: 'کدِ بازیابی ذخیره شد — همین حالا آن را جایی امن بنویسید؛ دیگر نشان داده نمی‌شود.' });
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.' });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <PageHeader title="دربارهٔ سیستم" />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(260px, 1fr))', gap: 'var(--space-4)', alignItems: 'start' }}>
        <div className="gbox">
          <div className="gh">برنامه</div>
          <div className="gb">
            <Kv label="نامِ برنامه" value="سما حساب" />
            <Kv label="نسخه" value="۲٫۹" />
          </div>
        </div>

        <div className="gbox">
          <div className="gh">مجوزِ نصب</div>
          <div className="gb">
            {license == null ? (
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>در حالِ بارگیری…</div>
            ) : (
              <>
                <Kv label="وضعیت" value={license.isExpired ? 'دورهٔ رایگان تمام شده' : 'فعال (دورهٔ رایگانِ یک‌ساله)'} />
                <Kv label="روزهایِ باقی‌مانده" value={license.daysRemaining != null ? `${license.daysRemaining} روز` : '—'} />
              </>
            )}
          </div>
        </div>

        <div className="gbox">
          <div className="gh">کاربرِ جاری</div>
          <div className="gb">
            <Kv label="نام" value={user?.fullName ?? '—'} />
            <Kv label="نامِ کاربری" value={user?.username ?? '—'} />
          </div>
        </div>

        <div className="gbox" style={{ gridColumn: '1 / -1' }}>
          <div className="gh">اطلاعاتِ شرکت (سربرگِ فاکتور)</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
              این اطلاعات در سربرگِ چاپیِ فاکتورِ فروش/خرید نمایش داده می‌شود. خالی‌گذاشتنِ هر فیلد یعنی در چاپ نمی‌آید.
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(220px, 1fr))', gap: 'var(--space-3)' }}>
              {COMPANY_FIELDS.map((f) => (
                <div className="field" key={f.key} style={f.key === 'CompanyAddress' ? { gridColumn: '1 / -1' } : undefined}>
                  <label className="label" htmlFor={`cs-${f.key}`}>{f.label}</label>
                  <input
                    id={`cs-${f.key}`}
                    className="input"
                    value={company[f.key] ?? ''}
                    onChange={(e) => setCompany((p) => ({ ...p, [f.key]: e.target.value }))}
                  />
                </div>
              ))}
            </div>
            <div>
              <button className="btn btn-primary btn-sm" disabled={savingCompany} onClick={saveCompany}>
                {savingCompany ? 'در حالِ ذخیره…' : 'ذخیرهٔ اطلاعاتِ شرکت'}
              </button>
            </div>
            {companyMsg && <StatusMessage kind={companyMsg.kind}>{companyMsg.text}</StatusMessage>}
          </div>
        </div>

        <div className="gbox" style={{ gridColumn: '1 / -1' }}>
          <div className="gh">کدِ بازیابیِ رمزِ عبور</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
              اگر رمزِ عبورتان را فراموش کنید، این کد تنها راهِ بازیابیِ آن از صفحهٔ ورود است (این برنامه آفلاین است — ایمیل/پیامکِ بازیابی وجود ندارد). با ساختنِ کدِ نو، کدِ قبلی از کار می‌افتد.
            </div>
            {!newCode ? (
              <div><button className="btn btn-primary btn-sm" onClick={makeCode}>ساختِ کدِ بازیابیِ نو</button></div>
            ) : (
              <>
                <div className="num" style={{
                  direction: 'ltr', fontSize: 'var(--text-lg)', fontWeight: 700, letterSpacing: 2,
                  background: 'var(--bg-sunken)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
                  padding: '10px 14px', textAlign: 'center',
                }}>
                  {newCode}
                </div>
                <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                  <button className="btn btn-primary btn-sm" disabled={saving} onClick={saveCode}>
                    {saving ? 'در حالِ ذخیره…' : 'یادداشت کردم — ذخیره کن'}
                  </button>
                  <button className="btn btn-ghost btn-sm" disabled={saving} onClick={makeCode}>ساختِ کدِ دیگر</button>
                </div>
              </>
            )}
            {msg && <StatusMessage kind={msg.kind}>{msg.text}</StatusMessage>}
          </div>
        </div>
      </div>
    </div>
  );
}
