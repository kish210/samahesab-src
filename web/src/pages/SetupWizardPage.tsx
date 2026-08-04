import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';
import { generateRecoveryCode } from '../lib/recoveryCode';
import { StatusMessage } from '../components/PageHeader';

interface ModuleRow { key: string; displayName: string; enabled: boolean }

const STEP_TITLES = ['اطلاعاتِ شرکت', 'سالِ مالی', 'ماژول‌هایِ اختیاری', 'انبارِ پیش‌فرض', 'رمز و کدِ بازیابی'];
const STEP_COUNT = STEP_TITLES.length;

/** یک‌سالِ بعدِ تاریخِ شمسیِ ورودی — پیش‌فرضِ سادهٔ پایانِ سالِ مالی (کاربر می‌تواند ویرایش کند). */
function plusOneYear(jalali: string): string {
  const [y, m, d] = jalali.split('/');
  return `${Number(y) + 1}/${m}/${d}`;
}

/**
 * ویزاردِ راه‌اندازیِ اولیهٔ وب (U-WEB-WIZARD) — معادلِ FirstRunWizardِ دسکتاپ که در وب اصلاً
 * وجود نداشت. برخلافِ دسکتاپ (تک‌کاربره، AppSettingsStoreِ محلی)، اینجا فقط یک بار در سطحِ
 * شرکت لازم است (چند کاربر/چند مرورگر ممکن است هم‌زمان به همین سرور وصل شوند) — به همین دلیل
 * در Shell به‌صورتِ بنرِ قابلِ‌ردکردن پیشنهاد می‌شود، نه ریدایرکتِ اجباری.
 */
export function SetupWizardPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  // step 0 — company
  const [company, setCompany] = useState({
    CompanyName: '', CompanyNationalId: '', CompanyEconomicCode: '', CompanyPhone: '', CompanyAddress: '',
  });

  // step 1 — fiscal year
  const [fyTitle, setFyTitle] = useState(`سالِ مالیِ ${todayJalaliString().split('/')[0]}`);
  const [fyStart, setFyStart] = useState(`${todayJalaliString().split('/')[0]}/01/01`);
  const [fyEnd, setFyEnd] = useState(plusOneYear(`${todayJalaliString().split('/')[0]}/01/01`));

  // step 2 — modules
  const [modules, setModules] = useState<ModuleRow[]>([]);
  const [modulesLoading, setModulesLoading] = useState(true);

  // step 3 — warehouse
  const [warehouseName, setWarehouseName] = useState('انبارِ مرکزی');

  // step 4 — password + recovery
  const [newPassword, setNewPassword] = useState('');
  const [newPassword2, setNewPassword2] = useState('');
  const [recoveryCode, setRecoveryCode] = useState<string | null>(null);
  const [finished, setFinished] = useState(false);

  useEffect(() => {
    if (step !== 2) return;
    setModulesLoading(true);
    apiGet<ModuleRow[]>('/api/modules').then(setModules).catch(() => setModules([])).finally(() => setModulesLoading(false));
  }, [step]);

  async function toggleModule(key: string, enabled: boolean) {
    setModules((prev) => prev.map((m) => (m.key === key ? { ...m, enabled } : m)));
    try {
      await apiPost(`/api/modules/${encodeURIComponent(key)}/toggle`, { enabled });
      window.dispatchEvent(new CustomEvent('sh:modules-changed'));
    } catch {
      setModules((prev) => prev.map((m) => (m.key === key ? { ...m, enabled: !enabled } : m)));
    }
  }

  async function goNext() {
    setErr(null);
    if (step === 0) {
      setBusy(true);
      try {
        const body: Record<string, string | null> = {};
        for (const [k, v] of Object.entries(company)) body[k] = v.trim() || null;
        await apiPut('/api/settings/company', body);
        setStep(1);
      } catch (e) {
        setErr(e instanceof ApiError ? e.message : 'ذخیرهٔ اطلاعاتِ شرکت ناموفق بود.');
      } finally {
        setBusy(false);
      }
      return;
    }
    if (step === 1) {
      if (!fyTitle.trim()) { setErr('عنوانِ سالِ مالی الزامی است.'); return; }
      if (!isValidJalali(fyStart) || !isValidJalali(fyEnd)) { setErr('تاریخِ شروع/پایانِ سالِ مالی نامعتبر است.'); return; }
      setBusy(true);
      try {
        await apiPost('/api/accounting/dimensions/fiscal-years', { id: 0, title: fyTitle.trim(), startDate: fyStart, endDate: fyEnd });
        setStep(2);
      } catch (e) {
        setErr(e instanceof ApiError ? e.message : 'ثبتِ سالِ مالی ناموفق بود.');
      } finally {
        setBusy(false);
      }
      return;
    }
    if (step === 2) { setStep(3); return; }
    if (step === 3) {
      if (warehouseName.trim()) {
        setBusy(true);
        try {
          await apiPost('/api/warehouse', { name: warehouseName.trim() });
        } catch (e) {
          // انبارِ هم‌نام ممکن است از قبل باشد — مانعِ ادامهٔ ویزارد نشود.
          setErr(e instanceof ApiError ? e.message : null);
        } finally {
          setBusy(false);
        }
      }
      setStep(4);
      return;
    }
  }

  function goBack() {
    setErr(null);
    setStep((s) => Math.max(0, s - 1));
  }

  async function changePassword() {
    setErr(null);
    if (newPassword.length < 8 || !/[A-Za-z]/.test(newPassword) || !/\d/.test(newPassword)) {
      setErr('رمزِ عبور باید حداقل ۸ کاراکتر و شاملِ حرف و عدد باشد.');
      return;
    }
    if (newPassword !== newPassword2) { setErr('تکرارِ رمزِ عبور با رمزِ واردشده یکسان نیست.'); return; }
    setBusy(true);
    try {
      await apiPost('/api/auth/change-password', { newPassword });
      const code = generateRecoveryCode();
      await apiPost('/api/auth/recovery-code', { recoveryCode: code.replace(/-/g, '') });
      setRecoveryCode(code);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'تغییرِ رمز ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function finish() {
    setBusy(true);
    try {
      await apiPut('/api/settings/company', { SetupCompleted: 'true' });
      setFinished(true);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'اتمامِ راه‌اندازی ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function skipSetup() {
    setBusy(true);
    try {
      await apiPut('/api/settings/company', { SetupCompleted: 'true' });
      navigate('/');
    } catch {
      navigate('/');
    } finally {
      setBusy(false);
    }
  }

  if (finished) {
    return (
      <div style={{ maxWidth: 520, margin: '48px auto', textAlign: 'center' }}>
        <div style={{ fontSize: 40 }}>✅</div>
        <h2 style={{ marginTop: 'var(--space-3)' }}>راه‌اندازی تمام شد</h2>
        <p style={{ color: 'var(--text-muted)' }}>سما حساب آمادهٔ استفاده است.</p>
        <button className="btn btn-primary" onClick={() => navigate('/')}>رفتن به داشبورد</button>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 640, margin: '32px auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 'var(--space-4)' }}>
        {STEP_TITLES.map((t, i) => (
          <div key={t} style={{
            flex: 1, textAlign: 'center', fontSize: 'var(--text-xs)', padding: '6px 4px',
            color: i === step ? 'var(--text-strong)' : 'var(--text-muted)',
            borderBottom: `2px solid ${i <= step ? 'var(--blue-600)' : 'var(--border)'}`,
            fontWeight: i === step ? 700 : 400,
          }}>
            {t}
          </div>
        ))}
      </div>

      <div className="gbox">
        <div className="gh">راه‌اندازیِ اولیه — گامِ {step + 1} از {STEP_COUNT}: {STEP_TITLES[step]}</div>
        <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>

          {step === 0 && (
            <>
              <div className="field">
                <label className="label">نامِ شرکت</label>
                <input className="input" value={company.CompanyName}
                  onChange={(e) => setCompany((p) => ({ ...p, CompanyName: e.target.value }))} />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(200px, 1fr))', gap: 'var(--space-3)' }}>
                <div className="field">
                  <label className="label">شناسهٔ ملی</label>
                  <input className="input" value={company.CompanyNationalId}
                    onChange={(e) => setCompany((p) => ({ ...p, CompanyNationalId: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label">کدِ اقتصادی</label>
                  <input className="input" value={company.CompanyEconomicCode}
                    onChange={(e) => setCompany((p) => ({ ...p, CompanyEconomicCode: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label">تلفن</label>
                  <input className="input" value={company.CompanyPhone}
                    onChange={(e) => setCompany((p) => ({ ...p, CompanyPhone: e.target.value }))} />
                </div>
              </div>
              <div className="field">
                <label className="label">آدرس</label>
                <input className="input" value={company.CompanyAddress}
                  onChange={(e) => setCompany((p) => ({ ...p, CompanyAddress: e.target.value }))} />
              </div>
            </>
          )}

          {step === 1 && (
            <>
              <div className="field">
                <label className="label">عنوانِ سالِ مالی</label>
                <input className="input" value={fyTitle} onChange={(e) => setFyTitle(e.target.value)} />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(200px, 1fr))', gap: 'var(--space-3)' }}>
                <JalaliDateInput label="شروع" value={fyStart} onChange={setFyStart} />
                <JalaliDateInput label="پایان" value={fyEnd} onChange={setFyEnd} />
              </div>
            </>
          )}

          {step === 2 && (
            <>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
                ماژول‌هایِ اختیاری که می‌خواهید فعال باشند را انتخاب کنید — بعداً هم از «مدیریتِ ماژول‌ها» قابلِ تغییر است.
              </div>
              {modulesLoading ? (
                <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>
              ) : modules.length === 0 ? (
                <StatusMessage kind="muted">ماژولِ اختیاریِ نصب‌شده‌ای یافت نشد.</StatusMessage>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {modules.map((m) => (
                    <label key={m.key} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--text-sm)' }}>
                      <input type="checkbox" checked={m.enabled} onChange={(e) => toggleModule(m.key, e.target.checked)} />
                      {m.displayName}
                    </label>
                  ))}
                </div>
              )}
            </>
          )}

          {step === 3 && (
            <>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
                نامِ اولین انبار را وارد کنید (اختیاری — خالی بگذارید تا این گام رد شود).
              </div>
              <div className="field">
                <label className="label">نامِ انبار</label>
                <input className="input" value={warehouseName} onChange={(e) => setWarehouseName(e.target.value)} />
              </div>
            </>
          )}

          {step === 4 && (
            <>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
                برایِ اتمامِ راه‌اندازی، یک رمزِ عبورِ نو برایِ کاربرِ جاری تعیین کنید — سپس کدِ بازیابی ساخته می‌شود.
              </div>
              {!recoveryCode ? (
                <>
                  <div className="field">
                    <label className="label">رمزِ عبورِ نو</label>
                    <input className="input" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
                  </div>
                  <div className="field">
                    <label className="label">تکرارِ رمزِ عبور</label>
                    <input className="input" type="password" value={newPassword2} onChange={(e) => setNewPassword2(e.target.value)} />
                  </div>
                  <div><button className="btn btn-primary btn-sm" disabled={busy} onClick={changePassword}>
                    {busy ? 'در حالِ ذخیره…' : 'تغییرِ رمز و ساختِ کدِ بازیابی'}
                  </button></div>
                </>
              ) : (
                <>
                  <div className="num" style={{
                    direction: 'ltr', fontSize: 'var(--text-lg)', fontWeight: 700, letterSpacing: 2,
                    background: 'var(--bg-sunken)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
                    padding: '10px 14px', textAlign: 'center',
                  }}>
                    {recoveryCode}
                  </div>
                  <StatusMessage kind="success">همین حالا این کد را جایی امن یادداشت کنید — دیگر نشان داده نمی‌شود.</StatusMessage>
                  <div><button className="btn btn-primary" disabled={busy} onClick={finish}>
                    {busy ? 'در حالِ اتمام…' : 'اتمامِ راه‌اندازی'}
                  </button></div>
                </>
              )}
            </>
          )}

          {err && <StatusMessage kind="error">{err}</StatusMessage>}

          {step < 4 && (
            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-2)' }}>
              <button className="btn btn-ghost btn-sm" onClick={skipSetup} disabled={busy}>ردِ راه‌اندازی</button>
              <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                {step > 0 && <button className="btn btn-secondary btn-sm" onClick={goBack} disabled={busy}>قبلی</button>}
                <button className="btn btn-primary btn-sm" onClick={goNext} disabled={busy}>
                  {busy ? 'در حالِ ذخیره…' : 'بعدی'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
