import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface ProjectRow {
  id: number; code: string; title: string; employerPartyId: number; employerName: string;
  contractAmount: number; advancePercent: number; retentionPercent: number; insurancePercent: number;
  taxPercent: number; advanceOutstanding: number; status: string;
}
interface DashboardDto {
  projectId: number; code: string; title: string; contractAmount: number; cumulativeBilled: number;
  grossBilled: number; netBilled: number; retentionHeld: number; insuranceHeld: number; taxWithheld: number;
  advanceReceived: number; advanceOutstanding: number; progressPercent: number; profit: number; postedStatementCount: number;
}
interface AdvanceRow {
  id: number; amount: number; date: string; recoveredToDate: number; outstanding: number;
  paymentMethod: string; voucherId: number | null; note: string | null;
}
interface GuaranteeRow {
  id: number; contractProjectId: number; type: number; bank: string; amount: number;
  issueDate: string; expiryDate: string; status: number; daysToExpiry: number | null; isExpiringSoon: boolean;
}
interface StatementRow {
  id: number; number: number; type: number; date: string; grossThisPeriod: number; advanceRecovery: number;
  retention: number; insurance: number; tax: number; penalty: number; other: number; netPayable: number;
  status: number; voucherId: number | null;
}
interface PersonOption { id: number; name: string }
interface SettingsDto {
  receivableAccountId: number | null; retentionDepositAccountId: number | null; insuranceDepositAccountId: number | null;
  prepaidTaxAccountId: number | null; advanceLiabilityAccountId: number | null; penaltyExpenseAccountId: number | null;
  revenueAccountId: number | null; bankAccountId: number | null;
  defaultAdvancePercent: number; defaultRetentionPercent: number; defaultInsuranceWithholdPercent: number;
  defaultTaxWithholdPercent: number; useCostCenterAsDimension: boolean;
}

const numberFormat = new Intl.NumberFormat('fa-IR');
const CONTRACT_TYPES = ['فهرست‌بها', 'مقطوع', 'امانی'];
const GUARANTEE_TYPES = ['پیش‌پرداخت', 'حسن‌انجام‌کار', 'شرکت‌درمناقصه'];
const GUARANTEE_STATUSES = ['فعال', 'آزادشده', 'منقضی'];
const STATEMENT_TYPES = ['موقت', 'قطعی'];
const STATEMENT_STATUSES = ['پیش‌نویس', 'تأییدشده', 'ثبت‌شده'];

/** U-WEB-CONTRACTING — ماژولِ پیمانکاری. پیش از این نشست هیچ Commandی برایِ ساختِ پیمان
 * وجود نداشت (نه در وب، نه در دسکتاپ) — با SaveContractProjectCommand رفع شد. */
export function ContractingPage() {
  const [tab, setTab] = useState<'projects' | 'settings'>('projects');
  const [projects, setProjects] = useState<ProjectRow[]>([]);
  const [selected, setSelected] = useState<ProjectRow | null>(null);
  const [dashboard, setDashboard] = useState<DashboardDto | null>(null);
  const [advances, setAdvances] = useState<AdvanceRow[]>([]);
  const [guarantees, setGuarantees] = useState<GuaranteeRow[]>([]);
  const [statements, setStatements] = useState<StatementRow[]>([]);
  const [employers, setEmployers] = useState<PersonOption[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  function loadProjects() {
    apiGet<ProjectRow[]>('/api/contracting/projects').then(setProjects)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ پیمان‌ها.'));
  }
  useEffect(loadProjects, []);
  useEffect(() => {
    apiGet<PersonOption[]>('/api/persons').then(setEmployers).catch(() => {});
  }, []);

  function loadProjectDetail(id: number) {
    apiGet<DashboardDto>(`/api/contracting/projects/${id}/dashboard`).then(setDashboard).catch(() => setDashboard(null));
    apiGet<AdvanceRow[]>(`/api/contracting/projects/${id}/advances`).then(setAdvances).catch(() => {});
    apiGet<GuaranteeRow[]>(`/api/contracting/guarantees?projectId=${id}&activeOnly=false`).then(setGuarantees).catch(() => {});
    apiGet<StatementRow[]>(`/api/contracting/projects/${id}/statements`).then(setStatements).catch(() => {});
  }

  function selectProject(p: ProjectRow) {
    setSelected(p);
    loadProjectDetail(p.id);
  }

  // ── فرمِ پیمانِ نو ──
  const [showNewProject, setShowNewProject] = useState(false);
  const [pCode, setPCode] = useState('');
  const [pTitle, setPTitle] = useState('');
  const [pEmployerId, setPEmployerId] = useState<number | null>(null);
  const [pType, setPType] = useState(0);
  const [pAmount, setPAmount] = useState('');
  const [pStart, setPStart] = useState(todayJalaliString());
  const [pAdvance, setPAdvance] = useState('0');
  const [pRetention, setPRetention] = useState('0');
  const [pInsurance, setPInsurance] = useState('0');
  const [pTax, setPTax] = useState('0');

  async function saveProject() {
    if (!pCode.trim() || !pTitle.trim() || !pEmployerId) { setError('کد/عنوان/کارفرما الزامی است.'); return; }
    try {
      await apiPost('/api/contracting/projects', {
        id: 0, code: pCode, title: pTitle, employerPartyId: pEmployerId, contractType: pType,
        contractAmount: Number(pAmount) || 0, startDate: pStart,
        advancePercent: Number(pAdvance) || 0, retentionPercent: Number(pRetention) || 0,
        insuranceWithholdPercent: Number(pInsurance) || 0, taxWithholdPercent: Number(pTax) || 0,
      });
      setNotice('پیمان ثبت شد.');
      setShowNewProject(false);
      setPCode(''); setPTitle(''); setPEmployerId(null); setPAmount('');
      loadProjects();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ پیمان ناموفق بود.'); }
  }

  // ── دریافتِ پیش‌پرداخت ──
  const [advAmount, setAdvAmount] = useState('');
  const [advDate, setAdvDate] = useState(todayJalaliString());
  async function receiveAdvance() {
    if (!selected) return;
    try {
      await apiPost('/api/contracting/advances/receive', {
        contractProjectId: selected.id, date: advDate, amount: Number(advAmount) || 0,
      });
      setNotice('پیش‌پرداخت ثبت شد.');
      setAdvAmount('');
      loadProjectDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ پیش‌پرداخت ناموفق بود.'); }
  }

  // ── ضمانت‌نامهٔ نو ──
  const [showNewGuarantee, setShowNewGuarantee] = useState(false);
  const [gType, setGType] = useState(0);
  const [gBank, setGBank] = useState('');
  const [gAmount, setGAmount] = useState('');
  const [gIssue, setGIssue] = useState(todayJalaliString());
  const [gExpiry, setGExpiry] = useState(todayJalaliString());
  async function registerGuarantee() {
    if (!selected) return;
    try {
      await apiPost('/api/contracting/guarantees', {
        contractProjectId: selected.id, type: gType, bank: gBank, amount: Number(gAmount) || 0,
        issueDate: gIssue, expiryDate: gExpiry,
      });
      setNotice('ضمانت‌نامه ثبت شد.');
      setShowNewGuarantee(false);
      setGBank(''); setGAmount('');
      loadProjectDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ ضمانت‌نامه ناموفق بود.'); }
  }
  async function releaseGuarantee(id: number) {
    try {
      await apiPost(`/api/contracting/guarantees/${id}/release`, {});
      setNotice('ضمانت‌نامه آزاد شد.');
      if (selected) loadProjectDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'آزادسازی ناموفق بود.'); }
  }

  // ── صورت‌وضعیتِ نو ──
  const [showNewStatement, setShowNewStatement] = useState(false);
  const [sNumber, setSNumber] = useState('1');
  const [sType, setSType] = useState(0);
  const [sDate, setSDate] = useState(todayJalaliString());
  const [sCumulative, setSCumulative] = useState('');
  const [sPrevious, setSPrevious] = useState('0');
  async function saveStatement() {
    if (!selected) return;
    try {
      await apiPost('/api/contracting/statements', {
        contractProjectId: selected.id, number: Number(sNumber) || 1, type: sType, date: sDate,
        cumulativeGrossWork: Number(sCumulative) || 0, previousCumulative: Number(sPrevious) || 0,
      });
      setNotice('صورت‌وضعیت ذخیره شد.');
      setShowNewStatement(false);
      setSCumulative('');
      loadProjectDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ذخیرهٔ صورت‌وضعیت ناموفق بود.'); }
  }
  async function postStatement(id: number) {
    try {
      await apiPost(`/api/contracting/statements/${id}/post`, {});
      setNotice('صورت‌وضعیت ثبتِ سند شد.');
      if (selected) loadProjectDetail(selected.id);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ سند ناموفق بود.'); }
  }

  // ── تنظیمات ──
  const [settings, setSettings] = useState<SettingsDto | null>(null);
  useEffect(() => {
    if (tab === 'settings') apiGet<SettingsDto>('/api/contracting/settings').then(setSettings).catch(() => {});
  }, [tab]);
  async function saveSettings() {
    if (!settings) return;
    try {
      await apiPost('/api/contracting/settings', settings);
      setNotice('تنظیماتِ پیمانکاری ذخیره شد.');
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ذخیرهٔ تنظیمات ناموفق بود.'); }
  }
  function accField(label: string, key: keyof SettingsDto) {
    return (
      <div className="field">
        <label className="label">{label}</label>
        <input className="input" type="number" style={{ direction: 'ltr' }}
          value={(settings as any)?.[key] ?? ''}
          onChange={(e) => setSettings((s) => s && { ...s, [key]: e.target.value ? Number(e.target.value) : null })} />
      </div>
    );
  }

  const employerOptions: SearchSelectOption[] = employers.map((e) => ({ id: e.id, label: e.name }));

  const projectColumns: Column<ProjectRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'title', header: 'عنوان', render: (r) => <a onClick={() => selectProject(r)} style={{ cursor: 'pointer' }}>{r.title}</a> },
    { key: 'employer', header: 'کارفرما', render: (r) => r.employerName },
    { key: 'amount', header: 'مبلغِ پیمان', numeric: true, render: (r) => numberFormat.format(r.contractAmount) },
    { key: 'advOut', header: 'ماندهٔ پیش‌پرداخت', numeric: true, render: (r) => numberFormat.format(r.advanceOutstanding) },
    { key: 'status', header: 'وضعیت', render: (r) => r.status },
  ];

  const advanceColumns: Column<AdvanceRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => numberFormat.format(r.amount) },
    { key: 'recovered', header: 'بازیافت‌شده', numeric: true, render: (r) => numberFormat.format(r.recoveredToDate) },
    { key: 'outstanding', header: 'مانده', numeric: true, render: (r) => numberFormat.format(r.outstanding) },
    { key: 'method', header: 'روش', render: (r) => r.paymentMethod },
  ];

  const guaranteeColumns: Column<GuaranteeRow>[] = [
    { key: 'type', header: 'نوع', render: (r) => GUARANTEE_TYPES[r.type] ?? r.type },
    { key: 'bank', header: 'بانک', render: (r) => r.bank },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => numberFormat.format(r.amount) },
    { key: 'expiry', header: 'انقضا', render: (r) => (
      <span className={r.isExpiringSoon ? 'badge badge-yellow' : ''}>{r.expiryDate}{r.daysToExpiry != null ? ` (${r.daysToExpiry} روز)` : ''}</span>
    ) },
    { key: 'status', header: 'وضعیت', render: (r) => GUARANTEE_STATUSES[r.status] ?? r.status },
    {
      key: 'action', header: '', render: (r) => r.status === 0 ? (
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => releaseGuarantee(r.id)}>آزادسازی</button>
      ) : null,
    },
  ];

  const statementColumns: Column<StatementRow>[] = [
    { key: 'number', header: '#', render: (r) => r.number },
    { key: 'type', header: 'نوع', render: (r) => STATEMENT_TYPES[r.type] ?? r.type },
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'gross', header: 'ناخالص', numeric: true, render: (r) => numberFormat.format(r.grossThisPeriod) },
    { key: 'net', header: 'خالصِ قابلِ‌پرداخت', numeric: true, render: (r) => numberFormat.format(r.netPayable) },
    { key: 'status', header: 'وضعیت', render: (r) => STATEMENT_STATUSES[r.status] ?? r.status },
    {
      key: 'action', header: '', render: (r) => r.status !== 2 ? (
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => postStatement(r.id)}>ثبتِ سند</button>
      ) : (r.voucherId ? <span style={{ color: 'var(--text-muted)' }}>سندِ #{r.voucherId}</span> : null),
    },
  ];

  return (
    <div>
      <PageHeader title="پیمانکاری" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'projects' ? 'on' : ''} onClick={() => setTab('projects')}>پیمان‌ها</button>
        <button type="button" className={tab === 'settings' ? 'on' : ''} onClick={() => setTab('settings')}>تنظیمات</button>
      </div>

      {tab === 'projects' && (
        <div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNewProject((v) => !v)}>پیمانِ نو</button>
          </div>
          {showNewProject && (
            <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
              <div className="gh">پیمانِ نو</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
                <div className="field"><label className="label">کد</label><input className="input" value={pCode} onChange={(e) => setPCode(e.target.value)} /></div>
                <div className="field" style={{ gridColumn: 'span 2' }}><label className="label">عنوان</label><input className="input" value={pTitle} onChange={(e) => setPTitle(e.target.value)} /></div>
                <div className="field">
                  <label className="label">نوعِ پیمان</label>
                  <select className="select" value={pType} onChange={(e) => setPType(Number(e.target.value))}>
                    {CONTRACT_TYPES.map((t, i) => <option key={t} value={i}>{t}</option>)}
                  </select>
                </div>
                <div className="field" style={{ gridColumn: 'span 2' }}>
                  <label className="label">کارفرما</label>
                  <SearchSelect options={employerOptions} value={pEmployerId} onChange={setPEmployerId} placeholder="جست‌وجویِ کارفرما…" />
                </div>
                <div className="field"><label className="label">مبلغِ پیمان</label><input className="input" type="number" style={{ direction: 'ltr' }} value={pAmount} onChange={(e) => setPAmount(e.target.value)} /></div>
                <JalaliDateInput value={pStart} onChange={setPStart} label="تاریخِ شروع" />
                <div className="field"><label className="label">درصدِ پیش‌پرداخت</label><input className="input" type="number" value={pAdvance} onChange={(e) => setPAdvance(e.target.value)} /></div>
                <div className="field"><label className="label">درصدِ حسن‌انجام‌کار</label><input className="input" type="number" value={pRetention} onChange={(e) => setPRetention(e.target.value)} /></div>
                <div className="field"><label className="label">درصدِ بیمه</label><input className="input" type="number" value={pInsurance} onChange={(e) => setPInsurance(e.target.value)} /></div>
                <div className="field"><label className="label">درصدِ مالیات</label><input className="input" type="number" value={pTax} onChange={(e) => setPTax(e.target.value)} /></div>
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button type="button" className="btn btn-primary btn-sm" onClick={saveProject}>ثبتِ پیمان</button>
              </div>
            </div>
          )}
          <DataTable columns={projectColumns} rows={projects} rowKey={(r) => r.id} emptyText="پیمانی ثبت نشده." />

          {selected && (
            <div style={{ marginTop: 'var(--space-5)' }}>
              <div className="gh">پیمانِ «{selected.title}» ({selected.code})</div>
              {dashboard && (
                <div className="sumbar" style={{ marginTop: 'var(--space-2)' }}>
                  <span>پیشرفت: {dashboard.progressPercent}٪</span>
                  <span>ناخالصِ صورت‌وضعیت‌ها: {numberFormat.format(dashboard.grossBilled)}</span>
                  <span>خالصِ قابلِ‌پرداخت: {numberFormat.format(dashboard.netBilled)}</span>
                  <span>سپردهٔ حسن‌انجام: {numberFormat.format(dashboard.retentionHeld)}</span>
                  <span>سپردهٔ بیمه: {numberFormat.format(dashboard.insuranceHeld)}</span>
                  <span>ماندهٔ پیش‌پرداخت: {numberFormat.format(dashboard.advanceOutstanding)}</span>
                  <span>سود: {numberFormat.format(dashboard.profit)}</span>
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)', marginTop: 'var(--space-4)' }}>
                <div>
                  <div className="gh">پیش‌پرداخت‌ها</div>
                  <div style={{ display: 'flex', gap: 'var(--space-2)', margin: 'var(--space-2) 0', alignItems: 'flex-end' }}>
                    <div className="field"><label className="label">مبلغ</label><input className="input" type="number" style={{ direction: 'ltr' }} value={advAmount} onChange={(e) => setAdvAmount(e.target.value)} /></div>
                    <JalaliDateInput value={advDate} onChange={setAdvDate} label="تاریخ" />
                    <button type="button" className="btn btn-secondary btn-sm" onClick={receiveAdvance}>دریافت</button>
                  </div>
                  <DataTable columns={advanceColumns} rows={advances} rowKey={(r) => r.id} emptyText="پیش‌پرداختی ثبت نشده." />
                </div>
                <div>
                  <div className="gh">ضمانت‌نامه‌ها</div>
                  <div style={{ margin: 'var(--space-2) 0' }}>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowNewGuarantee((v) => !v)}>ضمانت‌نامهٔ نو</button>
                  </div>
                  {showNewGuarantee && (
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 'var(--space-2)', marginBottom: 'var(--space-2)' }}>
                      <select className="select" value={gType} onChange={(e) => setGType(Number(e.target.value))}>
                        {GUARANTEE_TYPES.map((t, i) => <option key={t} value={i}>{t}</option>)}
                      </select>
                      <input className="input" placeholder="بانک" value={gBank} onChange={(e) => setGBank(e.target.value)} />
                      <input className="input" type="number" placeholder="مبلغ" style={{ direction: 'ltr' }} value={gAmount} onChange={(e) => setGAmount(e.target.value)} />
                      <JalaliDateInput value={gIssue} onChange={setGIssue} label="صدور" />
                      <JalaliDateInput value={gExpiry} onChange={setGExpiry} label="انقضا" />
                      <button type="button" className="btn btn-primary btn-sm" onClick={registerGuarantee}>ثبت</button>
                    </div>
                  )}
                  <DataTable columns={guaranteeColumns} rows={guarantees} rowKey={(r) => r.id} emptyText="ضمانت‌نامه‌ای ثبت نشده." />
                </div>
              </div>

              <div style={{ marginTop: 'var(--space-4)' }}>
                <div className="gh">صورت‌وضعیت‌ها</div>
                <div style={{ margin: 'var(--space-2) 0' }}>
                  <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowNewStatement((v) => !v)}>صورت‌وضعیتِ نو</button>
                </div>
                {showNewStatement && (
                  <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-3)' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 'var(--space-2)' }}>
                      <div className="field"><label className="label">شماره</label><input className="input" type="number" value={sNumber} onChange={(e) => setSNumber(e.target.value)} /></div>
                      <div className="field">
                        <label className="label">نوع</label>
                        <select className="select" value={sType} onChange={(e) => setSType(Number(e.target.value))}>
                          {STATEMENT_TYPES.map((t, i) => <option key={t} value={i}>{t}</option>)}
                        </select>
                      </div>
                      <JalaliDateInput value={sDate} onChange={setSDate} label="تاریخ" />
                      <div className="field"><label className="label">کارکردِ تجمعی</label><input className="input" type="number" style={{ direction: 'ltr' }} value={sCumulative} onChange={(e) => setSCumulative(e.target.value)} /></div>
                      <div className="field"><label className="label">تجمعیِ قبلی</label><input className="input" type="number" style={{ direction: 'ltr' }} value={sPrevious} onChange={(e) => setSPrevious(e.target.value)} /></div>
                    </div>
                    <div style={{ marginTop: 'var(--space-3)' }}>
                      <button type="button" className="btn btn-primary btn-sm" onClick={saveStatement}>ذخیره (محاسبهٔ آبشار)</button>
                    </div>
                  </div>
                )}
                <DataTable columns={statementColumns} rows={statements} rowKey={(r) => r.id} emptyText="صورت‌وضعیتی ثبت نشده." />
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'settings' && settings && (
        <div className="gbox" style={{ padding: 'var(--space-4)' }}>
          <div className="gh">نگاشتِ حساب‌ها (شناسهٔ حساب)</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
            {accField('دریافتنیِ کارفرما', 'receivableAccountId')}
            {accField('سپردهٔ حسن‌انجام‌کار', 'retentionDepositAccountId')}
            {accField('سپردهٔ بیمه', 'insuranceDepositAccountId')}
            {accField('پیش‌پرداختِ مالیات', 'prepaidTaxAccountId')}
            {accField('بدهیِ پیش‌پرداخت', 'advanceLiabilityAccountId')}
            {accField('هزینهٔ جریمه/سایر', 'penaltyExpenseAccountId')}
            {accField('درآمدِ پیمان', 'revenueAccountId')}
            {accField('بانک', 'bankAccountId')}
          </div>
          <div className="gh" style={{ marginTop: 'var(--space-4)' }}>درصدهایِ پیش‌فرض</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
            <div className="field"><label className="label">پیش‌پرداخت٪</label><input className="input" type="number" value={settings.defaultAdvancePercent} onChange={(e) => setSettings((s) => s && { ...s, defaultAdvancePercent: Number(e.target.value) || 0 })} /></div>
            <div className="field"><label className="label">حسن‌انجام‌کار٪</label><input className="input" type="number" value={settings.defaultRetentionPercent} onChange={(e) => setSettings((s) => s && { ...s, defaultRetentionPercent: Number(e.target.value) || 0 })} /></div>
            <div className="field"><label className="label">بیمه٪</label><input className="input" type="number" value={settings.defaultInsuranceWithholdPercent} onChange={(e) => setSettings((s) => s && { ...s, defaultInsuranceWithholdPercent: Number(e.target.value) || 0 })} /></div>
            <div className="field"><label className="label">مالیات٪</label><input className="input" type="number" value={settings.defaultTaxWithholdPercent} onChange={(e) => setSettings((s) => s && { ...s, defaultTaxWithholdPercent: Number(e.target.value) || 0 })} /></div>
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={saveSettings}>ذخیرهٔ تنظیمات</button>
          </div>
        </div>
      )}
    </div>
  );
}
