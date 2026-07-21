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
  const [voucherDate, setVoucherDate] = useState(todayJalaliString());
  const [description, setDescription] = useState('');
  const [lines, setLines] = useState<VoucherLine[]>([emptyLine(), emptyLine()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    apiGet<AccountOption[]>('/api/accounts?leafOnly=true').then(setAccounts).catch(() => {});
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
        voucherTypeId: 1,
        description: description || null,
        reference: null,
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

  return (
    <div>
      <PageHeader title="ثبتِ سندِ حسابداریِ نو" />
      <form onSubmit={submit}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 'var(--space-3)', marginBottom: 'var(--space-4)' }}>
          <JalaliDateInput label="تاریخِ سند" value={voucherDate} onChange={setVoucherDate} />
          <div className="field">
            <label className="label">شرحِ سند</label>
            <input className="input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="شرحِ کلیِ سند…" />
          </div>
        </div>

        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ textAlign: 'right', color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>
              <th style={{ padding: '6px 8px' }}>حساب</th>
              <th style={{ padding: '6px 8px' }}>شرح</th>
              <th style={{ padding: '6px 8px', width: 140 }}>بدهکار</th>
              <th style={{ padding: '6px 8px', width: 140 }}>بستانکار</th>
              <th style={{ width: 40 }} />
            </tr>
          </thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i} style={{ borderTop: '1px solid var(--border)' }}>
                <td style={{ padding: '6px 8px' }}>
                  <SearchSelect
                    options={accountOptions}
                    value={l.accountId}
                    onChange={(id) => updateLine(i, { accountId: id })}
                    placeholder="جست‌وجویِ حساب…"
                  />
                </td>
                <td style={{ padding: '6px 8px' }}>
                  <input className="input" value={l.description} onChange={(e) => updateLine(i, { description: e.target.value })} />
                </td>
                <td style={{ padding: '6px 8px' }}>
                  <input className="input num" type="number" min="0" value={l.debit}
                    onChange={(e) => updateLine(i, { debit: e.target.value, credit: e.target.value ? '' : l.credit })} />
                </td>
                <td style={{ padding: '6px 8px' }}>
                  <input className="input num" type="number" min="0" value={l.credit}
                    onChange={(e) => updateLine(i, { credit: e.target.value, debit: e.target.value ? '' : l.debit })} />
                </td>
                <td style={{ textAlign: 'center' }}>
                  {lines.length > 2 && (
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>✕</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr style={{ borderTop: '2px solid var(--border-strong)', fontWeight: 700 }}>
              <td colSpan={2} style={{ padding: '8px' }}>جمع</td>
              <td className="num" style={{ padding: '8px' }}>{money(totalDebit)}</td>
              <td className="num" style={{ padding: '8px' }}>{money(totalCredit)}</td>
              <td />
            </tr>
          </tfoot>
        </table>

        <button type="button" className="btn btn-secondary btn-sm" onClick={addLine} style={{ marginTop: 'var(--space-2)' }}>
          + ردیفِ نو
        </button>

        <div style={{ marginTop: 'var(--space-3)' }}>
          {totalDebit > 0 || totalCredit > 0 ? (
            <span className={`badge ${isBalanced ? 'badge-green' : 'badge-red'}`}>
              {isBalanced ? 'تراز است' : `تفاوت: ${money(Math.abs(totalDebit - totalCredit))}`}
            </span>
          ) : null}
        </div>

        {error && (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <StatusMessage kind="error">{error}</StatusMessage>
          </div>
        )}

        <button type="submit" className="btn btn-primary" disabled={submitting} style={{ marginTop: 'var(--space-4)' }}>
          {submitting ? 'در حالِ ثبت…' : 'ثبتِ سند'}
        </button>
      </form>
    </div>
  );
}
