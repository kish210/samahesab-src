import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money, numberFormat } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { useActiveFiscalYear } from '../hooks/useActiveFiscalYear';

interface VoucherListDto {
  id: number;
  voucherNumber: string;
  voucherDate: string;
  voucherTypeName: string;
  statusName: string;
  totalDebit: number;
  totalCredit: number;
  description: string | null;
  isBalanced: boolean;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

interface VoucherTypeDto {
  id: number;
  name: string;
}

interface VoucherPreviewLineDto {
  accountName: string;
  debit: number;
  credit: number;
}

const STATUS_CHIPS: { label: string; value: number | null }[] = [
  { label: 'همه', value: null },
  { label: 'قطعی', value: 2 },
  { label: 'پیش‌نویس', value: 1 },
];

export function VouchersPage() {
  const navigate = useNavigate();
  const fiscalYearId = useActiveFiscalYear();
  const [fromDate, setFromDate] = useState('1405/01/01');
  const [toDate, setToDate] = useState('1405/12/29');
  const [search, setSearch] = useState('');
  const [typeId, setTypeId] = useState<number | null>(null);
  const [status, setStatus] = useState<number | null>(null);
  const [types, setTypes] = useState<VoucherTypeDto[]>([]);
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<VoucherListDto> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [selected, setSelected] = useState<VoucherListDto | null>(null);
  const [previewLines, setPreviewLines] = useState<VoucherPreviewLineDto[] | null>(null);
  const [actionMsg, setActionMsg] = useState<string | null>(null);

  useEffect(() => {
    apiGet<VoucherTypeDto[]>('/api/vouchers/types').then(setTypes).catch(() => {});
  }, []);

  async function search_(nextPage = 1) {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        fiscalYearId: String(fiscalYearId ?? 1),
        fromDate,
        toDate,
        page: String(nextPage),
        size: '20',
      });
      if (typeId != null) params.set('typeId', String(typeId));
      if (status != null) params.set('status', String(status));
      if (search.trim()) params.set('search', search.trim());
      const data = await apiGet<PagedResult<VoucherListDto>>(`/api/vouchers?${params.toString()}`);
      setResult(data);
      setPage(nextPage);
      setSelected(null);
      setPreviewLines(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ اسنادِ حسابداری.');
    } finally {
      setLoading(false);
    }
  }

  async function openPreview(row: VoucherListDto) {
    setSelected(row);
    setPreviewLines(null);
    setActionMsg(null);
    try {
      setPreviewLines(await apiGet<VoucherPreviewLineDto[]>(`/api/vouchers/${row.id}/preview`));
    } catch {
      setPreviewLines([]);
    }
  }

  async function postVoucher() {
    if (!selected) return;
    setActionMsg(null);
    try {
      await apiPost(`/api/vouchers/${selected.id}/post`, {});
      setActionMsg('سند قطعی شد.');
      search_(page);
    } catch (e) {
      setActionMsg(e instanceof ApiError ? e.message : 'قطعی‌سازیِ سند ناموفق بود.');
    }
  }

  async function copyVoucher() {
    if (!selected) return;
    setActionMsg(null);
    try {
      await apiPost<{ voucherId: number }>(`/api/vouchers/${selected.id}/copy`, {
        date: selected.voucherDate,
      });
      setActionMsg('سند به‌صورتِ پیش‌نویسِ نو کپی شد.');
      search_(page);
    } catch (e) {
      setActionMsg(e instanceof ApiError ? e.message : 'کپیِ سند ناموفق بود.');
    }
  }

  async function reverseVoucher() {
    if (!selected) return;
    setActionMsg(null);
    try {
      await apiPost(`/api/vouchers/${selected.id}/reverse`, { date: selected.voucherDate });
      setActionMsg('سندِ معکوس ثبت شد.');
      search_(page);
    } catch (e) {
      setActionMsg(e instanceof ApiError ? e.message : 'ثبتِ سندِ معکوس ناموفق بود.');
    }
  }

  function exportCsv() {
    if (!result) return;
    const header = ['شماره', 'تاریخ', 'نوع', 'شرح', 'بدهکار', 'بستانکار', 'وضعیت'];
    const rows = result.items.map((r) => [r.voucherNumber, r.voucherDate, r.voucherTypeName, r.description ?? '', r.totalDebit, r.totalCredit, r.statusName]);
    const csv = [header, ...rows].map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'اسناد-حسابداری.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  const columns: Column<VoucherListDto>[] = [
    { key: 'num', header: 'شمارهٔ سند', render: (r) => r.voucherNumber },
    { key: 'date', header: 'تاریخ', render: (r) => r.voucherDate },
    { key: 'type', header: 'نوع', render: (r) => r.voucherTypeName },
    { key: 'desc', header: 'شرح', render: (r) => r.description ?? '' },
    { key: 'debit', header: 'بدهکار', numeric: true, render: (r) => money(r.totalDebit) },
    { key: 'credit', header: 'بستانکار', numeric: true, render: (r) => money(r.totalCredit) },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${r.isBalanced ? 'badge-green' : 'badge-red'}`}>{r.statusName}</span> },
  ];

  const footerDebit = result?.items.reduce((s, r) => s + r.totalDebit, 0) ?? 0;
  const footerCredit = result?.items.reduce((s, r) => s + r.totalCredit, 0) ?? 0;

  return (
    <div>
      <PageHeader title="اسنادِ حسابداری" actions={
        <>
          <button className="btn btn-secondary" onClick={() => navigate('/accounts')}>دفترِ کل</button>
          <button className="btn btn-secondary" onClick={() => navigate('/trial-balance')}>تراز آزمایشی</button>
          <button className="btn btn-secondary" onClick={exportCsv} disabled={!result}>خروجی اکسل</button>
          <button className="btn btn-secondary" onClick={() => window.print()}>چاپ</button>
          <button className="btn btn-primary" onClick={() => navigate('/vouchers/new')}>+ سندِ نو</button>
        </>
      } />

      <div className="fltbar" style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-4)', flexWrap: 'wrap' }}>
        <div className="field">
          <label className="label">جست‌وجو</label>
          <input className="input" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="شماره، شرح…" style={{ width: 200 }} />
        </div>
        <div className="field">
          <label className="label">از تاریخ</label>
          <input className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} placeholder="1405/01/01" />
        </div>
        <div className="field">
          <label className="label">تا تاریخ</label>
          <input className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} placeholder="1405/12/29" />
        </div>
        <div className="field">
          <label className="label">نوعِ سند</label>
          <select className="select" value={typeId ?? ''} onChange={(e) => setTypeId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">همه انواع</option>
            {types.map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          {STATUS_CHIPS.map((c) => (
            <div key={c.label} className={`chip ${status === c.value ? 'active' : ''}`} style={{ cursor: 'pointer' }}
              onClick={() => setStatus(c.value)}>
              {c.label}
            </div>
          ))}
        </div>
        <button className="btn btn-primary" onClick={() => search_(1)} disabled={loading}>
          {loading ? 'در حالِ جست‌وجو…' : 'جست‌وجو'}
        </button>
      </div>

      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {result && !error && (
        <div className="split" style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
          <div className="list-col" style={{ flex: 1, minWidth: 0 }}>
            <DataTable
              columns={columns}
              rows={result.items}
              rowKey={(r) => r.id}
              emptyText="سندی یافت نشد."
              selectedKey={selected?.id ?? null}
              onRowClick={openPreview}
            />
            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>
              <span>
                جمعِ اسنادِ فیلترشده — {numberFormat.format(result.totalCount)} سند · صفحهٔ {numberFormat.format(result.pageNumber)} از {numberFormat.format(result.totalPages)} · بدهکارِ صفحه {money(footerDebit)} = بستانکارِ صفحه {money(footerCredit)}
              </span>
              <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                <button className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => search_(page - 1)}>قبلی</button>
                <button className="btn btn-secondary btn-sm" disabled={page >= result.totalPages} onClick={() => search_(page + 1)}>بعدی</button>
              </div>
            </div>
          </div>

          {selected && (
            <div className="preview-col" style={{ width: 320, flex: 'none', display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div className="gbox">
                <div className="gh">
                  پیش‌نمایشِ سند {selected.voucherNumber}
                  <span className={`st ${selected.statusName === 'قطعی' ? 'g' : 'a'}`} style={{ marginInlineStart: 6 }}>
                    <i />{selected.statusName}
                  </span>
                </div>
                <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}><span style={{ color: 'var(--text-muted)' }}>تاریخ</span><b>{selected.voucherDate}</b></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}><span style={{ color: 'var(--text-muted)' }}>نوع</span><b>{selected.voucherTypeName}</b></div>
                  {selected.description && <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 2 }}>{selected.description}</div>}
                </div>
              </div>

              {previewLines && (
                <div className="dgrid-wrap">
                  <table className="dgrid" style={{ fontSize: 11.5 }}>
                    <thead><tr><th>حساب</th><th className="num">بدهکار</th><th className="num">بستانکار</th></tr></thead>
                    <tbody>
                      {previewLines.map((l, i) => (
                        <tr key={i}>
                          <td>{l.accountName}</td>
                          <td className="num strong">{l.debit ? money(l.debit) : '۰'}</td>
                          <td className="num strong">{l.credit ? money(l.credit) : '۰'}</td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      <tr>
                        <td>جمع</td>
                        <td className="num">{money(previewLines.reduce((s, l) => s + l.debit, 0))}</td>
                        <td className="num">{money(previewLines.reduce((s, l) => s + l.credit, 0))}</td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              )}

              <div className={`sumbar ${selected.isBalanced ? 'ok' : 'bad'}`} style={{ justifyContent: 'center' }}>
                <b>{selected.isBalanced ? '✓ تراز' : '✗ ناتراز'}</b>
              </div>

              {actionMsg && <StatusMessage kind={actionMsg.includes('ناموفق') ? 'error' : 'success'}>{actionMsg}</StatusMessage>}

              <div style={{ display: 'flex', gap: 6 }}>
                {selected.statusName === 'پیش‌نویس' && (
                  <button className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={postVoucher}>قطعی‌سازی</button>
                )}
                <button className="btn btn-secondary btn-sm" style={{ flex: 1 }} onClick={copyVoucher}>کپیِ سند</button>
                {selected.statusName === 'قطعی' && (
                  <button className="btn btn-secondary btn-sm" style={{ flex: 1 }} onClick={reverseVoucher}>سندِ معکوس</button>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
