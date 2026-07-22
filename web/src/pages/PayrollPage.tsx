import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiFetch, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { jalaliOf, todayJalaliString } from '../lib/jalali';
import { money } from '../lib/format';

interface SlipRow {
  employeeId: number; employeeName: string; department: string;
  baseSalary: number; overtime: number; allowances: number; insurance: number; tax: number; net: number;
}
interface RunResult {
  created: number; skipped: number; totalGross: number; totalNet: number;
  totalEmployeeInsurance: number; totalEmployerInsurance: number; totalTax: number;
}
interface PayrollSettingsDto {
  year: string; minWageMonthly: number; housingAllowance: number; foodAllowance: number;
  childAllowancePerChild: number; monthlyTaxExemption: number;
  insuranceEmployeeRate: number; insuranceEmployerRate: number; hoursPerMonth: number;
  overtimeFactor: number; holidayFactor: number; nightShiftFactor: number; maxChildren: number;
}

const MONTH_NAMES = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];

/** U-WEB-HR — حقوق و دستمزد (ماژولِ اختیاریِ HR). CQRSِ کامل از قبل در Modules.HR/Application
 * بود ولی هیچ endpoint/صفحهٔ وبی صدایش نمی‌زد. */
export function PayrollPage() {
  const nowJ = jalaliOf(new Date());
  const [year, setYear] = useState(String(nowJ.y));
  const [month, setMonth] = useState(nowJ.m);
  const [slips, setSlips] = useState<SlipRow[] | null>(null);
  const [runResult, setRunResult] = useState<RunResult | null>(null);
  const [voucherDate, setVoucherDate] = useState(todayJalaliString());
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [showSettings, setShowSettings] = useState(false);
  const [settings, setSettings] = useState<PayrollSettingsDto | null>(null);

  function loadSlips() {
    setError(null);
    apiGet<SlipRow[]>(`/api/hr/payroll/slips?year=${year}&month=${month}`)
      .then(setSlips)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ پیش‌نمایشِ حقوق.'));
  }

  useEffect(loadSlips, [year, month]);

  function loadSettings() {
    apiGet<PayrollSettingsDto>(`/api/hr/payroll/settings?year=${year}`).then(setSettings).catch(() => {});
  }

  useEffect(() => { if (showSettings) loadSettings(); }, [showSettings, year]); // eslint-disable-line react-hooks/exhaustive-deps

  async function runPayroll() {
    setBusy(true); setError(null); setNotice(null); setRunResult(null);
    try {
      const r = await apiPost<RunResult>('/api/hr/payroll/run', { year, month, overwrite: false });
      setRunResult(r);
      setNotice(`${r.created} فیش صادر شد، ${r.skipped} فیش (از قبل موجود) رد شد.`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'اجرایِ حقوقِ ماه ناموفق بود.');
    } finally { setBusy(false); }
  }

  async function postVoucher() {
    if (!window.confirm('سندِ حسابداریِ حقوقِ این ماه صادر شود؟')) return;
    setBusy(true); setError(null); setNotice(null);
    try {
      const r = await apiPost<{ voucherId: number; employeeCount: number; gross: number; net: number }>(
        '/api/hr/payroll/post-voucher', { date: voucherDate });
      setNotice(`سندِ شمارهٔ ${r.voucherId} برایِ ${r.employeeCount} نفر صادر شد.`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'صدورِ سندِ حقوق ناموفق بود.');
    } finally { setBusy(false); }
  }

  async function saveSettings() {
    if (!settings) return;
    setBusy(true); setError(null);
    try {
      await apiFetch('/api/hr/payroll/settings', { method: 'PUT', body: JSON.stringify(settings) });
      setNotice('تنظیماتِ سالِ حقوق ذخیره شد.');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیرهٔ تنظیمات ناموفق بود.');
    } finally { setBusy(false); }
  }

  const columns: Column<SlipRow>[] = [
    { key: 'name', header: 'کارمند', render: (r) => r.employeeName },
    { key: 'dept', header: 'واحد', render: (r) => r.department },
    { key: 'base', header: 'حقوقِ پایه', numeric: true, render: (r) => money(r.baseSalary) },
    { key: 'ins', header: 'بیمه', numeric: true, render: (r) => money(r.insurance) },
    { key: 'tax', header: 'مالیات', numeric: true, render: (r) => money(r.tax) },
    { key: 'net', header: 'خالصِ پرداختی', numeric: true, render: (r) => <b>{money(r.net)}</b> },
  ];

  return (
    <div>
      <PageHeader
        title="حقوق و دستمزد"
        actions={<button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowSettings((v) => !v)}>
          تنظیماتِ سالِ حقوق
        </button>}
      />

      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-end', marginBottom: 'var(--space-4)' }}>
        <div className="field" style={{ maxWidth: 140 }}>
          <label className="label">سال</label>
          <input className="input" value={year} onChange={(e) => setYear(e.target.value)} style={{ direction: 'ltr' }} />
        </div>
        <div className="field" style={{ maxWidth: 160 }}>
          <label className="label">ماه</label>
          <select className="select" value={month} onChange={(e) => setMonth(Number(e.target.value))}>
            {MONTH_NAMES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
          </select>
        </div>
        <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={runPayroll}>
          اجرایِ حقوقِ ماه
        </button>
      </div>

      {showSettings && settings && (
        <div className="gbox" style={{ marginBottom: 'var(--space-4)', padding: 'var(--space-4)' }}>
          <div className="gh">تنظیماتِ سالِ حقوقِ {year}</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
            <div className="field">
              <label className="label">حداقلِ حقوقِ ماهانه</label>
              <input className="input" type="number" value={settings.minWageMonthly}
                onChange={(e) => setSettings({ ...settings, minWageMonthly: Number(e.target.value) })} />
            </div>
            <div className="field">
              <label className="label">حق‌مسکن</label>
              <input className="input" type="number" value={settings.housingAllowance}
                onChange={(e) => setSettings({ ...settings, housingAllowance: Number(e.target.value) })} />
            </div>
            <div className="field">
              <label className="label">حق‌خواروبار</label>
              <input className="input" type="number" value={settings.foodAllowance}
                onChange={(e) => setSettings({ ...settings, foodAllowance: Number(e.target.value) })} />
            </div>
            <div className="field">
              <label className="label">معافیتِ مالیاتیِ ماهانه</label>
              <input className="input" type="number" value={settings.monthlyTaxExemption}
                onChange={(e) => setSettings({ ...settings, monthlyTaxExemption: Number(e.target.value) })} />
            </div>
            <div className="field">
              <label className="label">نرخِ بیمهٔ سهمِ کارمند</label>
              <input className="input" type="number" step="0.01" value={settings.insuranceEmployeeRate}
                onChange={(e) => setSettings({ ...settings, insuranceEmployeeRate: Number(e.target.value) })} />
            </div>
            <div className="field">
              <label className="label">نرخِ بیمهٔ سهمِ کارفرما</label>
              <input className="input" type="number" step="0.01" value={settings.insuranceEmployerRate}
                onChange={(e) => setSettings({ ...settings, insuranceEmployerRate: Number(e.target.value) })} />
            </div>
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveSettings}>ذخیرهٔ تنظیمات</button>
          </div>
          <div className="hint" style={{ marginTop: 6 }}>
            سایرِ نرخ‌ها (اضافه‌کاری/شب‌کاری/تعطیل/سقفِ فرزند/ساعتِ کاریِ ماه) با مقدارِ پیش‌فرض اعمال می‌شوند.
          </div>
        </div>
      )}

      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      {runResult && (
        <div className="sumbar" style={{ marginBottom: 'var(--space-4)' }}>
          <span>ناخالص: <b>{money(runResult.totalGross)}</b></span>
          <span>خالص: <b>{money(runResult.totalNet)}</b></span>
          <span>بیمهٔ کارمند: {money(runResult.totalEmployeeInsurance)}</span>
          <span>بیمهٔ کارفرما: {money(runResult.totalEmployerInsurance)}</span>
          <span>مالیات: {money(runResult.totalTax)}</span>
        </div>
      )}

      <div className="gh" style={{ marginBottom: 'var(--space-2)' }}>پیش‌نمایشِ حقوقِ ماه (بر مبنایِ حقوقِ پایه)</div>
      {slips && <DataTable columns={columns} rows={slips} rowKey={(r) => r.employeeId} emptyText="کارمندِ فعالی نیست." />}

      <div className="gbox" style={{ marginTop: 'var(--space-4)', padding: 'var(--space-4)', display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-end' }}>
        <JalaliDateInput value={voucherDate} onChange={setVoucherDate} label="تاریخِ سندِ حسابداریِ حقوق" />
        <button type="button" className="btn btn-secondary btn-sm" disabled={busy} onClick={postVoucher}>
          صدورِ سندِ حسابداریِ حقوق
        </button>
      </div>
    </div>
  );
}
