import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface TrialBalanceRow {
  code: string;
  name: string;
  debit: number;
  credit: number;
  balance: number;
  accountId: number;
}

interface AccountDto {
  id: number;
  code: string;
  name: string;
  level: number;
  parentId: number | null;
  isLeaf: boolean;
}

const LEVEL_OPTIONS = [
  { value: 4, label: 'تفصیلی' },
  { value: 3, label: 'معین' },
  { value: 2, label: 'کل' },
];

export function TrialBalancePage() {
  const navigate = useNavigate();
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [rows, setRows] = useState<TrialBalanceRow[] | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [level, setLevel] = useState(4);
  const [hideZero, setHideZero] = useState(true);

  useEffect(() => {
    apiGet<AccountDto[]>('/api/accounts').then(setAccounts).catch(() => {});
  }, []);

  async function search() {
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<TrialBalanceRow[]>(`/api/reports/trial-balance?from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}`);
      setRows(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تراز آزمایشی.');
    } finally {
      setLoading(false);
    }
  }

  const accountsById = useMemo(() => new Map(accounts.map((a) => [a.id, a])), [accounts]);

  function ancestorAtLevel(accountId: number, targetLevel: number): AccountDto | null {
    let cur = accountsById.get(accountId);
    if (!cur) return null;
    while (cur && cur.level > targetLevel && cur.parentId != null) {
      cur = accountsById.get(cur.parentId);
    }
    return cur ?? null;
  }

  // گروه‌بندیِ ردیف‌ها بر اساسِ سطحِ انتخاب‌شده — هرگاه level کمتر از ۴ (تفصیلی) باشد،
  // چند حسابِ برگ زیرِ یک نیایِ مشترک جمع می‌شوند (سطحِ کل/معین)؛ برایِ سطحِ ۴ رفتار عیناً
  // قبلی (فهرستِ تخت) است.
  const groupedRows = useMemo(() => {
    if (!rows) return null;
    if (level === 4 || accounts.length === 0) {
      return rows.map((r) => ({ code: r.code, name: r.name, debit: r.debit, credit: r.credit, balance: r.balance, accountId: r.accountId as number | null }));
    }
    const byGroup = new Map<string, { code: string; name: string; debit: number; credit: number; balance: number; accountId: number | null }>();
    for (const r of rows) {
      const anc = ancestorAtLevel(r.accountId, level);
      const key = anc ? anc.code : r.code;
      const existing = byGroup.get(key);
      if (existing) {
        existing.debit += r.debit;
        existing.credit += r.credit;
        existing.balance += r.balance;
      } else {
        byGroup.set(key, { code: key, name: anc ? anc.name : r.name, debit: r.debit, credit: r.credit, balance: r.balance, accountId: anc?.isLeaf ? anc.id : null });
      }
    }
    return Array.from(byGroup.values()).sort((a, b) => a.code.localeCompare(b.code));
  }, [rows, level, accounts]);

  const visibleRows = useMemo(() => {
    if (!groupedRows) return null;
    return hideZero ? groupedRows.filter((r) => r.debit !== 0 || r.credit !== 0) : groupedRows;
  }, [groupedRows, hideZero]);

  const totalDebit = visibleRows?.reduce((s, r) => s + r.debit, 0) ?? 0;
  const totalCredit = visibleRows?.reduce((s, r) => s + r.credit, 0) ?? 0;
  const diff = totalDebit - totalCredit;
  const groupCount = new Set(visibleRows?.map((r) => r.code.split('-')[0])).size;

  function exportCsv() {
    if (!visibleRows) return;
    const header = ['کد', 'نام', 'بدهکار', 'بستانکار', 'مانده'];
    const body = visibleRows.map((r) => [r.code, r.name, r.debit, r.credit, r.balance]);
    const csv = [header, ...body].map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'تراز-آزمایشی.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div>
      <PageHeader title="تراز آزمایشی" actions={
        <>
          <button className="btn btn-secondary" onClick={() => navigate('/general-ledger')}>دفترِ کل</button>
          <button className="btn btn-secondary" onClick={exportCsv} disabled={!visibleRows}>خروجی اکسل</button>
          <button className="btn btn-secondary" onClick={() => window.print()}>چاپ</button>
        </>
      } />
      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)', flexWrap: 'wrap' }}>
        <div className="field">
          <label className="label">از تاریخ</label>
          <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="field">
          <label className="label">تا تاریخ</label>
          <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <div className="field">
          <label className="label">سطحِ گزارش</label>
          <select className="select" value={level} onChange={(e) => setLevel(Number(e.target.value))}>
            {LEVEL_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 32, fontSize: 'var(--text-sm)' }}>
          <input type="checkbox" checked={hideZero} onChange={(e) => setHideZero(e.target.checked)} />
          حذفِ حساب‌هایِ بی‌گردش
        </label>
        <button className="btn btn-primary" onClick={search} disabled={loading}>
          {loading ? 'در حالِ جست‌وجو…' : 'نمایش'}
        </button>
        {visibleRows && (
          <span style={{ fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>
            {groupCount} گروه · {visibleRows.length} حساب
          </span>
        )}
      </div>
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {visibleRows && !error && (
        <div className="print-area">
          <div className="print-only" style={{ display: 'none', marginBottom: 'var(--space-3)' }}>
            <h2>تراز آزمایشی</h2>
            <div>از {fromDate} تا {toDate}</div>
          </div>
          <div className="dgrid-wrap">
            <table className="dgrid">
              <thead>
                <tr>
                  <th style={{ width: 90 }}>کد</th>
                  <th>نام</th>
                  <th style={{ width: 140 }} className="num">بدهکار</th>
                  <th style={{ width: 140 }} className="num">بستانکار</th>
                  <th style={{ width: 140 }} className="num">مانده</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((r) => (
                  <tr key={r.code} className={level < 4 ? 'grp-row' : undefined}
                    style={level < 4 ? { background: 'var(--gray-50)', fontWeight: 700 } : undefined}>
                    <td className="num">{r.code}</td>
                    <td>
                      {r.accountId ? <Link to={`/general-ledger?accountId=${r.accountId}`}>{r.name}</Link> : r.name}
                    </td>
                    <td className="num">{money(r.debit)}</td>
                    <td className="num">{money(r.credit)}</td>
                    <td className="num strong">{money(r.balance)}</td>
                  </tr>
                ))}
                {visibleRows.length === 0 && (
                  <tr>
                    <td colSpan={5} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                      ردیفی یافت نشد.
                    </td>
                  </tr>
                )}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={2}>جمعِ کل</td>
                  <td className="num">{money(totalDebit)}</td>
                  <td className="num">{money(totalCredit)}</td>
                  <td className="num">{money(totalDebit - totalCredit)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
          <div className={`sumbar ${Math.abs(diff) < 1 ? 'ok' : 'bad'}`} style={{ marginTop: 'var(--space-3)' }}>
            <b>{Math.abs(diff) < 1 ? '✓ تراز است' : '✗ ناتراز'}</b>
            <div className="grow" />
            <div className="s"><span className="l">جمعِ بدهکار</span><span className="v">{money(totalDebit)}</span></div>
            <div className="s"><span className="l">جمعِ بستانکار</span><span className="v">{money(totalCredit)}</span></div>
            <div className="s"><span className="l">اختلاف</span><span className="v" style={{ color: Math.abs(diff) < 1 ? 'var(--success-500)' : 'var(--danger-500)' }}>{money(Math.abs(diff))}</span></div>
          </div>
        </div>
      )}
    </div>
  );
}
