import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { JalaliDateInput } from '../components/JalaliDateInput';

interface BankAccount {
  id: number;
  bankName: string;
  accountNumber: string;
  sheba: string;
  cardNumber: string;
  branchName: string;
  openingBalance: number;
  isActive: boolean;
}

interface ReconMatchRow {
  voucherItemId: number;
  date: string;
  amount: number;
  description: string;
  reference: string;
}

interface LedgerRow {
  voucherItemId: number;
  date: string;
  amount: number;
  description: string;
}

interface StatementRow {
  date: string;
  amount: number;
  reference: string | null;
}

interface ReconResult {
  bankName: string;
  matchedCount: number;
  unmatchedLedgerCount: number;
  unmatchedStatementCount: number;
  alreadyReconciledCount: number;
  lastReconciledDate: string | null;
  matched: ReconMatchRow[];
  unmatchedLedger: LedgerRow[];
  unmatchedStatement: StatementRow[];
}

/**
 * U-BANK-RECON-WEB — مغایرت‌گیری بانکی: دفترِ بانکِ سیستم را با صورت‌حسابِ CSVِ بانک
 * (هر خط: تاریخ,مبلغ[,شرح]) تطبیقِ خودکار می‌دهد؛ ردیف‌های تطبیق‌شده را ماندگار ثبت می‌کند.
 * پورتِ وبِ BankReconciliationView دسکتاپ روی همان موتورهای خالص Application.
 */
export function BankReconciliationPage() {
  const [accounts, setAccounts] = useState<BankAccount[]>([]);
  const [accountId, setAccountId] = useState<number | null>(null);
  const [from, setFrom] = useState(todayJalaliString().replace(/\d{2}\/\d{2}$/, '01/01'));
  const [to, setTo] = useState(todayJalaliString());
  const [statementCsv, setStatementCsv] = useState('');
  const [result, setResult] = useState<ReconResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);

  function load() {
    setLoading(true);
    apiGet<BankAccount[]>('/api/bankaccounts?activeOnly=true')
      .then((list) => {
        setAccounts(list);
        setAccountId((prev) => prev ?? list[0]?.id ?? null);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ حساب‌های بانکی.'))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  async function runReconcile() {
    if (accountId == null) { setError('یک حساب بانکی را انتخاب کنید.'); return; }
    if (!statementCsv.trim()) { setError('صورت‌حساب CSV خالی است.'); return; }
    setError(null); setNotice(null); setWorking(true);
    try {
      const res = await apiPost<ReconResult>(`/api/bankaccounts/${accountId}/reconcile`, {
        from, to, statementCsv,
      });
      setResult(res);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تطبیق ناموفق بود.');
    } finally {
      setWorking(false);
    }
  }

  async function commitReconcile() {
    if (accountId == null || !result || result.matchedCount === 0) {
      setError('ردیف تطبیق‌شده‌ای برای ثبت وجود ندارد.');
      return;
    }
    setError(null); setNotice(null); setWorking(true);
    try {
      const res = await apiPost<{ added: number }>(`/api/bankaccounts/${accountId}/reconcile/commit`, {
        voucherItemIds: result.matched.map((m) => m.voucherItemId),
        date: todayJalaliString(),
      });
      setNotice(`✅ ${res.added} ردیف به‌صورت ماندگار ثبت شد و در تطبیق‌های بعدی نمایش داده نمی‌شود.`);
      setResult(null);
      setStatementCsv('');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ثبتِ تطبیق ناموفق بود.');
    } finally {
      setWorking(false);
    }
  }

  const matchedColumns: Column<ReconMatchRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    { key: 'description', header: 'شرحِ دفتر', render: (r) => r.description },
    { key: 'reference', header: 'شرحِ صورت‌حساب', render: (r) => r.reference },
  ];

  const ledgerColumns: Column<LedgerRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    { key: 'description', header: 'شرح', render: (r) => r.description },
  ];

  const statementColumns: Column<StatementRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    { key: 'reference', header: 'شرح', render: (r) => r.reference ?? '' },
  ];

  return (
    <div>
      <PageHeader title="مغایرت‌گیری بانکی" />

      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
        <div className="gh">صورت‌حساب بانک (CSV)</div>
        <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(200px, 1fr))', gap: 'var(--space-3)' }}>
            <div className="field">
              <label className="label">حساب بانکی</label>
              <select className="input" value={accountId ?? ''} onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : null)}>
                {accounts.map((a) => (
                  <option key={a.id} value={a.id}>{a.bankName} — {a.accountNumber}</option>
                ))}
              </select>
            </div>
            <div className="field" style={{ display: 'flex', gap: 'var(--space-3)' }}>
              <JalaliDateInput label="از تاریخ" value={from} onChange={setFrom} />
              <JalaliDateInput label="تا تاریخ" value={to} onChange={setTo} />
            </div>
          </div>
          <div className="field">
            <label className="label">صورت‌حساب CSV — هر خط: تاریخ,مبلغ[,شرح] (مبلغِ واریز مثبت، برداشت منفی)</label>
            <textarea
              className="input"
              rows={6}
              dir="ltr"
              placeholder={"تاریخ,مبلغ,شرح\n1404/05/01,1000000,واریز مشتری\n1404/05/02,-250000,پرداخت چک"}
              value={statementCsv}
              onChange={(e) => setStatementCsv(e.target.value)}
            />
          </div>
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button type="button" className="btn btn-primary btn-sm" disabled={working || accountId == null} onClick={runReconcile}>
              {working ? 'در حالِ تطبیق…' : 'تطبیق خودکار'}
            </button>
            {result && (
              <button type="button" className="btn btn-secondary btn-sm" disabled={working || result.matchedCount === 0} onClick={commitReconcile}>
                ثبتِ ماندگارِ {result.matchedCount} ردیفِ منطبق
              </button>
            )}
          </div>
        </div>
      </div>

      {result && (
        <>
          <StatusMessage kind="muted">
            {result.bankName} — {result.matchedCount} منطبق، {result.unmatchedLedgerCount} نامنطبقِ دفتر،{' '}
            {result.unmatchedStatementCount} نامنطبقِ صورت‌حساب
            {result.alreadyReconciledCount > 0 && (
              <> · {result.alreadyReconciledCount} ردیف قبلاً تطبیق‌شده{result.lastReconciledDate ? ` (آخرین: ${result.lastReconciledDate})` : ''}</>
            )}
          </StatusMessage>

          <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="gh">ردیف‌های منطبق</div>
            <div className="gb">
              <DataTable columns={matchedColumns} rows={result.matched} rowKey={(r) => r.voucherItemId} emptyText="هیچ ردیف منطبقی یافت نشد." />
            </div>
          </div>

          <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="gh">نامنطبقِ دفترِ سیستم</div>
            <div className="gb">
              <DataTable columns={ledgerColumns} rows={result.unmatchedLedger} rowKey={(r) => r.voucherItemId} emptyText="هیچ ردیف نامنطبقی در دفتر نیست." />
            </div>
          </div>

          <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="gh">نامنطبقِ صورت‌حساب بانک</div>
            <div className="gb">
              <DataTable columns={statementColumns} rows={result.unmatchedStatement} rowKey={(r, i) => `${r.date}-${r.amount}-${i}`} emptyText="هیچ ردیف نامنطبقی در صورت‌حساب نیست." />
            </div>
          </div>
        </>
      )}

      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
    </div>
  );
}
