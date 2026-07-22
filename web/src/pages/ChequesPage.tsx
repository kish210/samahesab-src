import { useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect } from '../components/SearchSelect';
import { JalaliDateInput, isValidJalali } from '../components/JalaliDateInput';

interface ChequeBoardDto {
  id: number;
  chequeNumber: string;
  bankName: string;
  amount: number;
  dueDate: string;
  type: 'دریافتی' | 'پرداختی';
  dueState: 'Overdue' | 'DueToday' | 'Upcoming';
}

// ChequeStatus (بک‌اند، بدونِ JsonStringEnumConverter — سریال‌سازیِ عددی): InProcess=0, Cleared=1, Returned=2, Transferred=3, Cancelled=4
interface ChequeRowDto {
  id: number;
  chequeType: 0 | 1; // Received=0, Paid=1
  chequeNumber: string;
  bankName: string;
  amount: number;
  dueDate: string;
  status: 0 | 1 | 2 | 3 | 4;
  issuedBy: string;
  description: string;
}

interface PartyOption {
  id: number;
  name: string;
  code: string;
}

const STATE_LABEL: Record<ChequeBoardDto['dueState'], { text: string; cls: string; dot: string }> = {
  Overdue: { text: 'سررسیدگذشته', cls: 'badge-red', dot: 'var(--danger-500)' },
  DueToday: { text: 'سررسیدِ امروز', cls: 'badge-amber', dot: 'var(--warning-500)' },
  Upcoming: { text: 'آینده', cls: 'badge-gray', dot: 'var(--gray-400)' },
};

/** تابلویِ چک — پورتِ ساختاریِ design-system/screens/cheques.html: کاشی‌هایِ آماریِ کلیک‌پذیر
 * (فیلترِ وضعیت) + چیپِ نوع (دریافتی/پرداختی) + فیلترِ بانک/بازهٔ تاریخ/جست‌وجو + گریدِ چک با
 * اقداماتِ واقعیِ تغییرِ وضعیت (وصول/واگذاری‌به‌بانک/برگشت) + ثبتِ چکِ نو. */
export function ChequesPage() {
  const [rows, setRows] = useState<ChequeBoardDto[]>([]);
  const [allCheques, setAllCheques] = useState<ChequeRowDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [typeFilter, setTypeFilter] = useState<'همه' | 'دریافتی' | 'پرداختی'>('همه');
  const [stateFilter, setStateFilter] = useState<ChequeBoardDto['dueState'] | 'همه'>('همه');
  const [bankFilter, setBankFilter] = useState('');
  const [search, setSearch] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const [showNew, setShowNew] = useState(false);
  const [customers, setCustomers] = useState<PartyOption[]>([]);
  const [suppliers, setSuppliers] = useState<PartyOption[]>([]);
  const [newType, setNewType] = useState<'دریافتی' | 'پرداختی'>('دریافتی');
  const [newPartyId, setNewPartyId] = useState<number | null>(null);
  const [newChequeNumber, setNewChequeNumber] = useState('');
  const [newBank, setNewBank] = useState('');
  const [newAmount, setNewAmount] = useState('');
  const [newDueDate, setNewDueDate] = useState(todayJalaliString());
  const [newDescription, setNewDescription] = useState('');
  const [newError, setNewError] = useState<string | null>(null);
  const [savingNew, setSavingNew] = useState(false);

  function load() {
    setLoading(true);
    Promise.all([
      apiGet<ChequeBoardDto[]>(`/api/cheques/board?today=${encodeURIComponent(todayJalaliString())}`),
      apiGet<ChequeRowDto[]>('/api/cheques'),
    ])
      .then(([board, all]) => { setRows(board); setAllCheques(all); setSelectedId(null); })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تابلویِ چک.'))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);
  useEffect(() => {
    apiGet<PartyOption[]>('/api/customers').then(setCustomers).catch(() => {});
    apiGet<PartyOption[]>('/api/suppliers').then(setSuppliers).catch(() => {});
  }, []);

  const banks = useMemo(() => Array.from(new Set(rows.map((r) => r.bankName))).sort(), [rows]);

  const filtered = useMemo(
    () => rows.filter((r) =>
      (typeFilter === 'همه' || r.type === typeFilter) &&
      (stateFilter === 'همه' || r.dueState === stateFilter) &&
      (!bankFilter || r.bankName === bankFilter) &&
      (!fromDate || r.dueDate >= fromDate) &&
      (!toDate || r.dueDate <= toDate) &&
      (!search.trim() || r.chequeNumber.includes(search.trim()) || r.bankName.includes(search.trim()))
    ),
    [rows, typeFilter, stateFilter, bankFilter, fromDate, toDate, search],
  );
  const stats = useMemo(() => {
    const of = (state: ChequeBoardDto['dueState'] | 'همه') => {
      const list = state === 'همه' ? rows : rows.filter((r) => r.dueState === state);
      return { count: list.length, amount: list.reduce((s, r) => s + r.amount, 0) };
    };
    const ofStatus = (status: 1 | 2) => {
      const list = allCheques.filter((c) => c.status === status);
      return { count: list.length, amount: list.reduce((s, c) => s + c.amount, 0) };
    };
    return { all: of('همه'), overdue: of('Overdue'), dueToday: of('DueToday'), upcoming: of('Upcoming'), cleared: ofStatus(1), returned: ofStatus(2) };
  }, [rows, allCheques]);

  const filteredTotal = useMemo(() => filtered.reduce((s, r) => s + r.amount, 0), [filtered]);

  // ChequeAction (بک‌اند): Clear=0, Return=1, Transfer=2 — سریال‌سازیِ JSONِ سرور برایِ enum عددی
  // است (بدونِ JsonStringEnumConverter)، پس باید همان مقدارِ عددی ارسال شود، نه نامِ رشته‌ای.
  const ACTION_CODE = { Clear: 0, Return: 1, Transfer: 2 } as const;

  async function act(action: keyof typeof ACTION_CODE) {
    if (!selectedId) return;
    let returnReason: string | undefined;
    if (action === 'Return') {
      returnReason = window.prompt('علتِ برگشتِ چک؟') ?? undefined;
      if (!returnReason) return;
    }
    setBusy(true);
    setError(null);
    try {
      await apiPost(`/api/cheques/${selectedId}/status`, { action: ACTION_CODE[action], date: todayJalaliString(), returnReason });
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیتِ چک ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  function resetNewForm() {
    setNewType('دریافتی');
    setNewPartyId(null);
    setNewChequeNumber('');
    setNewBank('');
    setNewAmount('');
    setNewDueDate(todayJalaliString());
    setNewDescription('');
    setNewError(null);
  }

  async function submitNew() {
    setNewError(null);
    if (!newPartyId) { setNewError('طرف‌حساب الزامی است.'); return; }
    if (!newChequeNumber.trim()) { setNewError('شمارهٔ چک الزامی است.'); return; }
    if (!newBank.trim()) { setNewError('نامِ بانک الزامی است.'); return; }
    if (!(Number(newAmount) > 0)) { setNewError('مبلغِ چک باید بزرگ‌تر از صفر باشد.'); return; }
    if (!isValidJalali(newDueDate)) { setNewError('تاریخِ سررسید معتبر نیست.'); return; }

    setSavingNew(true);
    try {
      await apiPost('/api/cheques', {
        chequeType: newType === 'دریافتی' ? 0 : 1,
        chequeNumber: newChequeNumber.trim(),
        bankName: newBank.trim(),
        amount: Number(newAmount),
        dueDate: newDueDate,
        partyId: newPartyId,
        partyType: newType === 'دریافتی' ? 'Customer' : 'Supplier',
        date: todayJalaliString(),
        description: newDescription || null,
      });
      setShowNew(false);
      resetNewForm();
      load();
    } catch (e) {
      setNewError(e instanceof ApiError ? e.message : 'ثبتِ چک ناموفق بود.');
    } finally {
      setSavingNew(false);
    }
  }

  const columns: Column<ChequeBoardDto>[] = [
    { key: 'num', header: 'شمارهٔ چک', render: (r) => r.chequeNumber },
    { key: 'bank', header: 'بانک', render: (r) => r.bankName },
    { key: 'type', header: 'نوع', render: (r) => <span className={`badge ${r.type === 'دریافتی' ? 'badge-blue' : 'badge-gray'}`}>{r.type}</span> },
    { key: 'due', header: 'سررسید', render: (r) => r.dueDate },
    { key: 'amount', header: 'مبلغ', numeric: true, render: (r) => money(r.amount) },
    {
      key: 'state', header: 'وضعیت',
      render: (r) => <span className={`badge ${STATE_LABEL[r.dueState].cls}`}>{STATE_LABEL[r.dueState].text}</span>,
    },
  ];

  return (
    <div>
      <PageHeader title="تابلویِ چک" actions={
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowNew((v) => !v)}>+ چکِ جدید</button>
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => window.print()}>چاپ</button>
          <button type="button" className="btn btn-primary btn-sm" disabled={!selectedId || busy} onClick={() => act('Clear')}>وصول</button>
          <button type="button" className="btn btn-secondary btn-sm" disabled={!selectedId || busy} onClick={() => act('Transfer')}>واگذاری به بانک</button>
          <button type="button" className="btn btn-secondary btn-sm" style={{ color: 'var(--danger-700)' }} disabled={!selectedId || busy} onClick={() => act('Return')}>برگشتِ چک</button>
        </div>
      } />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {showNew && (
        <div className="gbox" style={{ marginBottom: 'var(--space-3)' }}>
          <div className="gh">ثبتِ چکِ جدید</div>
          <div className="gb" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
            <div className="field">
              <label className="label">نوع</label>
              <select className="select" value={newType} onChange={(e) => { setNewType(e.target.value as 'دریافتی' | 'پرداختی'); setNewPartyId(null); }}>
                <option value="دریافتی">دریافتی (از مشتری)</option>
                <option value="پرداختی">پرداختی (به تأمین‌کننده)</option>
              </select>
            </div>
            <div className="field">
              <label className="label">طرف‌حساب</label>
              <SearchSelect
                options={(newType === 'دریافتی' ? customers : suppliers).map((p) => ({ id: p.id, label: p.name, sublabel: p.code }))}
                value={newPartyId}
                onChange={setNewPartyId}
                placeholder="جست‌وجو…"
              />
            </div>
            <div className="field">
              <label className="label">شمارهٔ چک</label>
              <input className="input" value={newChequeNumber} onChange={(e) => setNewChequeNumber(e.target.value)} />
            </div>
            <div className="field">
              <label className="label">بانک</label>
              <input className="input" value={newBank} onChange={(e) => setNewBank(e.target.value)} />
            </div>
            <div className="field">
              <label className="label">مبلغ</label>
              <input className="input" type="number" min="0" value={newAmount} onChange={(e) => setNewAmount(e.target.value)} />
            </div>
            <JalaliDateInput label="تاریخِ سررسید" value={newDueDate} onChange={setNewDueDate} />
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="label">توضیحات</label>
              <input className="input" value={newDescription} onChange={(e) => setNewDescription(e.target.value)} />
            </div>
          </div>
          {newError && <div style={{ padding: '0 12px 10px' }}><StatusMessage kind="error">{newError}</StatusMessage></div>}
          <div style={{ padding: '0 12px 12px', display: 'flex', gap: 8 }}>
            <button className="btn btn-primary btn-sm" disabled={savingNew} onClick={submitNew}>{savingNew ? 'در حالِ ثبت…' : 'ثبتِ چک'}</button>
            <button className="btn btn-secondary btn-sm" onClick={() => { setShowNew(false); resetNewForm(); }}>انصراف</button>
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 10, marginBottom: 'var(--space-3)' }}>
        {([
          ['همه', 'نزدِ صندوق (در جریان)', stats.all, 'var(--blue-600)', true],
          ['Overdue', 'سررسیدگذشته', stats.overdue, 'var(--danger-500)', true],
          ['DueToday', 'سررسیدِ امروز', stats.dueToday, 'var(--warning-500)', true],
          ['Upcoming', 'آینده', stats.upcoming, 'var(--gray-400)', true],
        ] as const).map(([key, label, s, dot]) => (
          <div key={key}
            onClick={() => setStateFilter(key as typeof stateFilter)}
            style={{
              background: 'var(--bg-surface)', cursor: 'pointer', borderRadius: 'var(--radius-md)', padding: '10px 12px',
              border: `1px solid ${stateFilter === key ? 'var(--blue-600)' : 'var(--border)'}`,
              boxShadow: stateFilter === key ? 'var(--ring-focus)' : 'none',
            }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 8, height: 8, borderRadius: 99, background: dot, display: 'inline-block' }} />
              {label}
            </div>
            <div style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{s.count} فقره</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-muted)', marginTop: 1 }}>{money(s.amount)} ریال</div>
          </div>
        ))}
        {([
          ['وصول‌شده', stats.cleared, 'var(--success-500)'],
          ['برگشتی', stats.returned, 'var(--danger-500)'],
        ] as const).map(([label, s, dot]) => (
          <div key={label} title="آماریِ کلی — چون این چک‌ها دیگر در تابلویِ در‌جریان نیستند، کلیک فیلتری اعمال نمی‌کند."
            style={{ background: 'var(--gray-50)', borderRadius: 'var(--radius-md)', padding: '10px 12px', border: '1px solid var(--border)' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 8, height: 8, borderRadius: 99, background: dot, display: 'inline-block' }} />
              {label}
            </div>
            <div style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{s.count} فقره</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-muted)', marginTop: 1 }}>{money(s.amount)} ریال</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 'var(--space-3)', flexWrap: 'wrap', alignItems: 'end' }}>
        {(['همه', 'دریافتی', 'پرداختی'] as const).map((t) => (
          <button key={t} type="button" className={`chip${typeFilter === t ? ' active' : ''}`} onClick={() => setTypeFilter(t)}>
            {t}
          </button>
        ))}
        <div className="field">
          <label className="label">بانک</label>
          <select className="select" value={bankFilter} onChange={(e) => setBankFilter(e.target.value)}>
            <option value="">همهٔ بانک‌ها</option>
            {banks.map((b) => (
              <option key={b} value={b}>{b}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label className="label">از سررسید</label>
          <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} placeholder="1405/01/01" style={{ width: 120 }} />
        </div>
        <div className="field">
          <label className="label">تا سررسید</label>
          <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} placeholder="1405/12/29" style={{ width: 120 }} />
        </div>
        <div className="field">
          <label className="label">جست‌وجو</label>
          <input className="input" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="شمارهٔ چک / بانک…" style={{ width: 180 }} />
        </div>
      </div>

      {loading && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && (
        <>
          <DataTable
            columns={columns}
            rows={filtered}
            rowKey={(r) => r.id}
            emptyText="چکی در جریان نیست."
            selectedKey={selectedId}
            onRowClick={(r) => setSelectedId(r.id)}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>
            <span>جمعِ فیلترشده — {filtered.length} فقره · {money(filteredTotal)} ریال</span>
          </div>
        </>
      )}
    </div>
  );
}
