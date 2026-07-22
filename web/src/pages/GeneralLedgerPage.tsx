import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface AccountDto {
  id: number;
  code: string;
  name: string;
  level: number;
  parentId: number | null;
  isLeaf: boolean;
}

interface LedgerRow {
  date: string;
  voucherNumber: string;
  code: string;
  name: string;
  description: string | null;
  debit: number;
  credit: number;
  balance: number;
}

interface AccountNode extends AccountDto {
  children: AccountNode[];
}

function buildTree(accounts: AccountDto[]): AccountNode[] {
  const byId = new Map<number, AccountNode>(accounts.map((a) => [a.id, { ...a, children: [] }]));
  const roots: AccountNode[] = [];
  for (const node of byId.values()) {
    if (node.parentId != null && byId.has(node.parentId)) {
      byId.get(node.parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  const sortByCode = (list: AccountNode[]) => {
    list.sort((a, b) => a.code.localeCompare(b.code));
    list.forEach((n) => sortByCode(n.children));
  };
  sortByCode(roots);
  return roots;
}

function AccountTreeNode({ node, selectedId, expanded, onToggle, onSelect }: {
  node: AccountNode;
  selectedId: number | null;
  expanded: Set<number>;
  onToggle: (id: number) => void;
  onSelect: (a: AccountDto) => void;
}) {
  const isOpen = expanded.has(node.id);
  const hasChildren = node.children.length > 0;
  return (
    <div>
      <div
        onClick={() => (hasChildren ? onToggle(node.id) : onSelect(node))}
        style={{
          display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer', padding: '4px 6px', borderRadius: 4,
          background: selectedId === node.id ? 'var(--blue-100)' : 'transparent',
          fontWeight: node.isLeaf ? 400 : 600,
          fontSize: node.isLeaf ? 12.5 : 12.5,
        }}
      >
        <span style={{ width: 14, display: 'inline-block', textAlign: 'center', color: 'var(--text-muted)' }}>
          {hasChildren ? (isOpen ? '▾' : '▸') : ''}
        </span>
        <span className="mut" style={{ fontVariantNumeric: 'tabular-nums', fontSize: 11 }}>{node.code}</span>
        <span
          onClick={(e) => { if (node.isLeaf) { e.stopPropagation(); onSelect(node); } }}
          style={{ color: node.isLeaf ? 'var(--text-strong)' : 'var(--text-body)' }}
        >
          {node.name}
        </span>
      </div>
      {isOpen && hasChildren && (
        <div style={{ paddingInlineStart: 16 }}>
          {node.children.map((c) => (
            <AccountTreeNode key={c.id} node={c} selectedId={selectedId} expanded={expanded} onToggle={onToggle} onSelect={onSelect} />
          ))}
        </div>
      )}
    </div>
  );
}

export function GeneralLedgerPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [selectedAccount, setSelectedAccount] = useState<AccountDto | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const accountId = selectedAccount?.id ?? null;
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [rows, setRows] = useState<LedgerRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    apiGet<AccountDto[]>('/api/accounts').then((list) => {
      setAccounts(list);
      const fromUrl = searchParams.get('accountId');
      if (fromUrl) {
        const a = list.find((x) => x.id === Number(fromUrl));
        if (a) {
          setSelectedAccount(a);
          setExpanded(new Set(ancestorIds(list, a.id)));
        }
      }
    }).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function ancestorIds(list: AccountDto[], id: number): number[] {
    const byId = new Map(list.map((a) => [a.id, a]));
    const ids: number[] = [];
    let cur = byId.get(id);
    while (cur?.parentId != null) {
      ids.push(cur.parentId);
      cur = byId.get(cur.parentId);
    }
    return ids;
  }

  const tree = useMemo(() => buildTree(accounts), [accounts]);

  async function search(forAccountId: number) {
    setLoading(true);
    setError(null);
    try {
      const data = await apiGet<LedgerRow[]>(
        `/api/reports/general-ledger?from=${encodeURIComponent(fromDate)}&to=${encodeURIComponent(toDate)}&accountId=${forAccountId}`,
      );
      setRows(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ دفترِ کل/معین.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (accountId) search(accountId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accountId]);

  function toggleExpand(id: number) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function exportCsv() {
    if (!rows || !selectedAccount) return;
    const header = ['تاریخ', 'شمارهٔ سند', 'شرح', 'بدهکار', 'بستانکار', 'مانده'];
    const body = rows.map((r) => [r.date, r.voucherNumber, r.description ?? '', r.debit, r.credit, r.balance]);
    const csv = [header, ...body].map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `دفتر-معین-${selectedAccount.code}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  const columns: Column<LedgerRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => r.date },
    { key: 'num', header: 'شمارهٔ سند', render: (r) => r.voucherNumber },
    { key: 'desc', header: 'شرح', render: (r) => r.description ?? '' },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.debit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.credit) },
    { key: 'balance', header: 'مانده', numeric: true, render: (r) => <span style={{ fontWeight: 600 }}>{money(r.balance)}</span> },
  ];

  const endingBalance = rows && rows.length > 0 ? rows[rows.length - 1].balance : 0;

  return (
    <div>
      <PageHeader title="دفترِ کل / معین" actions={
        <>
          <button className="btn btn-secondary" onClick={() => navigate('/trial-balance')}>تراز آزمایشی</button>
          <button className="btn btn-secondary" onClick={exportCsv} disabled={!rows}>اکسل</button>
          <button className="btn btn-secondary" onClick={() => window.print()}>چاپ</button>
        </>
      } />

      <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-start' }}>
        <div className="dgrid-wrap" style={{ width: 280, flex: 'none', maxHeight: 560, overflowY: 'auto', padding: 8 }}>
          {tree.map((n) => (
            <AccountTreeNode key={n.id} node={n} selectedId={accountId} expanded={expanded} onToggle={toggleExpand}
              onSelect={(a) => setSelectedAccount(a)} />
          ))}
          {tree.length === 0 && <div style={{ padding: 12, color: 'var(--text-muted)', fontSize: 12 }}>حسابی یافت نشد.</div>}
        </div>

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-3)', flexWrap: 'wrap' }}>
            <div className="field">
              <label className="label">از تاریخ</label>
              <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            </div>
            <div className="field">
              <label className="label">تا تاریخ</label>
              <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
            </div>
            <button className="btn btn-primary" onClick={() => accountId && search(accountId)} disabled={loading || !accountId}>
              {loading ? 'در حالِ جست‌وجو…' : 'نمایش'}
            </button>
          </div>

          {selectedAccount && (
            <div className="gbox" style={{ marginBottom: 'var(--space-3)' }}>
              <div className="gh" style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>{selectedAccount.code} — {selectedAccount.name}</span>
                {rows && (
                  <span className="num" style={{ fontWeight: 700, color: endingBalance >= 0 ? 'var(--text-strong)' : 'var(--success-700)' }}>
                    ماندهٔ پایانِ دوره: {money(endingBalance)}
                  </span>
                )}
              </div>
            </div>
          )}

          {!selectedAccount && <StatusMessage kind="muted">حسابی را از درختِ کنار انتخاب کنید.</StatusMessage>}
          {error && <StatusMessage kind="error">{error}</StatusMessage>}
          {rows && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => `${r.date}-${r.voucherNumber}-${r.debit}-${r.credit}`} emptyText="ردیفی یافت نشد." />}
        </div>
      </div>
    </div>
  );
}
