import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { JalaliDateInput } from '../components/JalaliDateInput';

interface LoanRow {
  id: number;
  code: string;
  name: string;
  startDate: string;
  principal: number;
  annualInterestPercent: number;
  termMonths: number;
  status: number;
  paidInstallments: number;
  paidPrincipal: number;
  paidInterest: number;
  remainingPrincipal: number;
  lastPaymentDate: string | null;
  monthlyPayment: number;
}

interface Installment {
  index: number;
  payment: number;
  principal: number;
  interest: number;
  remaining: number;
}

interface Draft {
  code: string;
  name: string;
  startDate: string;
  principal: string;
  annualInterestPercent: string;
  termMonths: string;
}

const EMPTY_DRAFT: Draft = {
  code: '', name: '', startDate: todayJalaliString(),
  principal: '0', annualInterestPercent: '23', termMonths: '12',
};

function statusBadge(l: LoanRow) {
  if (l.status === 2) return <span className="badge badge-gray">تسویه‌شده</span>;
  return <span className="badge badge-green">در جریان</span>;
}

/**
 * U-LOAN — تسهیلاتِ مالی/وام (هم‌راستا با «تسهیلات مالی»یِ راهکاران): ثبتِ وام، جدولِ اقساطِ
 * مساوی (اصل/بهره/مانده) و پرداختِ ترتیبیِ قسط با صدورِ سندِ حسابداریِ خودکار.
 */
export function LoansPage() {
  const [rows, setRows] = useState<LoanRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const [saving, setSaving] = useState(false);
  const [scheduleLoan, setScheduleLoan] = useState<LoanRow | null>(null);
  const [schedule, setSchedule] = useState<Installment[]>([]);
  const [paying, setPaying] = useState(false);

  function load() {
    setLoading(true);
    apiGet<LoanRow[]>('/api/loans')
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ وام‌ها.'))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  function openCreate() { setDraft(EMPTY_DRAFT); setError(null); setNotice(null); setShowForm(true); }

  async function save() {
    setError(null);
    if (!draft.code.trim() || !draft.name.trim()) { setError('کد و نامِ وام الزامی است.'); return; }
    if (Number(draft.principal) <= 0) { setError('اصلِ وام باید بزرگ‌تر از صفر باشد.'); return; }
    if (Number(draft.annualInterestPercent) < 0) { setError('نرخِ بهره نمی‌تواند منفی باشد.'); return; }
    if (Number(draft.termMonths) <= 0) { setError('مدتِ وام باید بزرگ‌تر از صفر باشد.'); return; }
    setSaving(true);
    try {
      await apiPost('/api/loans', {
        code: draft.code.trim(), name: draft.name.trim(), startDate: draft.startDate,
        principal: Number(draft.principal), annualInterestPercent: Number(draft.annualInterestPercent),
        termMonths: Number(draft.termMonths),
      });
      setShowForm(false);
      setNotice('وام ثبت شد و سندِ دریافت صادر گردید.');
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  async function showSchedule(l: LoanRow) {
    setError(null); setNotice(null);
    setScheduleLoan(l);
    setSchedule([]);
    try {
      const s = await apiGet<Installment[]>(`/api/loans/${l.id}/schedule`);
      setSchedule(s);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بارگیریِ جدولِ اقساط ناموفق بود.');
    }
  }

  async function payNext(l: LoanRow) {
    setError(null); setNotice(null);
    const next = l.paidInstallments + 1;
    if (next > l.termMonths) { setError('همهٔ اقساط پرداخت شده‌اند.'); return; }
    setPaying(true);
    try {
      const res = await apiPost<{ voucherId: number }>(`/api/loans/${l.id}/installments/${next}/pay`, {
        id: l.id, installmentIndex: next, paymentDate: todayJalaliString(),
      });
      setNotice(`✅ قسطِ ${next}/${l.termMonths} پرداخت شد (شناسهٔ سند: ${res.voucherId}).`);
      if (scheduleLoan?.id === l.id) showSchedule(l);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'پرداختِ قسط ناموفق بود.');
    } finally {
      setPaying(false);
    }
  }

  const columns: Column<LoanRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => r.name },
    { key: 'principal', header: 'اصلِ وام', numeric: true, render: (r) => money(r.principal) },
    { key: 'monthlyPayment', header: 'قسطِ ماهانه', numeric: true, render: (r) => money(r.monthlyPayment) },
    { key: 'remaining', header: 'ماندهٔ اصل', numeric: true, render: (r) => money(r.remainingPrincipal) },
    {
      key: 'progress', header: 'اقساط',
      render: (r) => `${r.paidInstallments}/${r.termMonths} پرداخت‌شده`,
    },
    { key: 'status', header: 'وضعیت', render: statusBadge },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => showSchedule(r)}>جدولِ اقساط</button>
          <button type="button" className="btn btn-secondary btn-sm" disabled={r.status === 2 || paying} onClick={() => payNext(r)}>
            پرداختِ قسطِ بعدی
          </button>
        </div>
      ),
    },
  ];

  const scheduleColumns: Column<Installment>[] = [
    { key: 'index', header: '#', render: (r) => r.index },
    { key: 'payment', header: 'قسط', numeric: true, render: (r) => money(r.payment) },
    { key: 'principal', header: 'اصل', numeric: true, render: (r) => money(r.principal) },
    { key: 'interest', header: 'بهره', numeric: true, render: (r) => money(r.interest) },
    { key: 'remaining', header: 'مانده', numeric: true, render: (r) => money(r.remaining) },
  ];

  return (
    <div>
      <PageHeader
        title="تسهیلات مالی (وام)"
        actions={<button type="button" className="btn btn-primary btn-sm" onClick={openCreate}>وامِ نو</button>}
      />

      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {showForm && (
        <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="gh">ثبتِ وامِ نو</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(200px, 1fr))', gap: 'var(--space-3)' }}>
              <div className="field">
                <label className="label">کد</label>
                <input className="input" value={draft.code} onChange={(e) => setDraft((p) => ({ ...p, code: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">نام/طرف‌حساب</label>
                <input className="input" value={draft.name} onChange={(e) => setDraft((p) => ({ ...p, name: e.target.value }))} />
              </div>
              <JalaliDateInput label="تاریخِ دریافت" value={draft.startDate} onChange={(v) => setDraft((p) => ({ ...p, startDate: v }))} />
              <div className="field">
                <label className="label">مدت (ماه)</label>
                <input className="input" type="number" min="1" value={draft.termMonths} onChange={(e) => setDraft((p) => ({ ...p, termMonths: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">اصلِ وام (ریال)</label>
                <input className="input" type="number" min="0" value={draft.principal} onChange={(e) => setDraft((p) => ({ ...p, principal: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">نرخِ بهرهٔ سالانه (٪)</label>
                <input className="input" type="number" min="0" step="0.1" value={draft.annualInterestPercent} onChange={(e) => setDraft((p) => ({ ...p, annualInterestPercent: e.target.value }))} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
              <button type="button" className="btn btn-primary btn-sm" disabled={saving} onClick={save}>
                {saving ? 'در حالِ ذخیره…' : 'ثبت و صدورِ سندِ دریافت'}
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowForm(false)}>انصراف</button>
            </div>
          </div>
        </div>
      )}

      {scheduleLoan && (
        <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="gh" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>جدولِ اقساط — {scheduleLoan.name}</span>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => { setScheduleLoan(null); setSchedule([]); }}>بستن</button>
          </div>
          <div className="gb">
            <DataTable columns={scheduleColumns} rows={schedule} rowKey={(r) => r.index} emptyText="جدولی یافت نشد." />
          </div>
        </div>
      )}

      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="وام/تسهیلاتی ثبت نشده است." />}
    </div>
  );
}
