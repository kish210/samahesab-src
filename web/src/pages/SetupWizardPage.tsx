import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';
import { generateRecoveryCode } from '../lib/recoveryCode';
import { StatusMessage } from '../components/PageHeader';

interface ModuleRow { key: string; displayName: string; enabled: boolean }
interface WizWarehouse { name: string }
interface WizProduct { name: string; isService: boolean; salePrice: string; purchasePrice: string }
interface WizCustomer { name: string; mobile: string; isCompany: boolean }
interface PresetProduct { name: string; isService: boolean; salePrice: number }

const STEP_TITLES = ['اطلاعاتِ شرکت و صنف', 'سالِ مالی', 'ماژول‌هایِ اختیاری', 'دادهٔ پایه', 'رمز و کدِ بازیابی'];
const STEP_COUNT = STEP_TITLES.length;

/** صنف‌های پرکاربرد — عیناً از FirstRunWizardِ دسکتاپ (برای پیش‌پُرِ کالاهای نمونه). */
const BUSINESS_TYPES = [
  'فروشگاه / سوپرمارکت', 'رستوران / کافه / فست‌فود', 'پوشاک و البسه', 'خدمات / مشاوره',
  'پخش و بازرگانی', 'تولیدی / کارگاه', 'آرایشی و بهداشتی', 'طلا و جواهر', 'داروخانه',
  'لوازم خانگی / دیجیتال', 'نمایشگاه خودرو', 'سایر',
];

/** کالاهای نمونهٔ متناسب با هر صنف — کاربر می‌تواند ویرایش/حذف کند. */
const BUSINESS_PRESETS: Record<string, PresetProduct[]> = {
  'رستوران / کافه / فست‌فود': [
    { name: 'چلوکباب کوبیده', isService: false, salePrice: 1850000 },
    { name: 'جوجه‌کباب', isService: false, salePrice: 1650000 },
    { name: 'نوشابه', isService: false, salePrice: 250000 },
    { name: 'چای', isService: false, salePrice: 150000 },
    { name: 'قهوه', isService: false, salePrice: 450000 },
    { name: 'سالاد فصل', isService: false, salePrice: 350000 },
  ],
  'فروشگاه / سوپرمارکت': [
    { name: 'برنج ایرانی (کیلو)', isService: false, salePrice: 1200000 },
    { name: 'روغن مایع', isService: false, salePrice: 850000 },
    { name: 'شکر (کیلو)', isService: false, salePrice: 380000 },
    { name: 'نوشابه خانواده', isService: false, salePrice: 320000 },
    { name: 'ماکارونی', isService: false, salePrice: 280000 },
  ],
  'پوشاک و البسه': [
    { name: 'پیراهن مردانه', isService: false, salePrice: 1850000 },
    { name: 'شلوار جین', isService: false, salePrice: 2400000 },
    { name: 'مانتو', isService: false, salePrice: 3200000 },
    { name: 'تی‌شرت', isService: false, salePrice: 950000 },
  ],
  'خدمات / مشاوره': [
    { name: 'مشاورهٔ ساعتی', isService: true, salePrice: 2500000 },
    { name: 'پشتیبانیِ ماهانه', isService: true, salePrice: 5000000 },
    { name: 'نصب و راه‌اندازی', isService: true, salePrice: 3500000 },
  ],
  'آرایشی و بهداشتی': [
    { name: 'شامپو', isService: false, salePrice: 480000 },
    { name: 'کرم مرطوب‌کننده', isService: false, salePrice: 650000 },
    { name: 'عطر', isService: false, salePrice: 2800000 },
    { name: 'لوازم آرایش', isService: false, salePrice: 1200000 },
  ],
  'داروخانه': [
    { name: 'استامینوفن', isService: false, salePrice: 85000 },
    { name: 'ویتامین C', isService: false, salePrice: 220000 },
    { name: 'ماسک', isService: false, salePrice: 35000 },
    { name: 'شربت سرماخوردگی', isService: false, salePrice: 180000 },
  ],
  'لوازم خانگی / دیجیتال': [
    { name: 'گوشی موبایل', isService: false, salePrice: 95000000 },
    { name: 'هندزفری', isService: false, salePrice: 1800000 },
    { name: 'شارژر', isService: false, salePrice: 850000 },
    { name: 'کابل USB', isService: false, salePrice: 250000 },
  ],
  'طلا و جواهر': [
    { name: 'انگشتر طلا (گرم)', isService: false, salePrice: 0 },
    { name: 'سرویس طلا', isService: false, salePrice: 0 },
    { name: 'سکه تمام', isService: false, salePrice: 0 },
  ],
};

/** یک‌سالِ بعدِ تاریخِ شمسیِ ورودی — پیش‌فرضِ سادهٔ پایانِ سالِ مالی (کاربر می‌تواند ویرایش کند). */
function plusOneYear(jalali: string): string {
  const [y, m, d] = jalali.split('/');
  return `${Number(y) + 1}/${m}/${d}`;
}

/** کدِ یکتا برای کالا/مشتریِ ثبت‌شده از ویزارد (timestamp + ایندکس — احتمالِ تداخل را کم می‌کند). */
function genCode(prefix: string, index: number): string {
  return `${prefix}${Date.now().toString().slice(-6)}${index + 1}`;
}

/**
 * ویزاردِ راه‌اندازیِ اولیهٔ وب (U-WEB-WIZARD) — معادلِ FirstRunWizardِ دسکتاپ. پیش‌تر فقط
 * شرکت/سال/ماژول/یک‌انبار/رمز داشت؛ حالا «صنف + دادهٔ پایهٔ چندردیفه» (انبار/کالا/مشتری) را هم
 * دارد تا یک شرکت از همان اولین اجرا دادهٔ واقعی‌اش را وارد کند (همان تجربهٔ دسکتاپ، در وب).
 */
export function SetupWizardPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  // step 0 — company + صنف
  const [company, setCompany] = useState({
    CompanyName: '', CompanyNationalId: '', CompanyEconomicCode: '', CompanyPhone: '', CompanyAddress: '',
    BusinessType: '',
  });

  // step 1 — fiscal year
  const [fyTitle, setFyTitle] = useState(`سالِ مالیِ ${todayJalaliString().split('/')[0]}`);
  const [fyStart, setFyStart] = useState(`${todayJalaliString().split('/')[0]}/01/01`);
  const [fyEnd, setFyEnd] = useState(plusOneYear(`${todayJalaliString().split('/')[0]}/01/01`));

  // step 2 — modules
  const [modules, setModules] = useState<ModuleRow[]>([]);
  const [modulesLoading, setModulesLoading] = useState(true);

  // step 3 — base data
  const [warehouses, setWarehouses] = useState<WizWarehouse[]>([{ name: 'انبارِ مرکزی' }]);
  const [products, setProducts] = useState<WizProduct[]>([{ name: '', isService: false, salePrice: '0', purchasePrice: '0' }]);
  const [customers, setCustomers] = useState<WizCustomer[]>([{ name: '', mobile: '', isCompany: false }]);
  const [baseSummary, setBaseSummary] = useState<string | null>(null);

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

  function applyBusinessPreset() {
    const preset = BUSINESS_PRESETS[company.BusinessType];
    if (!preset || preset.length === 0) {
      setErr('برای این صنف نمونهٔ آماده‌ای نیست؛ کالاها را دستی وارد کنید.');
      return;
    }
    setErr(null);
    setProducts(preset.map((p) => ({ name: p.name, isService: p.isService, salePrice: String(p.salePrice), purchasePrice: '0' })));
  }

  async function saveBaseData(): Promise<void> {
    const problems: string[] = [];
    let whN = 0, prN = 0, cuN = 0;

    for (const w of warehouses) {
      if (!w.name.trim()) continue;
      try { await apiPost('/api/warehouse', { name: w.name.trim() }); whN++; }
      catch (e) { problems.push(`انبار «${w.name.trim()}»: ${e instanceof ApiError ? e.message : 'خطا'}`); }
    }

    let pi = 0;
    for (const p of products) {
      if (!p.name.trim()) continue;
      try {
        await apiPost('/api/products', {
          code: genCode('K', pi), barcode: null, name: p.name.trim(), nameEn: null,
          groupId: null, brandId: null, unitId: 1, productType: p.isService ? 1 : 0,
          purchasePrice: Number(p.purchasePrice) || 0, salePrice: Number(p.salePrice) || 0,
          wholesalePrice: Number(p.salePrice) || 0, consumerPrice: Number(p.salePrice) || 0,
          minStock: 0, maxStock: null, hasSerial: false, hasBatch: false, hasExpiry: false,
          valuationMethod: 0, taxRate: 0, description: null, image: null,
        });
        prN++;
      } catch (e) { problems.push(`کالا «${p.name.trim()}»: ${e instanceof ApiError ? e.message : 'خطا'}`); }
      pi++;
    }

    let ci = 0;
    for (const c of customers) {
      if (!c.name.trim()) continue;
      try {
        await apiPost('/api/customers', {
          code: genCode('M', ci), customerType: c.isCompany ? 'حقوقی' : 'حقیقی',
          firstName: c.isCompany ? null : c.name.trim(), lastName: null, companyName: c.isCompany ? c.name.trim() : null,
          phone: c.mobile.trim() || null, mobile: c.mobile.trim() || null, email: null,
          province: null, city: null, address: null, postalCode: null,
          creditLimit: 0, creditDays: 0, priceLevel: 'خرده', discount: 0,
          nationalCode: null, economicCode: null, notes: null, contactPerson: null, visitor: null,
          isCustomerRole: true, isSupplierRole: false, isEmployeeRole: false, isSalespersonRole: false,
          groupId: null, birthDate: null,
        });
        cuN++;
      } catch (e) { problems.push(`مشتری «${c.name.trim()}»: ${e instanceof ApiError ? e.message : 'خطا'}`); }
      ci++;
    }

    setBaseSummary(problems.length === 0
      ? `✅ دادهٔ پایه ثبت شد: ${whN} انبار، ${prN} کالا/خدمت، ${cuN} مشتری.`
      : `⚠️ ${whN} انبار، ${prN} کالا/خدمت و ${cuN} مشتری ثبت شد؛ اما این موارد ناموفق بود: ${problems.join('؛ ')}`);
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
      setBusy(true);
      try {
        await saveBaseData();
        setStep(4);
      } finally {
        setBusy(false);
      }
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
    <div style={{ maxWidth: 680, margin: '32px auto' }}>
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
                <div className="field">
                  <label className="label">صنف / شغلِ شرکت</label>
                  <select className="input" value={company.BusinessType}
                    onChange={(e) => setCompany((p) => ({ ...p, BusinessType: e.target.value }))}>
                    <option value="">— انتخاب کنید —</option>
                    {BUSINESS_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                  </select>
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
                دادهٔ پایهٔ شروع را وارد کنید — همهٔ ردیف‌ها اختیاری‌اند و ردیفِ خالی نادیده گرفته می‌شود؛ بعداً هم قابلِ افزودن است.
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                <button type="button" className="btn btn-secondary btn-sm" onClick={applyBusinessPreset}>
                  پیش‌پُرِ کالاهای نمونه بر اساسِ صنف
                </button>
                {!company.BusinessType && (
                  <span style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)' }}>ابتدا صنف را در گامِ ۱ انتخاب کنید.</span>
                )}
              </div>

              <div className="field">
                <label className="label" style={{ fontWeight: 700 }}>انبارها</label>
                {warehouses.map((w, i) => (
                  <div key={i} style={{ display: 'flex', gap: 6, marginBottom: 6 }}>
                    <input className="input" placeholder="نامِ انبار" value={w.name}
                      onChange={(e) => setWarehouses((p) => p.map((r, j) => (j === i ? { ...r, name: e.target.value } : r)))} />
                    <button type="button" className="btn btn-ghost btn-sm"
                      onClick={() => setWarehouses((p) => p.filter((_, j) => j !== i))}>✕</button>
                  </div>
                ))}
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setWarehouses((p) => [...p, { name: '' }])}>+ انبار</button>
              </div>

              <div className="field">
                <label className="label" style={{ fontWeight: 700 }}>کالاها و خدمات</label>
                {products.map((p, i) => (
                  <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 110px 110px 90px 32px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
                    <input className="input" placeholder="نامِ کالا/خدمت" value={p.name}
                      onChange={(e) => setProducts((prev) => prev.map((r, j) => (j === i ? { ...r, name: e.target.value } : r)))} />
                    <input className="input" placeholder="فروش" type="number" min="0" value={p.salePrice}
                      onChange={(e) => setProducts((prev) => prev.map((r, j) => (j === i ? { ...r, salePrice: e.target.value } : r)))} />
                    <input className="input" placeholder="خرید" type="number" min="0" value={p.purchasePrice}
                      onChange={(e) => setProducts((prev) => prev.map((r, j) => (j === i ? { ...r, purchasePrice: e.target.value } : r)))} />
                    <label style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 'var(--text-xs)' }}>
                      <input type="checkbox" checked={p.isService}
                        onChange={(e) => setProducts((prev) => prev.map((r, j) => (j === i ? { ...r, isService: e.target.checked } : r)))} />
                      خدمت
                    </label>
                    <button type="button" className="btn btn-ghost btn-sm"
                      onClick={() => setProducts((prev) => prev.filter((_, j) => j !== i))}>✕</button>
                  </div>
                ))}
                <button type="button" className="btn btn-ghost btn-sm"
                  onClick={() => setProducts((prev) => [...prev, { name: '', isService: false, salePrice: '0', purchasePrice: '0' }])}>+ کالا/خدمت</button>
              </div>

              <div className="field">
                <label className="label" style={{ fontWeight: 700 }}>مشتری‌ها</label>
                {customers.map((c, i) => (
                  <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 180px 90px 32px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
                    <input className="input" placeholder="نامِ مشتری" value={c.name}
                      onChange={(e) => setCustomers((prev) => prev.map((r, j) => (j === i ? { ...r, name: e.target.value } : r)))} />
                    <input className="input" placeholder="موبایل/تلفن" value={c.mobile}
                      onChange={(e) => setCustomers((prev) => prev.map((r, j) => (j === i ? { ...r, mobile: e.target.value } : r)))} />
                    <label style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 'var(--text-xs)' }}>
                      <input type="checkbox" checked={c.isCompany}
                        onChange={(e) => setCustomers((prev) => prev.map((r, j) => (j === i ? { ...r, isCompany: e.target.checked } : r)))} />
                      حقوقی
                    </label>
                    <button type="button" className="btn btn-ghost btn-sm"
                      onClick={() => setCustomers((prev) => prev.filter((_, j) => j !== i))}>✕</button>
                  </div>
                ))}
                <button type="button" className="btn btn-ghost btn-sm"
                  onClick={() => setCustomers((prev) => [...prev, { name: '', mobile: '', isCompany: false }])}>+ مشتری</button>
              </div>
            </>
          )}

          {step === 4 && (
            <>
              {baseSummary && <StatusMessage kind={baseSummary.startsWith('✅') ? 'success' : 'error'}>{baseSummary}</StatusMessage>}
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
                  {busy ? 'در حالِ ذخیره…' : step === 3 ? 'ثبتِ دادهٔ پایه و بعدی' : 'بعدی'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
