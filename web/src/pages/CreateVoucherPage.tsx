import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';
import { useAuth } from '../auth/AuthContext';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';
import { todayJalaliString } from '../lib/jalali';
import { money } from '../lib/format';

interface AccountOption {
  id: number;
  code: string;
  name: string;
}

interface VoucherTypeOption {
  id: number;
  name: string;
}

interface CostCenterOption {
  id: number;
  code: string;
  name: string;
}

interface VoucherLine {
  accountId: number | null;
  debit: string;
  credit: string;
  description: string;
}

function emptyLine(): VoucherLine {
  return { accountId: null, debit: '', credit: '', description: '' };
}

/** ثبتِ سندِ حسابداری (دستی) — معادلِ وب برایِ VoucherEditView.xaml دسکتاپ؛
 * فقط حساب‌هایِ سرفصلِ‌پایان (leaf) قابلِ‌انتخاب‌اند، نه حساب‌هایِ گروه. */
export function CreateVoucherPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const fiscalYearId = useActiveFiscalYear();
  const [accounts, setAccounts] = useState<AccountOption[]>([]);
  const [voucherTypes, setVoucherTypes] = useState<VoucherTypeOption[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenterOption[]>([]);
  const [voucherDate, setVoucherDate] = useState(todayJalaliString());
  const [voucherTypeId, setVoucherTypeId] = useState<number>(9);
  const [costCenterId, setCostCenterId] = useState<number | null>(null);
  const [reference, setReference] = useState('');
  const [description, setDescription] = useState('');
  const [lines, setLines] = useState<VoucherLine[]>([emptyLine(), emptyLine()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    apiGet<AccountOption[]>('/api/accounts?leafOnly=true').then(setAccounts).catch(() => {});
    apiGet<VoucherTypeOption[]>('/api/vouchers/types').then(setVoucherTypes).catch(() => {});
    apiGet<CostCenterOption[]>('/api/accounting/dimensions/cost-centers?activeOnly=true').then(setCostCenters).catch(() => {});
  }, []);

  const totalDebit = lines.reduce((s, l) => s + (Number(l.debit) || 0), 0);
  const totalCredit = lines.reduce((s, l) => s + (Number(l.credit) || 0), 0);
  const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01 && totalDebit > 0;

  function updateLine(i: number, patch: Partial<VoucherLine>) {
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  }
  function removeLine(i: number) {
    setLines((prev) => prev.filter((_, idx) => idx !== i));
  }
  function addLine() {
    setLines((prev) => [...prev, emptyLine()]);
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!isValidJalali(voucherDate)) {
      setError('تاریخِ سند معتبر نیست (قالب: ۱۴۰۵/۰۴/۲۶).');
      return;
    }
    const items = lines
      .filter((l) => l.accountId && (Number(l.debit) || Number(l.credit)))
      .map((l, i) => ({
        rowNumber: i + 1,
        accountId: l.accountId!,
        debit: Number(l.debit) || 0,
        credit: Number(l.credit) || 0,
        description: l.description || null,
        costCenterId,
        projectId: null,
      }));
    if (items.length < 2) {
      setError('سند باید حداقل دو ردیفِ حساب داشته باشد.');
      return;
    }
    if (!isBalanced) {
      setError(`سند تراز نیست — بدهکار ${money(totalDebit)} ≠ بستانکار ${money(totalCredit)}.`);
      return;
    }

    setSubmitting(true);
    try {
      const res = await apiPost<{ voucherId: number }>('/api/Vouchers', {
        branchId: user?.branchId ?? 1,
        fiscalYearId: fiscalYearId ?? 1,
        voucherDate,
        voucherTypeId,
        description: description || null,
        reference: reference || null,
        currencyId: null,
        exchangeRate: 1,
        items,
      });
      navigate('/vouchers', { replace: true, state: { justCreated: res.voucherId } });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ثبتِ سند ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  const accountOptions = accounts.map((a) => ({ id: a.id, label: a.name, sublabel: a.code }));

  const hasAmounts = totalDebit > 0 || totalCredit > 0;

  return (
    <div>
      <PageHeader title="ثبتِ سندِ حسابداریِ نو" />
      <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {/* پورتِ `.gbox`ِ design-system برایِ مشخصاتِ سند (voucher.html) */}
        <div className="gbox">
          <div className="gh">
            مشخصاتِ سند
            <span className="st a" style={{ marginInlineStart: 6 }}><i />پیش‌نویس</span>
          </div>
          <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12 }}>
            <JalaliDateInput label="تاریخِ سند" value={voucherDate} onChange={setVoucherDate} />
            <div className="field">
              <label className="label">نوعِ سند</label>
              <select className="select" value={voucherTypeId} onChange={(e) => setVoucherTypeId(Number(e.target.value))}>
                {voucherTypes.map((t) => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="label">مرکزِ هزینه</label>
              <select className="select" value={costCenterId ?? ''} onChange={(e) => setCostCenterId(e.target.value ? Number(e.target.value) : null)}>
                <option value="">— بدونِ مرکزِ هزینه —</option>
                {costCenters.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="label">ارجاع</label>
              <input className="input" value={reference} onChange={(e) => setReference(e.target.value)} placeholder="شمارهٔ مرجع/سفارش…" />
            </div>
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="label">شرحِ سند</label>
              <input className="input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="شرحِ کلیِ سند…" />
            </div>
          </div>
        </div>

        <div className="dgrid-wrap">
          <table className="dgrid">
            <thead>
              <tr>
                <th>حساب</th>
                <th>شرح</th>
                <th style={{ width: 140 }} className="num">بدهکار</th>
                <th style={{ width: 140 }} className="num">بستانکار</th>
                <th style={{ width: 36 }} className="c" />
              </tr>
            </thead>
            <tbody>
              {lines.map((l, i) => (
                <tr key={i}>
                  <td>
                    <SearchSelect
                      options={accountOptions}
                      value={l.accountId}
                      onChange={(id) => updateLine(i, { accountId: id })}
                      placeholder="جست‌وجویِ حساب…"
                    />
                  </td>
                  <td>
                    <input className="input" value={l.description} onChange={(e) => updateLine(i, { description: e.target.value })} />
                  </td>
                  <td className="num">
                    <input className="input num" type="number" min="0" value={l.debit}
                      onChange={(e) => updateLine(i, { debit: e.target.value, credit: e.target.value ? '' : l.credit })} />
                  </td>
                  <td className="num">
                    <input className="input num" type="number" min="0" value={l.credit}
                      onChange={(e) => updateLine(i, { credit: e.target.value, debit: e.target.value ? '' : l.debit })} />
                  </td>
                  <td className="c">
                    {lines.length > 2 && (
                      <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>✕</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={2}>جمع — {lines.length} ردیف</td>
                <td className="num">{money(totalDebit)}</td>
                <td className="num">{money(totalCredit)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>

        <div>
          <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>
            + ردیفِ نو
          </button>
        </div>

        {/* پورتِ `.sumbar`ِ design-system — نوارِ جمع/تراز پایینِ فرم */}
        {hasAmounts && (
          <div className={`sumbar ${isBalanced ? 'ok' : 'bad'}`}>
            <b>{isBalanced ? 'سند تراز است' : 'سند تراز نیست'}</b>
            <div className="grow" />
            <div className="s"><span className="l">جمعِ بدهکار</span><span className="v">{money(totalDebit)}</span></div>
            <div className="s"><span className="l">جمعِ بستانکار</span><span className="v">{money(totalCredit)}</span></div>
            <div className="s"><span className="l">اختلاف</span><span className="v" style={{ color: isBalanced ? 'var(--success-500)' : 'var(--danger-500)' }}>{money(Math.abs(totalDebit - totalCredit))}</span></div>
          </div>
        )}

        {error && <StatusMessage kind="error">{error}</StatusMessage>}

        <div>
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'در حالِ ثبت…' : 'ثبتِ سند'}
          </button>
        </div>
      </form>
    </div>
  );
}
